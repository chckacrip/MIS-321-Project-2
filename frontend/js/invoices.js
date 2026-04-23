import { exportInvoicePdf } from './pdfExport.js';
import { downloadCSV } from './app.js';

const fmt = n => '$' + Number(n).toLocaleString('en-US', { minimumFractionDigits: 2 });

let _allInvoices = [];
let _selecting   = false;

export async function renderInvoices(container, mode) {
  _selecting = false;
  container.innerHTML = `
    <div class="page">
      <div class="page-header">
        <h2>Invoices</h2>
        <div class="header-actions">
          <button class="btn-secondary" id="btn-export-csv">Export CSV</button>
        </div>
      </div>
      <div class="filter-bar">
        <input class="filter-input" type="text" id="invoices-search" placeholder="Search invoice #, load #, bill to…" />
        <select class="filter-select" id="invoices-status-filter">
          <option value="">All Statuses</option>
          <option value="unpaid">Unpaid</option>
          <option value="paid">Paid</option>
        </select>
      </div>
      <div id="invoices-selection-bar" class="selection-bar hidden">
        <label class="select-all-wrap">
          <input type="checkbox" id="invoices-select-all" />
          <span>Select All</span>
        </label>
        <span id="invoices-select-count" class="select-count">0 selected</span>
        <button class="btn-primary" id="invoices-btn-download">Download CSV</button>
        <button class="btn-secondary" id="invoices-btn-cancel-select">Cancel</button>
      </div>
      <div id="invoices-table-wrap"></div>
    </div>
    <div class="modal-overlay hidden" id="modal-overlay">
      <div class="modal" id="modal"></div>
    </div>
  `;

  await loadTable();

  const pendingId = localStorage.getItem('openInvoiceId');
  if (pendingId) {
    localStorage.removeItem('openInvoiceId');
    openDetailModal(pendingId);
  }

  document.getElementById('modal-overlay').addEventListener('click', e => {
    if (e.target === e.currentTarget) closeModal();
  });
}

async function loadTable() {
  _selecting = false;
  const bar    = document.getElementById('invoices-selection-bar');
  const csvBtn = document.getElementById('btn-export-csv');
  if (bar)    bar.classList.add('hidden');
  if (csvBtn) csvBtn.classList.remove('hidden');

  _allInvoices = await fetch('/api/invoices').then(r => r.json());

  document.getElementById('invoices-search').addEventListener('input', renderTable);
  document.getElementById('invoices-status-filter').addEventListener('change', renderTable);

  if (csvBtn) {
    csvBtn.onclick = () => {
      _selecting = true;
      bar.classList.remove('hidden');
      csvBtn.classList.add('hidden');
      renderTable();

      document.getElementById('invoices-select-all').onclick = e => {
        document.querySelectorAll('#invoices-table-wrap .row-check')
          .forEach(cb => cb.checked = e.target.checked);
        updateSelectCount('invoices');
      };

      document.getElementById('invoices-btn-cancel-select').onclick = () => {
        _selecting = false;
        bar.classList.add('hidden');
        csvBtn.classList.remove('hidden');
        renderTable();
      };

      document.getElementById('invoices-btn-download').onclick = () => {
        const ids  = [...document.querySelectorAll('#invoices-table-wrap .row-check:checked')]
          .map(cb => Number(cb.dataset.id));
        const rows = _allInvoices.filter(i => ids.includes(i.invoice_id));
        if (!rows.length) return;
        downloadCSV(
          [
            ['Invoice #', 'Load #', 'Bill To', 'Origin', 'Destination', 'Total', 'Invoice Date', 'Due Date', 'Status'],
            ...rows.map(i => [
              i.invoice_number, i.load_number, i.bill_to_name, i.origin, i.destination,
              (Number(i.line_haul_rate) + Number(i.fsc_rate)).toFixed(2),
              i.invoice_date, i.due_date, i.payment_status,
            ]),
          ],
          'invoices.csv'
        );
      };
    };
  }

  renderTable();
}

function renderTable() {
  const q      = (document.getElementById('invoices-search')?.value ?? '').toLowerCase();
  const status = document.getElementById('invoices-status-filter')?.value ?? '';

  const filtered = _allInvoices.filter(i => {
    if (status && i.payment_status !== status) return false;
    if (q) {
      const haystack = [i.invoice_number, i.load_number, i.bill_to_name, i.origin, i.destination]
        .join(' ').toLowerCase();
      if (!haystack.includes(q)) return false;
    }
    return true;
  });

  const wrap = document.getElementById('invoices-table-wrap');

  if (!filtered.length) {
    wrap.innerHTML = '<p class="empty-state">No invoices match your filters.</p>';
    if (_selecting) updateSelectCount('invoices');
    return;
  }

  wrap.innerHTML = `
    <table class="data-table">
      <thead>
        <tr>
          ${_selecting ? '<th class="col-check"></th>' : ''}
          <th>Invoice #</th>
          <th>Load #</th>
          <th>Bill To</th>
          <th>Route</th>
          <th>Total</th>
          <th>Invoice Date</th>
          <th>Due Date</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        ${filtered.map(i => `
          <tr class="clickable-row" data-id="${i.invoice_id}">
            ${_selecting ? `<td class="col-check"><input type="checkbox" class="row-check" data-id="${i.invoice_id}" /></td>` : ''}
            <td class="mono">${i.invoice_number}</td>
            <td class="mono">${i.load_number}</td>
            <td>${i.bill_to_name}</td>
            <td>${i.origin} → ${i.destination}</td>
            <td>${fmt(Number(i.line_haul_rate) + Number(i.fsc_rate) + Number(i.tarp_rate) + Number(i.extra_fee))}</td>
            <td>${i.invoice_date}</td>
            <td>${i.due_date}</td>
            <td><span class="status-badge status-${i.payment_status}">${i.payment_status}</span></td>
          </tr>
        `).join('')}
      </tbody>
    </table>
  `;

  if (_selecting) {
    const sa = document.getElementById('invoices-select-all');
    if (sa) sa.checked = false;
    updateSelectCount('invoices');

    wrap.querySelectorAll('.row-check').forEach(cb => {
      cb.addEventListener('change', () => updateSelectCount('invoices'));
    });

    wrap.querySelectorAll('.clickable-row').forEach(row => {
      row.addEventListener('click', e => {
        if (e.target.type === 'checkbox') return;
        const cb = row.querySelector('.row-check');
        if (cb) { cb.checked = !cb.checked; updateSelectCount('invoices'); }
      });
    });
  } else {
    wrap.querySelectorAll('.clickable-row').forEach(row => {
      row.addEventListener('click', () => openDetailModal(row.dataset.id));
    });
  }
}

function updateSelectCount(prefix) {
  const checked = document.querySelectorAll(`#${prefix}-table-wrap .row-check:checked`).length;
  const total   = document.querySelectorAll(`#${prefix}-table-wrap .row-check`).length;
  const el = document.getElementById(`${prefix}-select-count`);
  if (el) el.textContent = `${checked} of ${total} selected`;
  const sa = document.getElementById(`${prefix}-select-all`);
  if (sa) {
    sa.checked = checked === total && total > 0;
    sa.indeterminate = checked > 0 && checked < total;
  }
}

async function openDetailModal(invoiceId) {
  const inv = await fetch(`/api/invoices/${invoiceId}`).then(r => r.json());
  const total = Number(inv.line_haul_rate) + Number(inv.fsc_rate) + Number(inv.tarp_rate) + Number(inv.extra_fee);

  const modal = document.getElementById('modal');
  modal.innerHTML = `
    <div class="modal-header">
      <h3>Invoice #${inv.invoice_number}</h3>
      <button class="modal-close" id="modal-close">&times;</button>
    </div>
    <div class="modal-body">
      <div class="detail-grid">
        <div class="detail-section">
          <h4>Bill To</h4>
          <div class="detail-row"><span>Name</span><span>${inv.bill_to_name}</span></div>
          <div class="detail-row"><span>Address</span><span>${inv.bill_to_address}</span></div>
        </div>
        <div class="detail-section">
          <h4>Consignee</h4>
          <div class="detail-row"><span>Name</span><span>${inv.consignee_name}</span></div>
          <div class="detail-row"><span>Address</span><span>${inv.consignee_address}</span></div>
        </div>
        <div class="detail-section">
          <h4>Load Info</h4>
          <div class="detail-row"><span>Load #</span><span class="mono">${inv.load_number}</span></div>
          <div class="detail-row"><span>Ship Date</span><span>${inv.ship_date}</span></div>
          <div class="detail-row"><span>Route</span><span>${inv.origin} → ${inv.destination}</span></div>
          <div class="detail-row"><span>Description</span><span>${inv.description}</span></div>
          ${inv.unit_number ? `<div class="detail-row"><span>Unit #</span><span class="mono">${inv.unit_number}</span></div>` : ''}
          <div class="detail-row"><span>Terms</span><span>${inv.terms}</span></div>
        </div>
        <div class="detail-section">
          <h4>Charges</h4>
          <div class="detail-row"><span>Line Haul</span><span>${fmt(inv.line_haul_rate)}</span></div>
          <div class="detail-row"><span>FSC</span><span>${Number(inv.fsc_rate) > 0 ? fmt(inv.fsc_rate) : '—'}</span></div>
          <div class="detail-row"><span>Tarp</span><span>${Number(inv.tarp_rate) > 0 ? fmt(inv.tarp_rate) : '—'}</span></div>
          <div class="detail-row"><span>Extra Fee</span><span>${Number(inv.extra_fee) > 0 ? fmt(inv.extra_fee) : '—'}</span></div>
          <div class="detail-row"><span>Total</span><strong>${fmt(total)}</strong></div>
        </div>
        <div class="detail-section">
          <h4>Payment</h4>
          <div class="detail-row"><span>Invoice Date</span><span>${inv.invoice_date}</span></div>
          <div class="detail-row"><span>Due Date</span><span>${inv.due_date}</span></div>
          <div class="detail-row"><span>Status</span><span><span class="status-badge status-${inv.payment_status}">${inv.payment_status}</span></span></div>
          ${inv.paid_date ? `<div class="detail-row"><span>Paid Date</span><span>${inv.paid_date}</span></div>` : ''}
        </div>
      </div>

      <div class="modal-actions">
        <button class="btn-secondary" id="btn-export-pdf">Export PDF</button>
        ${inv.payment_status === 'unpaid' ? `
          <button class="btn-primary" id="btn-mark-paid">Mark as Paid</button>
        ` : ''}
      </div>
    </div>
  `;

  document.getElementById('modal-close').addEventListener('click', closeModal);
  document.getElementById('btn-export-pdf').addEventListener('click', () => exportInvoicePdf(inv));

  if (inv.payment_status === 'unpaid') {
    document.getElementById('btn-mark-paid').addEventListener('click', async () => {
      await fetch(`/api/invoices/${invoiceId}/mark-paid`, { method: 'PATCH' });
      closeModal();
      _allInvoices = await fetch('/api/invoices').then(r => r.json());
      renderTable();
    });
  }

  showModal();
}


function showModal()  { document.getElementById('modal-overlay').classList.remove('hidden'); }
function closeModal() { document.getElementById('modal-overlay').classList.add('hidden'); }

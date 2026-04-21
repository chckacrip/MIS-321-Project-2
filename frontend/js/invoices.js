import { exportInvoicePdf } from './pdfExport.js';
const fmt = n => '$' + Number(n).toLocaleString('en-US', { minimumFractionDigits: 2 });

export async function renderInvoices(container, mode) {
  container.innerHTML = `
    <div class="page">
      <div class="page-header">
        <h2>Invoices</h2>
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
  const invoices = await fetch('/api/invoices').then(r => r.json());
  const wrap = document.getElementById('invoices-table-wrap');

  if (!invoices.length) {
    wrap.innerHTML = '<p class="empty-state">No invoices yet.</p>';
    return;
  }

  wrap.innerHTML = `
    <table class="data-table">
      <thead>
        <tr>
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
        ${invoices.map(i => `
          <tr class="clickable-row" data-id="${i.invoice_id}">
            <td class="mono">${i.invoice_number}</td>
            <td class="mono">${i.load_number}</td>
            <td>${i.bill_to_name}</td>
            <td>${i.origin} → ${i.destination}</td>
            <td>${fmt(Number(i.line_haul_rate) + Number(i.fsc_rate))}</td>
            <td>${i.invoice_date}</td>
            <td>${i.due_date}</td>
            <td><span class="status-badge status-${i.payment_status}">${i.payment_status}</span></td>
          </tr>
        `).join('')}
      </tbody>
    </table>
  `;

  wrap.querySelectorAll('.clickable-row').forEach(row => {
    row.addEventListener('click', () => openDetailModal(row.dataset.id));
  });
}

async function openDetailModal(invoiceId) {
  const inv = await fetch(`/api/invoices/${invoiceId}`).then(r => r.json());
  const total = Number(inv.line_haul_rate) + Number(inv.fsc_rate);

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
      await loadTable();
    });
  }

  showModal();
}


function showModal()  { document.getElementById('modal-overlay').classList.remove('hidden'); }
function closeModal() { document.getElementById('modal-overlay').classList.add('hidden'); }

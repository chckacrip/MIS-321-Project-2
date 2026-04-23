import { downloadCSV, getUser } from './app.js';

const fmt = n => '$' + Number(n).toLocaleString('en-US', { minimumFractionDigits: 2 });

let _allSummaries = [];
let _selecting    = false;

export async function renderDriverPay(container, mode) {
  _selecting = false;
  container.innerHTML = `
    <div class="page">
      <div class="page-header">
        <h2>${mode === 'trucker' ? 'My Pay' : 'Driver Pay'}</h2>
        <div class="header-actions">
          <button class="btn-secondary" id="btn-export-csv">Export CSV</button>
          ${mode === 'employee' ? '<button class="btn-primary" id="btn-generate">+ Generate Pay Summary</button>' : ''}
        </div>
      </div>
      ${mode === 'employee' ? `
      <div class="filter-bar">
        <input class="filter-input" type="text" id="pay-search" placeholder="Search driver name…" />
        <select class="filter-select" id="pay-driver-filter">
          <option value="">All Drivers</option>
        </select>
      </div>` : ''}
      <div id="pay-selection-bar" class="selection-bar hidden">
        <label class="select-all-wrap">
          <input type="checkbox" id="pay-select-all" />
          <span>Select All</span>
        </label>
        <span id="pay-select-count" class="select-count">0 selected</span>
        <button class="btn-primary" id="pay-btn-download">Download CSV</button>
        <button class="btn-secondary" id="pay-btn-cancel-select">Cancel</button>
      </div>
      <div id="pay-table-wrap"></div>
    </div>
    <div class="modal-overlay hidden" id="modal-overlay">
      <div class="modal" id="modal"></div>
    </div>
  `;

  const driverId = mode === 'trucker' ? getUser()?.driverId ?? null : null;
  await loadTable(driverId, mode);

  if (mode === 'employee') {
    document.getElementById('btn-generate').addEventListener('click', () => openGenerateModal());
  }

  document.getElementById('modal-overlay').addEventListener('click', e => {
    if (e.target === e.currentTarget) closeModal();
  });
}

async function loadTable(driverId, mode) {
  _selecting = false;
  const bar    = document.getElementById('pay-selection-bar');
  const csvBtn = document.getElementById('btn-export-csv');
  if (bar)    bar.classList.add('hidden');
  if (csvBtn) csvBtn.classList.remove('hidden');

  const url = driverId ? `/api/driver-pay?driverId=${driverId}` : '/api/driver-pay';
  _allSummaries = await fetch(url).then(r => r.json());

  if (mode === 'employee') {
    const driverFilter = document.getElementById('pay-driver-filter');
    if (driverFilter) {
      const names = [...new Set(_allSummaries.map(s => s.driver_name).filter(Boolean))].sort();
      driverFilter.innerHTML = `<option value="">All Drivers</option>` +
        names.map(n => `<option value="${n}">${n}</option>`).join('');
      driverFilter.addEventListener('change', () => renderPayTable(mode));
    }
    document.getElementById('pay-search')?.addEventListener('input', () => renderPayTable(mode));
  }

  if (csvBtn) {
    csvBtn.onclick = () => {
      _selecting = true;
      bar.classList.remove('hidden');
      csvBtn.classList.add('hidden');
      renderPayTable(mode);

      document.getElementById('pay-select-all').onclick = e => {
        document.querySelectorAll('#pay-table-wrap .row-check')
          .forEach(cb => cb.checked = e.target.checked);
        updateSelectCount('pay');
      };

      document.getElementById('pay-btn-cancel-select').onclick = () => {
        _selecting = false;
        bar.classList.add('hidden');
        csvBtn.classList.remove('hidden');
        renderPayTable(mode);
      };

      document.getElementById('pay-btn-download').onclick = () => {
        const ids  = [...document.querySelectorAll('#pay-table-wrap .row-check:checked')]
          .map(cb => Number(cb.dataset.id));
        const rows = _allSummaries.filter(s => ids.includes(s.summary_id));
        if (!rows.length) return;
        downloadCSV(
          [
            ['Driver', 'Unit', 'Pay Period Start', 'Pay Period End', 'Line Haul', 'FSC',
             'Commission Rate', 'Commission', 'Advances', 'Insurance', 'Workers Comp', 'Net Pay'],
            ...rows.map(s => {
              const commission = (Number(s.total_line_haul) * Number(s.commission_rate)).toFixed(2);
              return [
                s.driver_name, s.unit_number, s.pay_period_start, s.pay_period_end,
                s.total_line_haul, s.total_fsc,
                (Number(s.commission_rate) * 100).toFixed(0) + '%', commission,
                s.total_advances, s.insurance_deduction, s.workers_comp_deduction, s.net_pay,
              ];
            }),
          ],
          'driver-pay.csv'
        );
      };
    };
  }

  renderPayTable(mode);
}

function renderPayTable(mode) {
  const q      = (document.getElementById('pay-search')?.value ?? '').toLowerCase();
  const driver = document.getElementById('pay-driver-filter')?.value ?? '';

  const filtered = _allSummaries.filter(s => {
    if (driver && s.driver_name !== driver) return false;
    if (q && !(s.driver_name ?? '').toLowerCase().includes(q)) return false;
    return true;
  });

  const wrap = document.getElementById('pay-table-wrap');

  if (!filtered.length) {
    wrap.innerHTML = '<p class="empty-state">No pay summaries match your filters.</p>';
    if (_selecting) updateSelectCount('pay');
    return;
  }

  wrap.innerHTML = `
    <table class="data-table">
      <thead>
        <tr>
          ${_selecting ? '<th class="col-check"></th>' : ''}
          ${mode === 'employee' ? '<th>Driver</th><th>Unit</th>' : ''}
          <th>Pay Period</th>
          <th>Line Haul</th>
          <th>FSC</th>
          <th>Commission</th>
          <th>Advances</th>
          <th>Net Pay</th>
        </tr>
      </thead>
      <tbody>
        ${filtered.map(s => {
          const commission = Number(s.total_line_haul) * Number(s.commission_rate);
          return `
            <tr class="clickable-row" data-id="${s.summary_id}">
              ${_selecting ? `<td class="col-check"><input type="checkbox" class="row-check" data-id="${s.summary_id}" /></td>` : ''}
              ${mode === 'employee' ? `<td>${s.driver_name}</td><td class="mono">${s.unit_number}</td>` : ''}
              <td>${s.pay_period_start} – ${s.pay_period_end}</td>
              <td>${fmt(s.total_line_haul)}</td>
              <td>${fmt(s.total_fsc)}</td>
              <td class="negative">-${fmt(commission)}</td>
              <td class="negative">-${fmt(s.total_advances)}</td>
              <td><strong>${fmt(s.net_pay)}</strong></td>
            </tr>
          `;
        }).join('')}
      </tbody>
    </table>
  `;

  if (_selecting) {
    const sa = document.getElementById('pay-select-all');
    if (sa) sa.checked = false;
    updateSelectCount('pay');

    wrap.querySelectorAll('.row-check').forEach(cb => {
      cb.addEventListener('change', () => updateSelectCount('pay'));
    });

    wrap.querySelectorAll('.clickable-row').forEach(row => {
      row.addEventListener('click', e => {
        if (e.target.type === 'checkbox') return;
        const cb = row.querySelector('.row-check');
        if (cb) { cb.checked = !cb.checked; updateSelectCount('pay'); }
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

async function openDetailModal(summaryId) {
  const { summary, loads, advances } = await fetch(`/api/driver-pay/${summaryId}`).then(r => r.json());

  const commission = Number(summary.total_line_haul) * Number(summary.commission_rate);
  const grossPay   = Number(summary.total_line_haul) - commission + Number(summary.total_fsc)
                   + Number(summary.total_tarp ?? 0) + Number(summary.total_extra_fee ?? 0);

  const modal = document.getElementById('modal');
  modal.innerHTML = `
    <div class="modal-header">
      <h3>${summary.driver_name} — Pay Summary</h3>
      <button class="modal-close" id="modal-close">&times;</button>
    </div>
    <div class="modal-body">
      <div class="detail-grid">
        <div class="detail-section">
          <h4>Driver</h4>
          <div class="detail-row"><span>Unit</span><span class="mono">${summary.unit_number}</span></div>
          <div class="detail-row"><span>Address</span><span>${summary.address}</span></div>
          <div class="detail-row"><span>Pay Period</span><span>${summary.pay_period_start} – ${summary.pay_period_end}</span></div>
          <div class="detail-row"><span>Commission Rate</span><span>${(Number(summary.commission_rate) * 100).toFixed(0)}%</span></div>
        </div>
        <div class="detail-section">
          <h4>Pay Breakdown</h4>
          <div class="detail-row"><span>Total Line Haul</span><span>${fmt(summary.total_line_haul)}</span></div>
          <div class="detail-row"><span>Commission (${(Number(summary.commission_rate)*100).toFixed(0)}%)</span><span class="negative">-${fmt(commission)}</span></div>
          <div class="detail-row"><span>FSC (100%)</span><span>${fmt(summary.total_fsc)}</span></div>
          ${Number(summary.total_tarp) > 0 ? `<div class="detail-row"><span>Tarp (100%)</span><span>${fmt(summary.total_tarp)}</span></div>` : ''}
          ${Number(summary.total_extra_fee) > 0 ? `<div class="detail-row"><span>Extra Fee (100%)</span><span>${fmt(summary.total_extra_fee)}</span></div>` : ''}
          <div class="detail-row"><span>Gross Pay</span><strong>${fmt(grossPay)}</strong></div>
          <div class="detail-row"><span>Advances</span><span class="negative">-${fmt(summary.total_advances)}</span></div>
          <div class="detail-row"><span>Insurance</span><span class="negative">-${fmt(summary.insurance_deduction)}</span></div>
          <div class="detail-row"><span>Workers' Comp</span><span class="negative">-${fmt(summary.workers_comp_deduction)}</span></div>
          <div class="detail-row"><span>Net Pay</span><strong class="net-pay">${fmt(summary.net_pay)}</strong></div>
        </div>
      </div>

      ${loads.length ? `
        <h4 style="margin-top:1.5rem">Loads This Period</h4>
        <table class="data-table">
          <thead><tr><th>Load #</th><th>Date</th><th>Route</th><th>Line Haul</th><th>FSC</th></tr></thead>
          <tbody>
            ${loads.map(l => `
              <tr>
                <td class="mono">${l.load_number}</td>
                <td>${l.ship_date}</td>
                <td>${l.route}</td>
                <td>${fmt(l.line_haul_rate)}</td>
                <td>${Number(l.fsc_rate) > 0 ? fmt(l.fsc_rate) : '—'}</td>
              </tr>
            `).join('')}
          </tbody>
        </table>
      ` : ''}

      ${advances.length ? `
        <h4 style="margin-top:1.5rem">Advances This Period</h4>
        <table class="data-table">
          <thead><tr><th>Date</th><th>Type</th><th>Amount</th><th>Notes</th></tr></thead>
          <tbody>
            ${advances.map(a => `
              <tr>
                <td>${a.advance_date}</td>
                <td>${a.advance_type}</td>
                <td>${fmt(a.amount)}</td>
                <td>${a.notes ?? '—'}</td>
              </tr>
            `).join('')}
          </tbody>
        </table>
      ` : ''}
    </div>
  `;

  document.getElementById('modal-close').addEventListener('click', closeModal);
  showModal();
}

async function openGenerateModal() {
  const drivers = await fetch('/api/drivers').then(r => r.json());

  const modal = document.getElementById('modal');
  modal.innerHTML = `
    <div class="modal-header">
      <h3>Generate Pay Summary</h3>
      <button class="modal-close" id="modal-close">&times;</button>
    </div>
    <div class="modal-body">
      <form id="generate-form">
        <div class="form-grid">
          <div class="form-group">
            <label>Driver</label>
            <select name="driverId" required>
              <option value="">Select driver...</option>
              <option value="all">— All Drivers —</option>
              ${drivers.map(d => `<option value="${d.driver_id}">${d.name} (Unit ${d.unit_number})</option>`).join('')}
            </select>
          </div>
          <div class="form-group">
            <label>Pay Period Start</label>
            <input name="payPeriodStart" type="date" required id="pay-start" />
          </div>
          <div class="form-group">
            <label>Pay Period End</label>
            <input name="payPeriodEnd" type="date" required id="pay-end" />
          </div>
          <div class="form-group">
            <label>Insurance Deduction</label>
            <input name="insuranceDeduction" type="number" step="0.01" value="284.20" />
          </div>
          <div class="form-group">
            <label>Workers' Comp Deduction</label>
            <input name="workersCompDeduction" type="number" step="0.01" value="33.70" />
          </div>
        </div>
        <div id="pay-preview"></div>
        <div class="modal-actions">
          <button type="button" class="btn-secondary" id="modal-close-btn">Cancel</button>
          <button type="submit" class="btn-primary">Generate</button>
        </div>
      </form>
    </div>
  `;

  document.getElementById('modal-close').addEventListener('click', closeModal);
  document.getElementById('modal-close-btn').addEventListener('click', closeModal);

  document.getElementById('pay-start').addEventListener('change', e => {
    const start = new Date(e.target.value + 'T00:00:00');
    if (!isNaN(start)) {
      const end = new Date(start);
      end.setDate(end.getDate() + 6);
      document.getElementById('pay-end').value = end.toISOString().split('T')[0];
    }
  });

  document.getElementById('generate-form').addEventListener('submit', async e => {
    e.preventDefault();
    const f = e.target;
    const payload = {
      payPeriodStart:       f.payPeriodStart.value,
      payPeriodEnd:         f.payPeriodEnd.value,
      insuranceDeduction:   parseFloat(f.insuranceDeduction.value) || 0,
      workersCompDeduction: parseFloat(f.workersCompDeduction.value) || 0,
    };

    if (f.driverId.value === 'all') {
      await Promise.all(drivers.map(d =>
        fetch('/api/driver-pay/generate', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ ...payload, driverId: d.driver_id }),
        })
      ));
      closeModal();
      _allSummaries = await fetch('/api/driver-pay').then(r => r.json());
      renderPayTable('manager');
    } else {
      const res = await fetch('/api/driver-pay/generate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...payload, driverId: parseInt(f.driverId.value) }),
      });
      const data = await res.json();
      closeModal();
      _allSummaries = await fetch('/api/driver-pay').then(r => r.json());
      renderPayTable('manager');
      openDetailModal(data.summary_id);
    }
  });

  showModal();
}

function showModal()  { document.getElementById('modal-overlay').classList.remove('hidden'); }
function closeModal() { document.getElementById('modal-overlay').classList.add('hidden'); }

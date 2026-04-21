const fmt = n => '$' + Number(n).toLocaleString('en-US', { minimumFractionDigits: 2 });

export async function renderDriverPay(container, mode) {
  container.innerHTML = `
    <div class="page">
      <div class="page-header">
        <h2>${mode === 'trucker' ? 'My Pay' : 'Driver Pay'}</h2>
        ${mode === 'manager' ? '<button class="btn-primary" id="btn-generate">+ Generate Pay Summary</button>' : ''}
      </div>
      <div id="pay-table-wrap"></div>
    </div>
    <div class="modal-overlay hidden" id="modal-overlay">
      <div class="modal" id="modal"></div>
    </div>
  `;

  const driverId = mode === 'trucker' ? 1 : null;
  await loadTable(driverId, mode);

  if (mode === 'manager') {
    document.getElementById('btn-generate').addEventListener('click', () => openGenerateModal());
  }

  document.getElementById('modal-overlay').addEventListener('click', e => {
    if (e.target === e.currentTarget) closeModal();
  });
}

async function loadTable(driverId, mode) {
  const url = driverId ? `/api/driver-pay?driverId=${driverId}` : '/api/driver-pay';
  const summaries = await fetch(url).then(r => r.json());
  const wrap = document.getElementById('pay-table-wrap');

  if (!summaries.length) {
    wrap.innerHTML = '<p class="empty-state">No pay summaries found.</p>';
    return;
  }

  wrap.innerHTML = `
    <table class="data-table">
      <thead>
        <tr>
          ${mode === 'manager' ? '<th>Driver</th><th>Unit</th>' : ''}
          <th>Pay Period</th>
          <th>Line Haul</th>
          <th>FSC</th>
          <th>Commission</th>
          <th>Advances</th>
          <th>Net Pay</th>
        </tr>
      </thead>
      <tbody>
        ${summaries.map(s => {
          const commission = Number(s.total_line_haul) * Number(s.commission_rate);
          return `
            <tr class="clickable-row" data-id="${s.summary_id}">
              ${mode === 'manager' ? `<td>${s.driver_name}</td><td class="mono">${s.unit_number}</td>` : ''}
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

  wrap.querySelectorAll('.clickable-row').forEach(row => {
    row.addEventListener('click', () => openDetailModal(row.dataset.id));
  });
}

async function openDetailModal(summaryId) {
  const { summary, loads, advances } = await fetch(`/api/driver-pay/${summaryId}`).then(r => r.json());

  const commission = Number(summary.total_line_haul) * Number(summary.commission_rate);
  const grossPay   = Number(summary.total_line_haul) - commission + Number(summary.total_fsc);

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
      await loadTable(null, 'manager');
    } else {
      const res = await fetch('/api/driver-pay/generate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...payload, driverId: parseInt(f.driverId.value) }),
      });
      const data = await res.json();
      closeModal();
      await loadTable(null, 'manager');
      openDetailModal(data.summary_id);
    }
  });

  showModal();
}

function showModal()  { document.getElementById('modal-overlay').classList.remove('hidden'); }
function closeModal() { document.getElementById('modal-overlay').classList.add('hidden'); }

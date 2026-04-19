const STATUS_ORDER = ['pending', 'complete', 'invoiced', 'paid'];

export async function renderLoads(container, mode) {
  container.innerHTML = `
    <div class="page">
      <div class="page-header">
        <h2>${mode === 'trucker' ? 'My Loads' : 'Loads'}</h2>
        ${mode === 'manager' ? '<button class="btn-primary" id="btn-new-load">+ New Load</button>' : ''}
      </div>
      <div id="loads-table-wrap"></div>
    </div>
    <div class="modal-overlay hidden" id="modal-overlay">
      <div class="modal" id="modal"></div>
    </div>
  `;

  const driverId = mode === 'trucker' ? 1 : null; // TODO: replace 1 with logged-in driver id
  await loadTable(driverId, mode);

  if (mode === 'manager') {
    document.getElementById('btn-new-load').addEventListener('click', () => openNewLoadModal());
  }

  document.getElementById('modal-overlay').addEventListener('click', e => {
    if (e.target === e.currentTarget) closeModal();
  });
}

async function loadTable(driverId, mode) {
  const url = driverId ? `/api/loads?driverId=${driverId}` : '/api/loads';
  const loads = await fetch(url).then(r => r.json());

  const wrap = document.getElementById('loads-table-wrap');

  if (!loads.length) {
    wrap.innerHTML = '<p class="empty-state">No loads found.</p>';
    return;
  }

  wrap.innerHTML = `
    <table class="data-table">
      <thead>
        <tr>
          <th>Load #</th>
          <th>Ship Date</th>
          <th>Route</th>
          <th>Description</th>
          <th>Driver</th>
          <th>Line Haul</th>
          <th>FSC</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        ${loads.map(l => `
          <tr class="clickable-row" data-id="${l.load_id}">
            <td class="mono">${l.load_number}</td>
            <td>${l.ship_date}</td>
            <td class="route-cell">${l.origin} → ${l.destination}</td>
            <td>${l.description}</td>
            <td>${l.driver ?? '—'}</td>
            <td>$${Number(l.line_haul_rate).toLocaleString('en-US', { minimumFractionDigits: 2 })}</td>
            <td>${l.fsc_rate > 0 ? '$' + Number(l.fsc_rate).toLocaleString('en-US', { minimumFractionDigits: 2 }) : '—'}</td>
            <td><span class="status-badge status-${l.status}">${l.status}</span></td>
          </tr>
        `).join('')}
      </tbody>
    </table>
  `;

  wrap.querySelectorAll('.clickable-row').forEach(row => {
    row.addEventListener('click', () => {
      const load = loads.find(l => l.load_id == row.dataset.id);
      openDetailModal(load, mode);
    });
  });
}

function openDetailModal(load, mode) {
  const modal = document.getElementById('modal');
  const canAdvance = mode === 'manager' && STATUS_ORDER.indexOf(load.status) < STATUS_ORDER.length - 1;
  const nextStatus = canAdvance ? STATUS_ORDER[STATUS_ORDER.indexOf(load.status) + 1] : null;

  modal.innerHTML = `
    <div class="modal-header">
      <h3>Load ${load.load_number}</h3>
      <button class="modal-close" id="modal-close">&times;</button>
    </div>
    <div class="modal-body">
      <div class="detail-grid">
        <div class="detail-section">
          <h4>Load Info</h4>
          <div class="detail-row"><span>Ship Date</span><span>${load.ship_date}</span></div>
          <div class="detail-row"><span>Description</span><span>${load.description}</span></div>
          <div class="detail-row"><span>Terms</span><span>${load.terms}</span></div>
          <div class="detail-row"><span>Driver</span><span>${load.driver ?? '—'}</span></div>
          <div class="detail-row"><span>Status</span><span><span class="status-badge status-${load.status}">${load.status}</span></span></div>
        </div>
        <div class="detail-section">
          <h4>Rate</h4>
          <div class="detail-row"><span>Line Haul</span><span>$${Number(load.line_haul_rate).toLocaleString('en-US', { minimumFractionDigits: 2 })}</span></div>
          <div class="detail-row"><span>FSC</span><span>${load.fsc_rate > 0 ? '$' + Number(load.fsc_rate).toLocaleString('en-US', { minimumFractionDigits: 2 }) : '—'}</span></div>
          <div class="detail-row"><span>Total</span><strong>$${(Number(load.line_haul_rate) + Number(load.fsc_rate)).toLocaleString('en-US', { minimumFractionDigits: 2 })}</strong></div>
        </div>
        <div class="detail-section">
          <h4>Bill To</h4>
          <div class="detail-row"><span>Name</span><span>${load.bill_to_name}</span></div>
          <div class="detail-row"><span>Address</span><span>${load.bill_to_address}</span></div>
        </div>
        <div class="detail-section">
          <h4>Consignee</h4>
          <div class="detail-row"><span>Name</span><span>${load.consignee_name}</span></div>
          <div class="detail-row"><span>Address</span><span>${load.consignee_address}</span></div>
        </div>
      </div>
      ${canAdvance ? `
        <div class="modal-actions">
          <button class="btn-primary" id="btn-advance-status">
            Mark as ${nextStatus.charAt(0).toUpperCase() + nextStatus.slice(1)}
          </button>
        </div>
      ` : ''}
    </div>
  `;

  if (canAdvance) {
    document.getElementById('btn-advance-status').addEventListener('click', async () => {
      await fetch(`/api/loads/${load.load_id}/status`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: nextStatus }),
      });
      closeModal();
      const driverId = mode === 'trucker' ? 1 : null;
      await loadTable(driverId, mode);
    });
  }

  document.getElementById('modal-close').addEventListener('click', closeModal);
  showModal();
}

async function openNewLoadModal() {
  const drivers = await fetch('/api/drivers').then(r => r.json());

  const modal = document.getElementById('modal');
  modal.innerHTML = `
    <div class="modal-header">
      <h3>New Load</h3>
      <button class="modal-close" id="modal-close">&times;</button>
    </div>
    <div class="modal-body">
      <form id="new-load-form">
        <div class="form-grid">
          <div class="form-group">
            <label>Load Number</label>
            <input name="loadNumber" required placeholder="e.g. 1061465" />
          </div>
          <div class="form-group">
            <label>Ship Date</label>
            <input name="shipDate" type="date" required />
          </div>
          <div class="form-group">
            <label>Origin</label>
            <input name="origin" required placeholder="e.g. Birmingham, AL" />
          </div>
          <div class="form-group">
            <label>Destination</label>
            <input name="destination" required placeholder="e.g. Nashville, TN" />
          </div>
          <div class="form-group">
            <label>Description</label>
            <input name="description" required placeholder="e.g. Steel Coil" />
          </div>
          <div class="form-group">
            <label>Driver</label>
            <select name="driverId">
              <option value="">Unassigned</option>
              ${drivers.map(d => `<option value="${d.driver_id}">${d.name} (Unit ${d.unit_number})</option>`).join('')}
            </select>
          </div>
          <div class="form-group">
            <label>Line Haul Rate</label>
            <input name="lineHaulRate" type="number" step="0.01" required placeholder="0.00" />
          </div>
          <div class="form-group">
            <label>FSC Rate</label>
            <input name="fscRate" type="number" step="0.01" value="0" placeholder="0.00" />
          </div>
          <div class="form-group">
            <label>Terms</label>
            <input name="terms" value="Net 30" />
          </div>
          <div class="form-group">
            <label>Status</label>
            <select name="status">
              ${STATUS_ORDER.map(s => `<option value="${s}">${s}</option>`).join('')}
            </select>
          </div>
          <div class="form-group">
            <label>Bill To Name</label>
            <input name="billToName" placeholder="Company name" />
          </div>
          <div class="form-group">
            <label>Bill To Address</label>
            <input name="billToAddress" placeholder="Street, City, State" />
          </div>
          <div class="form-group">
            <label>Consignee Name</label>
            <input name="consigneeName" placeholder="Company name" />
          </div>
          <div class="form-group">
            <label>Consignee Address</label>
            <input name="consigneeAddress" placeholder="Street, City, State" />
          </div>
        </div>
        <div class="modal-actions">
          <button type="button" class="btn-secondary" id="modal-close-btn">Cancel</button>
          <button type="submit" class="btn-primary">Create Load</button>
        </div>
      </form>
    </div>
  `;

  document.getElementById('modal-close').addEventListener('click', closeModal);
  document.getElementById('modal-close-btn').addEventListener('click', closeModal);

  document.getElementById('new-load-form').addEventListener('submit', async e => {
    e.preventDefault();
    const f = e.target;
    await fetch('/api/loads', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        loadNumber:       f.loadNumber.value,
        shipDate:         f.shipDate.value,
        origin:           f.origin.value,
        destination:      f.destination.value,
        description:      f.description.value,
        lineHaulRate:     parseFloat(f.lineHaulRate.value),
        fscRate:          parseFloat(f.fscRate.value) || 0,
        terms:            f.terms.value,
        status:           f.status.value,
        billToName:       f.billToName.value,
        billToAddress:    f.billToAddress.value,
        consigneeName:    f.consigneeName.value,
        consigneeAddress: f.consigneeAddress.value,
        driverId:         f.driverId.value ? parseInt(f.driverId.value) : null,
      }),
    });
    closeModal();
    await loadTable(null, 'manager');
  });

  showModal();
}

function showModal() {
  document.getElementById('modal-overlay').classList.remove('hidden');
}

function closeModal() {
  document.getElementById('modal-overlay').classList.add('hidden');
}

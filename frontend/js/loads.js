const STATUS_ORDER = ['pending', 'complete', 'invoiced', 'paid'];
const ALL_STATUSES = ['pending', 'complete', 'invoiced', 'paid', 'cancelled'];

export async function renderLoads(container, mode) {
  container.innerHTML = `
    <div class="page">
      <div class="page-header">
        <h2>${mode === 'trucker' ? 'My Loads' : 'Loads'}</h2>
        ${mode === 'manager' ? '<button class="btn-primary" id="btn-new-load">+ New Load</button>' : ''}
      </div>
      <div class="filter-bar">
        <input class="filter-input" type="text" id="loads-search" placeholder="Search load #, route, description..." />
        <select class="filter-select" id="loads-status-filter">
          <option value="">All Statuses</option>
          ${ALL_STATUSES.map(s => `<option value="${s}">${cap(s)}</option>`).join('')}
        </select>
        ${mode === 'manager' ? `
        <select class="filter-select" id="loads-driver-filter">
          <option value="">All Drivers</option>
        </select>` : ''}
      </div>
      <div id="loads-table-wrap"></div>
    </div>
    <div class="modal-overlay hidden" id="modal-overlay">
      <div class="modal" id="modal"></div>
    </div>
  `;

  const driverId = mode === 'trucker' ? 1 : null;
  await loadTable(driverId, mode);

  if (mode === 'manager')
    document.getElementById('btn-new-load').addEventListener('click', () => openNewLoadModal());

  document.getElementById('modal-overlay').addEventListener('click', e => {
    if (e.target === e.currentTarget) closeModal();
  });
}

let _allLoads = [];

async function loadTable(driverId, mode) {
  const url = driverId ? `/api/loads?driverId=${driverId}` : '/api/loads';
  _allLoads  = await fetch(url).then(r => r.json());

  if (mode === 'manager') {
    const driverFilter = document.getElementById('loads-driver-filter');
    if (driverFilter) {
      const names = [...new Set(_allLoads.map(l => l.driver).filter(Boolean))].sort();
      driverFilter.innerHTML = `<option value="">All Drivers</option>` +
        names.map(n => `<option value="${n}">${n}</option>`).join('');
      driverFilter.addEventListener('change', () => renderTable(mode));
    }
  }

  document.getElementById('loads-search').addEventListener('input', () => renderTable(mode));
  document.getElementById('loads-status-filter').addEventListener('change', () => renderTable(mode));

  renderTable(mode);
}

function renderTable(mode) {
  const q      = (document.getElementById('loads-search')?.value ?? '').toLowerCase();
  const status = document.getElementById('loads-status-filter')?.value ?? '';
  const driver = document.getElementById('loads-driver-filter')?.value ?? '';

  const filtered = _allLoads.filter(l => {
    if (status && l.status !== status) return false;
    if (driver && l.driver !== driver) return false;
    if (q) {
      const haystack = [l.load_number, l.origin, l.destination, l.description, l.driver ?? '']
        .join(' ').toLowerCase();
      if (!haystack.includes(q)) return false;
    }
    return true;
  });

  const wrap = document.getElementById('loads-table-wrap');

  if (!filtered.length) {
    wrap.innerHTML = '<p class="empty-state">No loads match your filters.</p>';
    return;
  }

  wrap.innerHTML = `
    <table class="data-table">
      <thead>
        <tr>
          <th>Load #</th><th>Ship Date</th><th>Route</th><th>Description</th>
          <th>Driver</th><th>Line Haul</th><th>FSC</th><th>Status</th>
        </tr>
      </thead>
      <tbody>
        ${filtered.map(l => `
          <tr class="clickable-row ${l.status === 'cancelled' ? 'row-cancelled' : ''}" data-id="${l.load_id}">
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
      const load = _allLoads.find(l => l.load_id == row.dataset.id);
      openDetailModal(load, mode);
    });
  });
}

function openDetailModal(load, mode) {
  const modal       = document.getElementById('modal');
  const isCancelled = load.status === 'cancelled';
  const canAdvance  = mode === 'manager' && !isCancelled && STATUS_ORDER.indexOf(load.status) < STATUS_ORDER.length - 1;
  const nextStatus  = canAdvance ? STATUS_ORDER[STATUS_ORDER.indexOf(load.status) + 1] : null;

  modal.innerHTML = `
    <div class="modal-header">
      <h3>Load ${load.load_number}</h3>
      <button class="modal-close" id="modal-close">&times;</button>
    </div>
    <div class="modal-body ${isCancelled ? 'load-cancelled' : ''}">
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

      ${!isCancelled && mode === 'manager' ? `
        <div class="modal-actions">
          ${canAdvance ? `<button class="btn-primary" id="btn-advance-status">Mark as ${cap(nextStatus)}</button>` : ''}
          <div class="status-menu-wrap">
            <button class="btn-icon" id="btn-status-menu" title="Options">&#9776;</button>
            <div class="status-menu hidden" id="status-menu">
              <button class="status-menu-item" id="btn-menu-edit">Edit Load</button>
              <button class="status-menu-item" id="btn-menu-invoice">Generate Invoice</button>
              <div class="status-menu-has-sub">
                <div class="status-menu-item">Change Status <span class="submenu-arrow">›</span></div>
                <div class="status-submenu">
                  ${ALL_STATUSES.filter(s => s !== load.status && s !== 'cancelled').map(s =>
                    `<button class="status-menu-item status-sub-item" data-status="${s}">${cap(s)}</button>`
                  ).join('')}
                </div>
              </div>
              <div class="status-menu-divider"></div>
              <button class="status-menu-item status-menu-danger" id="btn-menu-cancel">⚠ Cancel Load</button>
            </div>
          </div>
        </div>
      ` : ''}

      ${mode === 'trucker' && load.status === 'pending' ? `
        <div class="modal-actions">
          <button class="btn-primary" id="btn-trucker-complete">Mark as Complete</button>
        </div>
      ` : ''}
    </div>
  `;

  document.getElementById('modal-close').addEventListener('click', closeModal);

  if (canAdvance) {
    document.getElementById('btn-advance-status').addEventListener('click', () => {
      if (confirm(`Mark load ${load.load_number} as ${cap(nextStatus)}?`))
        updateStatus(load.load_id, nextStatus, mode, load);
    });
  }

  if (!isCancelled && mode === 'manager') {
    const menuBtn = document.getElementById('btn-status-menu');
    const menu    = document.getElementById('status-menu');

    menuBtn.addEventListener('click', e => {
      e.stopPropagation();
      menu.classList.toggle('hidden');
    });

    document.addEventListener('click', () => menu.classList.add('hidden'), { once: true });

    menu.querySelectorAll('.status-sub-item').forEach(btn => {
      btn.addEventListener('click', () => {
        const s = btn.dataset.status;
        if (confirm(`Mark load ${load.load_number} as ${cap(s)}?`))
          updateStatus(load.load_id, s, mode, load);
      });
    });

    document.getElementById('btn-menu-edit').addEventListener('click', () => {
      menu.classList.add('hidden');
      openEditLoadModal(load, mode);
    });

    document.getElementById('btn-menu-invoice').addEventListener('click', () => {
      menu.classList.add('hidden');
      generateInvoiceFromLoad(load);
    });

    document.getElementById('btn-menu-cancel').addEventListener('click', () => {
      if (confirm(`Cancel load ${load.load_number}? It will be kept for records.`))
        updateStatus(load.load_id, 'cancelled', mode, load);
    });
  }

  if (mode === 'trucker' && load.status === 'pending') {
    document.getElementById('btn-trucker-complete').addEventListener('click', () => {
      if (confirm(`Mark load ${load.load_number} as Complete?`))
        updateStatus(load.load_id, 'complete', mode, load);
    });
  }

  showModal();
}

async function updateStatus(loadId, status, mode, load) {
  await fetch(`/api/loads/${loadId}/status`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ status }),
  });
  closeModal();
  await loadTable(mode === 'trucker' ? 1 : null, mode);
}

async function generateInvoiceFromLoad(load) {
  const res  = await fetch('/api/invoices/generate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      loadId:      load.load_id,
      invoiceDate: new Date().toISOString().split('T')[0],
    }),
  });

  const data      = await res.json();
  const alreadyExisted = res.status === 409;

  closeModal();

  if (alreadyExisted) {
    const modal = document.getElementById('modal');
    modal.innerHTML = `
      <div class="modal-header">
        <h3>Invoice Already Exists</h3>
        <button class="modal-close" id="modal-close">&times;</button>
      </div>
      <div class="modal-body">
        <p style="margin-bottom:1.5rem">An invoice already exists for load ${load.load_number}. Want to open it?</p>
        <div class="modal-actions">
          <button class="btn-secondary" id="btn-stay">Stay Here</button>
          <button class="btn-primary" id="btn-go">Open Invoice</button>
        </div>
      </div>
    `;
    document.getElementById('modal-close').addEventListener('click', closeModal);
    document.getElementById('btn-stay').addEventListener('click', closeModal);
    document.getElementById('btn-go').addEventListener('click', () => {
      localStorage.setItem('openInvoiceId', data.invoice_id);
      window.location.hash = '#invoices';
    });
    showModal();
  } else {
    localStorage.setItem('openInvoiceId', data.invoice_id);
    window.location.hash = '#invoices';
  }
}

function loadFormFields(drivers, load = null) {
  const driverOptions = `
    <option value="">Unassigned</option>
    ${drivers.map(d => `<option value="${d.driver_id}" ${load?.driver_id == d.driver_id ? 'selected' : ''}>${d.name} (Unit ${d.unit_number})</option>`).join('')}
  `;

  return `
    <div class="form-grid">
      <div class="form-group">
        <label>Load Number</label>
        <input name="loadNumber" required placeholder="e.g. 1061465" value="${load?.load_number ?? ''}" />
      </div>
      <div class="form-group">
        <label>Ship Date</label>
        <input name="shipDate" type="date" required value="${load?.ship_date ?? ''}" />
      </div>
      <div class="form-group">
        <label>Origin</label>
        <input name="origin" required placeholder="e.g. Birmingham, AL" value="${load?.origin ?? ''}" />
      </div>
      <div class="form-group">
        <label>Destination</label>
        <input name="destination" required placeholder="e.g. Nashville, TN" value="${load?.destination ?? ''}" />
      </div>
      <div class="form-group">
        <label>Description</label>
        <input name="description" required placeholder="e.g. Steel Coil" value="${load?.description ?? ''}" />
      </div>
      <div class="form-group">
        <label>Driver</label>
        <select name="driverId">${driverOptions}</select>
      </div>
      <div class="form-group">
        <label>Line Haul Rate</label>
        <input name="lineHaulRate" type="number" step="0.01" required placeholder="0.00" value="${load?.line_haul_rate ?? ''}" />
      </div>
      <div class="form-group">
        <label>FSC Rate</label>
        <input name="fscRate" type="number" step="0.01" placeholder="0.00" value="${load?.fsc_rate ?? 0}" />
      </div>
      <div class="form-group">
        <label>Terms</label>
        <input name="terms" value="${load?.terms ?? 'Net 30'}" />
      </div>
      <div class="form-group">
        <label>Bill To Name</label>
        <input name="billToName" placeholder="Company name" value="${load?.bill_to_name ?? ''}" />
      </div>
      <div class="form-group">
        <label>Bill To Address</label>
        <input name="billToAddress" placeholder="Street, City, State" value="${load?.bill_to_address ?? ''}" />
      </div>
      <div class="form-group">
        <label>Consignee Name</label>
        <input name="consigneeName" placeholder="Company name" value="${load?.consignee_name ?? ''}" />
      </div>
      <div class="form-group">
        <label>Consignee Address</label>
        <input name="consigneeAddress" placeholder="Street, City, State" value="${load?.consignee_address ?? ''}" />
      </div>
    </div>
  `;
}

function formToBody(f) {
  return {
    loadNumber:       f.loadNumber.value,
    shipDate:         f.shipDate.value,
    origin:           f.origin.value,
    destination:      f.destination.value,
    description:      f.description.value,
    lineHaulRate:     parseFloat(f.lineHaulRate.value),
    fscRate:          parseFloat(f.fscRate.value) || 0,
    terms:            f.terms.value,
    status:           'pending',
    billToName:       f.billToName.value,
    billToAddress:    f.billToAddress.value,
    consigneeName:    f.consigneeName.value,
    consigneeAddress: f.consigneeAddress.value,
    driverId:         f.driverId.value ? parseInt(f.driverId.value) : null,
  };
}

async function openNewLoadModal() {
  const drivers = await fetch('/api/drivers').then(r => r.json());
  const modal   = document.getElementById('modal');

  modal.innerHTML = `
    <div class="modal-header">
      <h3>New Load</h3>
      <button class="modal-close" id="modal-close">&times;</button>
    </div>
    <div class="modal-body">
      <form id="load-form">
        ${loadFormFields(drivers)}
        <div class="modal-actions">
          <button type="button" class="btn-secondary" id="modal-close-btn">Cancel</button>
          <button type="submit" class="btn-primary">Create Load</button>
        </div>
      </form>
    </div>
  `;

  document.getElementById('modal-close').addEventListener('click', closeModal);
  document.getElementById('modal-close-btn').addEventListener('click', closeModal);

  document.getElementById('load-form').addEventListener('submit', async e => {
    e.preventDefault();
    await fetch('/api/loads', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(formToBody(e.target)),
    });
    closeModal();
    await loadTable(null, 'manager');
  });

  showModal();
}

async function openEditLoadModal(load, mode) {
  const drivers = await fetch('/api/drivers').then(r => r.json());
  const modal   = document.getElementById('modal');

  modal.innerHTML = `
    <div class="modal-header">
      <h3>Edit Load ${load.load_number}</h3>
      <button class="modal-close" id="modal-close">&times;</button>
    </div>
    <div class="modal-body">
      <form id="load-form">
        ${loadFormFields(drivers, load)}
        <div class="modal-actions">
          <button type="button" class="btn-secondary" id="modal-close-btn">Cancel</button>
          <button type="submit" class="btn-primary">Save Changes</button>
        </div>
      </form>
    </div>
  `;

  const goBack = () => openDetailModal(load, mode);
  document.getElementById('modal-close').addEventListener('click', goBack);
  document.getElementById('modal-close-btn').addEventListener('click', goBack);

  document.getElementById('load-form').addEventListener('submit', async e => {
    e.preventDefault();
    const body = formToBody(e.target);
    body.status = load.status;
    await fetch(`/api/loads/${load.load_id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    closeModal();
    await loadTable(mode === 'trucker' ? 1 : null, mode);
  });

  showModal();
}

const cap = s => s.charAt(0).toUpperCase() + s.slice(1);
function showModal()  { document.getElementById('modal-overlay').classList.remove('hidden'); }
function closeModal() { document.getElementById('modal-overlay').classList.add('hidden'); }

export async function renderAccounts(container) {
  container.innerHTML = `
    <div class="page">
      <div class="page-header">
        <h2>Accounts</h2>
      </div>
      <div class="tab-bar">
        <button class="tab-btn active" id="tab-employees">Employees</button>
        <button class="tab-btn"        id="tab-truckers">Truckers</button>
      </div>
      <div id="accounts-content"></div>
    </div>
    <div class="modal-overlay hidden" id="modal-overlay">
      <div class="modal" id="modal"></div>
    </div>
  `;

  document.getElementById('tab-employees').addEventListener('click', () => {
    setTab('employees');
    renderEmployees();
  });
  document.getElementById('tab-truckers').addEventListener('click', () => {
    setTab('truckers');
    renderTruckers();
  });
  document.getElementById('modal-overlay').addEventListener('click', e => {
    if (e.target === e.currentTarget) closeModal();
  });

  await renderEmployees();
}

function setTab(active) {
  document.getElementById('tab-employees').classList.toggle('active', active === 'employees');
  document.getElementById('tab-truckers').classList.toggle('active',  active === 'truckers');
}

// ── Employees ──────────────────────────────────────────────────────────────

async function renderEmployees() {
  const employees = await fetch('/api/accounts/employees').then(r => r.json());
  const wrap = document.getElementById('accounts-content');

  wrap.innerHTML = `
    <div class="accounts-toolbar">
      <button class="btn-primary" id="btn-new-employee">+ New Employee</button>
    </div>
    ${!employees.length ? '<p class="empty-state">No employee accounts yet.</p>' : `
      <table class="data-table">
        <thead>
          <tr><th>Name</th><th>Email</th><th>Phone</th><th class="actions-col"></th></tr>
        </thead>
        <tbody>
          ${employees.map(e => `
            <tr>
              <td>${e.first_name} ${e.last_name}</td>
              <td>${e.email}</td>
              <td>${e.phone}</td>
              <td class="actions-col">
                <div class="row-actions">
                  <button class="btn-secondary btn-edit-emp" data-id="${e.user_id}">Edit</button>
                  <button class="btn-danger   btn-del-emp"  data-id="${e.user_id}">Delete</button>
                </div>
              </td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    `}
  `;

  document.getElementById('btn-new-employee').addEventListener('click', () => openEmployeeModal(null, employees));

  wrap.querySelectorAll('.btn-edit-emp').forEach(btn => {
    const emp = employees.find(e => e.user_id == btn.dataset.id);
    btn.addEventListener('click', () => openEmployeeModal(emp, employees));
  });

  wrap.querySelectorAll('.btn-del-emp').forEach(btn => {
    btn.addEventListener('click', async () => {
      if (!confirm('Delete this employee account?')) return;
      await fetch(`/api/accounts/employees/${btn.dataset.id}`, { method: 'DELETE' });
      await renderEmployees();
    });
  });
}

function openEmployeeModal(employee, _employees) {
  const isEdit = !!employee;
  const modal  = document.getElementById('modal');

  modal.innerHTML = `
    <div class="modal-header">
      <h3>${isEdit ? 'Edit Employee' : 'New Employee'}</h3>
      <button class="modal-close" id="modal-close">&times;</button>
    </div>
    <div class="modal-body">
      <form id="account-form">
        <div class="form-grid">
          <div class="form-group">
            <label>First Name</label>
            <input name="firstName" required value="${employee?.first_name ?? ''}" />
          </div>
          <div class="form-group">
            <label>Last Name</label>
            <input name="lastName" required value="${employee?.last_name ?? ''}" />
          </div>
          <div class="form-group">
            <label>Email</label>
            <input name="email" required value="${employee?.email ?? ''}" />
          </div>
          <div class="form-group">
            <label>Phone</label>
            <input name="phone" type="tel" required value="${employee?.phone ?? ''}" />
          </div>
          <div class="form-group">
            <label>${isEdit ? 'New Password (leave blank to keep)' : 'Password'}</label>
            <input name="password" type="password" ${isEdit ? '' : 'required'} />
          </div>
        </div>
        <div class="modal-actions">
          <button type="button" class="btn-secondary" id="modal-close-btn">Cancel</button>
          <button type="submit" class="btn-primary">${isEdit ? 'Save Changes' : 'Create Employee'}</button>
        </div>
      </form>
    </div>
  `;

  document.getElementById('modal-close').addEventListener('click', closeModal);
  document.getElementById('modal-close-btn').addEventListener('click', closeModal);

  document.getElementById('account-form').addEventListener('submit', async e => {
    e.preventDefault();
    const f    = e.target;
    const body = {
      firstName: f.firstName.value,
      lastName:  f.lastName.value,
      email:     f.email.value,
      phone:     f.phone.value,
      password:  f.password.value || null,
    };

    const url    = isEdit ? `/api/accounts/employees/${employee.user_id}` : '/api/accounts/employees';
    const method = isEdit ? 'PUT' : 'POST';
    await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    closeModal();
    await renderEmployees();
  });

  showModal();
}

// ── Truckers ───────────────────────────────────────────────────────────────

async function renderTruckers() {
  const truckers = await fetch('/api/accounts/truckers').then(r => r.json());
  const wrap = document.getElementById('accounts-content');

  wrap.innerHTML = `
    <div class="accounts-toolbar">
      <button class="btn-primary" id="btn-new-trucker">+ New Trucker</button>
    </div>
    ${!truckers.length ? '<p class="empty-state">No trucker accounts yet.</p>' : `
      <table class="data-table">
        <thead>
          <tr><th>Name</th><th>Unit</th><th>Email</th><th>Phone</th><th>Commission</th><th class="actions-col"></th></tr>
        </thead>
        <tbody>
          ${truckers.map(t => `
            <tr>
              <td>${t.first_name} ${t.last_name}</td>
              <td class="mono">${t.unit_number}</td>
              <td>${t.email}</td>
              <td>${t.phone}</td>
              <td>${(Number(t.commission_rate) * 100).toFixed(0)}%</td>
              <td class="actions-col">
                <div class="row-actions">
                  <button class="btn-secondary btn-edit-trk" data-id="${t.user_id}">Edit</button>
                  <button class="btn-danger   btn-del-trk"  data-id="${t.user_id}">Delete</button>
                </div>
              </td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    `}
  `;

  document.getElementById('btn-new-trucker').addEventListener('click', () => openTruckerModal(null));

  wrap.querySelectorAll('.btn-edit-trk').forEach(btn => {
    const trk = truckers.find(t => t.user_id == btn.dataset.id);
    btn.addEventListener('click', () => openTruckerModal(trk));
  });

  wrap.querySelectorAll('.btn-del-trk').forEach(btn => {
    btn.addEventListener('click', async () => {
      if (!confirm('Delete this trucker account and their driver record? This cannot be undone.')) return;
      await fetch(`/api/accounts/truckers/${btn.dataset.id}`, { method: 'DELETE' });
      await renderTruckers();
    });
  });
}

function openTruckerModal(trucker) {
  const isEdit = !!trucker;
  const modal  = document.getElementById('modal');

  modal.innerHTML = `
    <div class="modal-header">
      <h3>${isEdit ? 'Edit Trucker' : 'New Trucker'}</h3>
      <button class="modal-close" id="modal-close">&times;</button>
    </div>
    <div class="modal-body">
      <form id="account-form">
        <div class="form-grid">
          <div class="form-group">
            <label>First Name</label>
            <input name="firstName" required value="${trucker?.first_name ?? ''}" />
          </div>
          <div class="form-group">
            <label>Last Name</label>
            <input name="lastName" required value="${trucker?.last_name ?? ''}" />
          </div>
          <div class="form-group">
            <label>Email</label>
            <input name="email" required value="${trucker?.email ?? ''}" />
          </div>
          <div class="form-group">
            <label>Phone</label>
            <input name="phone" type="tel" required value="${trucker?.phone ?? ''}" />
          </div>
          <div class="form-group">
            <label>${isEdit ? 'New Password (leave blank to keep)' : 'Password'}</label>
            <input name="password" type="password" ${isEdit ? '' : 'required'} />
          </div>
          <div class="form-group">
            <label>Unit Number</label>
            <input name="unitNumber" required placeholder="e.g. 1516" value="${trucker?.unit_number ?? ''}" />
          </div>
          <div class="form-group" style="grid-column:1/-1">
            <label>Address</label>
            <input name="address" required placeholder="Street, City, State" value="${trucker?.address ?? ''}" />
          </div>
          <div class="form-group">
            <label>Commission Rate</label>
            <input name="commissionRate" type="number" step="0.01" min="0" max="1" required
                   placeholder="0.18" value="${trucker ? Number(trucker.commission_rate).toFixed(2) : '0.18'}" />
          </div>
        </div>
        <div class="modal-actions">
          <button type="button" class="btn-secondary" id="modal-close-btn">Cancel</button>
          <button type="submit" class="btn-primary">${isEdit ? 'Save Changes' : 'Create Trucker'}</button>
        </div>
      </form>
    </div>
  `;

  document.getElementById('modal-close').addEventListener('click', closeModal);
  document.getElementById('modal-close-btn').addEventListener('click', closeModal);

  document.getElementById('account-form').addEventListener('submit', async e => {
    e.preventDefault();
    const f    = e.target;
    const body = {
      firstName:      f.firstName.value,
      lastName:       f.lastName.value,
      email:          f.email.value,
      phone:          f.phone.value,
      password:       f.password.value || null,
      unitNumber:     f.unitNumber.value,
      address:        f.address.value,
      commissionRate: parseFloat(f.commissionRate.value),
    };

    const url    = isEdit ? `/api/accounts/truckers/${trucker.user_id}` : '/api/accounts/truckers';
    const method = isEdit ? 'PUT' : 'POST';
    await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    closeModal();
    await renderTruckers();
  });

  showModal();
}

function showModal()  { document.getElementById('modal-overlay').classList.remove('hidden'); }
function closeModal() { document.getElementById('modal-overlay').classList.add('hidden'); }

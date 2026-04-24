import { renderLogin }    from './auth.js';
import { renderLoads }    from './loads.js';
import { renderInvoices } from './invoices.js';
import { renderDriverPay} from './driverPay.js';
import { renderAnalytics} from './analytics.js';
import { renderAccounts } from './accounts.js';

const app = document.getElementById('app');

export function getUser() {
  const raw = sessionStorage.getItem('auth');
  return raw ? JSON.parse(raw) : null;
}

export function getMode() {
  const user = getUser();
  if (!user) return 'employee';
  return user.role === 'trucker' ? 'trucker' : 'employee';
}

export function isLoggedIn() {
  return getUser() !== null;
}

export function login(user) {
  sessionStorage.setItem('auth', JSON.stringify(user));
  window.location.hash = '#loads';
  router();
}

export async function logout() {
  await fetch('/api/chat/reset', { method: 'POST' }).catch(() => {});
  sessionStorage.removeItem('auth');
  router();
}

function renderNav() {
  const user = getUser();
  const mode = getMode();
  const nav  = document.createElement('nav');
  nav.id = 'main-nav';

  const employeeLinks = `
    <a href="#loads"      class="${isActive('#loads')}">Loads</a>
    <a href="#invoices"   class="${isActive('#invoices')}">Invoices</a>
    <a href="#driver-pay" class="${isActive('#driver-pay')}">Driver Pay</a>
    <a href="#analytics"  class="${isActive('#analytics')}">Analytics</a>
    ${user?.role === 'admin' ? `<a href="#accounts" class="${isActive('#accounts')}">Accounts</a>` : ''}
  `;

  const truckerLinks = `
    <a href="#loads"      class="${isActive('#loads')}">My Loads</a>
    <a href="#driver-pay" class="${isActive('#driver-pay')}">My Pay</a>
  `;

  const displayName = user?.role === 'admin'
    ? 'Admin'
    : `${user?.firstName ?? ''} ${user?.lastName ?? ''}`.trim() || user?.email || '';

  nav.innerHTML = `
    <div class="nav-left">
      <span class="nav-brand">Trucking Ops</span>
    </div>
    <div class="nav-links">
      ${mode === 'employee' ? employeeLinks : truckerLinks}
    </div>
    <div class="nav-right">
      <span class="nav-user">${displayName}</span>
      <button class="btn-nav-logout" id="btn-logout">Logout</button>
    </div>
  `;

  nav.querySelector('#btn-logout').addEventListener('click', logout);
  return nav;
}

function isActive(hash) {
  return window.location.hash === hash ? 'active' : '';
}

function router() {
  app.innerHTML = '';

  const hash = window.location.hash || '#loads';

  if (!isLoggedIn()) {
    renderLogin(app);
    return;
  }

  app.appendChild(renderNav());

  const content = document.createElement('div');
  content.id = 'content';
  app.appendChild(content);

  const user = getUser();
  const mode = getMode();

  // Truckers can only access loads and driver-pay
  const truckerOnly = ['#loads', '#driver-pay'];
  if (mode === 'trucker' && !truckerOnly.includes(hash)) {
    window.location.hash = '#loads';
    return;
  }

  // Accounts page is admin-only
  if (hash === '#accounts' && user?.role !== 'admin') {
    window.location.hash = '#loads';
    return;
  }

  switch (hash) {
    case '#loads':      renderLoads(content, mode);      break;
    case '#invoices':   renderInvoices(content, mode);   break;
    case '#driver-pay': renderDriverPay(content, mode);  break;
    case '#analytics':  renderAnalytics(content, mode);  break;
    case '#accounts':   renderAccounts(content);         break;
    default:            renderLoads(content, mode);
  }
}

window.addEventListener('hashchange', router);
window.addEventListener('load', router);

export function downloadCSV(rows, filename) {
  const csv = rows.map(r => r.map(c => `"${String(c ?? '').replace(/"/g, '""')}"`).join(',')).join('\n');
  const a = Object.assign(document.createElement('a'), {
    href: URL.createObjectURL(new Blob([csv], { type: 'text/csv' })),
    download: filename,
  });
  a.click();
  URL.revokeObjectURL(a.href);
}

import { renderLogin } from './auth.js';
import { renderLoads } from './loads.js';
import { renderInvoices } from './invoices.js';
import { renderDriverPay } from './driverPay.js';
import { renderAnalytics } from './analytics.js';

const app = document.getElementById('app');

export function getMode() {
  return localStorage.getItem('mode') || 'manager';
}

export function setMode(mode) {
  localStorage.setItem('mode', mode);
  const managerOnly = ['#invoices', '#analytics'];
  if (mode === 'trucker' && managerOnly.includes(window.location.hash)) {
    window.location.hash = '#loads';
  } else {
    router();
  }
}

export function isLoggedIn() {
  return localStorage.getItem('loggedIn') === 'true';
}

export function login() {
  localStorage.setItem('loggedIn', 'true');
  window.location.hash = '#loads';
}

function renderNav() {
  const mode = getMode();
  const nav = document.createElement('nav');
  nav.id = 'main-nav';

  const managerLinks = `
    <a href="#loads" class="${isActive('#loads')}">Loads</a>
    <a href="#invoices" class="${isActive('#invoices')}">Invoices</a>
    <a href="#driver-pay" class="${isActive('#driver-pay')}">Driver Pay</a>
    <a href="#analytics" class="${isActive('#analytics')}">Analytics</a>
  `;

  const truckerLinks = `
    <a href="#loads" class="${isActive('#loads')}">My Loads</a>
    <a href="#driver-pay" class="${isActive('#driver-pay')}">My Pay</a>
  `;

  nav.innerHTML = `
    <div class="nav-left">
      <span class="nav-brand">Trucking Ops</span>
    </div>
    <div class="nav-links">
      ${mode === 'manager' ? managerLinks : truckerLinks}
    </div>
    <div class="nav-right">
      <div class="mode-toggle">
        <button class="toggle-btn ${mode === 'manager' ? 'active' : ''}" data-mode="manager">Manager</button>
        <button class="toggle-btn ${mode === 'trucker' ? 'active' : ''}" data-mode="trucker">Trucker</button>
      </div>
    </div>
  `;

  nav.querySelectorAll('.toggle-btn').forEach(btn => {
    btn.addEventListener('click', () => setMode(btn.dataset.mode));
  });

  return nav;
}

function isActive(hash) {
  return window.location.hash === hash ? 'active' : '';
}

function router() {
  app.innerHTML = '';

  const hash = window.location.hash || '#login';

  if (!isLoggedIn()) {
    renderLogin(app);
    return;
  }

  app.appendChild(renderNav());

  const content = document.createElement('div');
  content.id = 'content';
  app.appendChild(content);

  const mode = getMode();
  const managerOnly = ['#invoices', '#analytics'];

  if (mode === 'trucker' && managerOnly.includes(hash)) {
    window.location.hash = '#loads';
    return;
  }

  switch (hash) {
    case '#loads':
      renderLoads(content, mode);
      break;
    case '#invoices':
      renderInvoices(content, mode);
      break;
    case '#driver-pay':
      renderDriverPay(content, mode);
      break;
    case '#analytics':
      renderAnalytics(content, mode);
      break;
    default:
      renderLoads(content, mode);
  }
}

window.addEventListener('hashchange', router);
window.addEventListener('load', router);

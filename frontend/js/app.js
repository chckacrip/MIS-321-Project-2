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
  router();
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
  nav.innerHTML = `
    <div class="nav-left">
      <span class="nav-brand">Trucking Ops</span>
    </div>
    <div class="nav-links">
      <a href="#loads" class="${isActive('#loads')}">Loads</a>
      <a href="#invoices" class="${isActive('#invoices')}">Invoices</a>
      <a href="#driver-pay" class="${isActive('#driver-pay')}">Driver Pay</a>
      <a href="#analytics" class="${isActive('#analytics')}">Analytics</a>
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

  switch (hash) {
    case '#loads':
      renderLoads(content, getMode());
      break;
    case '#invoices':
      renderInvoices(content, getMode());
      break;
    case '#driver-pay':
      renderDriverPay(content, getMode());
      break;
    case '#analytics':
      renderAnalytics(content, getMode());
      break;
    default:
      renderLoads(content, getMode());
  }
}

window.addEventListener('hashchange', router);
window.addEventListener('load', router);

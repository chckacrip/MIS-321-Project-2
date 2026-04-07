import { renderLogin } from './auth.js';

const app = document.getElementById('app');

// Simple hash-based router
function router() {
  const route = window.location.hash || '#login';

  switch (route) {
    case '#login':
      renderLogin(app);
      break;
    default:
      renderLogin(app);
  }
}

window.addEventListener('hashchange', router);
window.addEventListener('load', router);

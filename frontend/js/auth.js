import { login } from './app.js';

export function renderLogin(app) {
  let loginType = 'employee';

  function render() {
    app.innerHTML = `
      <div class="login-container">
        <h1>Trucking Operations</h1>
        <p class="login-subtitle">${loginType === 'employee' ? 'Employee / Admin Sign In' : 'Trucker Sign In'}</p>
        <form id="login-form">
          <input type="text" id="login-email" placeholder="Email" required autocomplete="username" />
          <input type="password" id="login-password" placeholder="Password" required />
          <p id="login-error" class="login-error hidden"></p>
          <button type="submit">Sign In</button>
        </form>
        <p class="login-switch">
          ${loginType === 'employee'
            ? '<a href="#" id="switch-login">Sign in as a trucker →</a>'
            : '<a href="#" id="switch-login">← Employee / Admin login</a>'}
        </p>
      </div>
    `;

    document.getElementById('switch-login').addEventListener('click', e => {
      e.preventDefault();
      loginType = loginType === 'employee' ? 'trucker' : 'employee';
      render();
    });

    document.getElementById('login-form').addEventListener('submit', async e => {
      e.preventDefault();
      const email    = document.getElementById('login-email').value.trim();
      const password = document.getElementById('login-password').value;
      const errorEl  = document.getElementById('login-error');

      errorEl.classList.add('hidden');

      try {
        const res = await fetch('/api/auth/login', {
          method:  'POST',
          headers: { 'Content-Type': 'application/json' },
          body:    JSON.stringify({ email, password, expectedRole: loginType }),
        });

        if (!res.ok) {
          const data = await res.json().catch(() => ({}));
          errorEl.textContent = data.message || 'Invalid email or password.';
          errorEl.classList.remove('hidden');
          return;
        }

        login(await res.json());
      } catch {
        errorEl.textContent = 'Could not connect to server.';
        errorEl.classList.remove('hidden');
      }
    });
  }

  render();
}

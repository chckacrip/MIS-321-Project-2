export function renderLogin(app) {
  app.innerHTML = `
    <div class="login-container">
      <h1>Trucking Operations</h1>
      <form id="login-form">
        <input type="text" id="username" placeholder="Username" required />
        <input type="password" id="password" placeholder="Password" required />
        <button type="submit">Sign In</button>
      </form>
    </div>
  `;

  document.getElementById('login-form').addEventListener('submit', handleLogin);
}

function handleLogin(e) {
  e.preventDefault();
  // TODO: replace with Cognito auth
  const username = document.getElementById('username').value;
  const password = document.getElementById('password').value;
  console.log('Login attempt:', username);
}

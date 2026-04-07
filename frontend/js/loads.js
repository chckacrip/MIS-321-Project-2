export function renderLoads(container, mode) {
  container.innerHTML = `
    <div class="page">
      <div class="page-header">
        <h2>Loads</h2>
        ${mode === 'manager' ? '<button class="btn-primary">+ New Load</button>' : ''}
      </div>
      <div class="page-description">
        <p>Track all freight loads from pickup to delivery. Each load contains the origin, destination,
        cargo description, line haul rate, and fuel surcharge. Managers can create and edit loads,
        assign drivers, and update load status (pending → complete → invoiced → paid).
        Truckers can view their own assigned loads only.</p>
      </div>
    </div>
  `;
}

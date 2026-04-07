export function renderAnalytics(container, mode) {
  container.innerHTML = `
    <div class="page">
      <div class="page-header">
        <h2>Analytics</h2>
      </div>
      <div class="page-description">
        <p>Visual reports and insights derived from your load and pay data — no extra data entry required.
        View revenue broken down by route, driver, or time period. Analyze fuel spend by route or driver.
        Get a basic profitability overview comparing revenue against total costs. Managers also have access
        to an AI chat interface to ask plain-English questions about the data without running manual reports.</p>
      </div>
    </div>
  `;
}

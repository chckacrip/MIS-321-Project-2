export function renderDriverPay(container, mode) {
  container.innerHTML = `
    <div class="page">
      <div class="page-header">
        <h2>Driver Pay</h2>
        ${mode === 'manager' ? '<button class="btn-primary">+ Generate Pay Summary</button>' : ''}
      </div>
      <div class="page-description">
        <p>Calculate and export weekly driver pay summaries. Pay is calculated from line haul earnings
        minus the company commission (default 18%), plus 100% of the fuel surcharge, minus any advances
        (fuel, EzPass, cash), insurance, and workers' comp deductions. Managers can generate pay sheets
        for any driver and export them. Truckers can view their own pay summaries only.</p>
      </div>
    </div>
  `;
}

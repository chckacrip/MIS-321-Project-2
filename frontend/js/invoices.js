export function renderInvoices(container, mode) {
  container.innerHTML = `
    <div class="page">
      <div class="page-header">
        <h2>Invoices</h2>
        ${mode === 'manager' ? '<button class="btn-primary">+ Generate Invoice</button>' : ''}
      </div>
      <div class="page-description">
        <p>Auto-generate invoices directly from load data. Once a load is marked complete, managers
        can generate an invoice that pulls the bill-to details, consignee, rate, terms, and load
        information automatically. Invoices can be exported to PDF in the standard company format
        and tracked as unpaid or paid. Truckers do not have access to this module.</p>
      </div>
    </div>
  `;
}

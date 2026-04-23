using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TruckingApi.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly string _conn;

    public InvoicesController(DatabaseOptions db) => _conn = db.ConnectionString;

    [HttpGet]
    public async Task<IActionResult> GetInvoices()
    {
        var sql = """
            SELECT i.invoice_id, i.invoice_number, i.invoice_date, i.due_date,
                   i.payment_status, i.paid_date,
                   l.load_id, l.load_number, l.origin, l.destination,
                   l.line_haul_rate, l.fsc_rate, l.tarp_rate, l.extra_fee, l.terms,
                   l.bill_to_name, l.bill_to_address
            FROM invoices i
            JOIN loads l ON i.load_id = l.load_id
            ORDER BY i.invoice_date DESC
        """;
        return Content(await Database.ExecuteSql(_conn, sql), "application/json");
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetInvoice(int id)
    {
        var sql = """
            SELECT i.*, l.load_number, l.ship_date, l.origin, l.destination,
                   l.description, l.line_haul_rate, l.fsc_rate, l.tarp_rate, l.extra_fee, l.terms,
                   l.bill_to_name, l.bill_to_address,
                   l.consignee_name, l.consignee_address,
                   d.unit_number
            FROM invoices i
            JOIN loads l ON i.load_id = l.load_id
            LEFT JOIN drivers d ON l.driver_id = d.driver_id
            WHERE i.invoice_id = @id
        """;

        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync()) return NotFound();

        var row = new Dictionary<string, object?>();
        for (int i = 0; i < reader.FieldCount; i++)
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);

        return Ok(row);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateInvoiceRequest body)
    {
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();

        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT invoice_id FROM invoices WHERE load_id = @loadId";
        checkCmd.Parameters.AddWithValue("@loadId", body.LoadId);
        var existing = await checkCmd.ExecuteScalarAsync();
        if (existing != null)
            return Conflict(new { error = "Invoice already exists for this load.", invoice_id = Convert.ToInt32(existing) });

        await using var numCmd = conn.CreateCommand();
        numCmd.CommandText = "SELECT COALESCE(MAX(CAST(invoice_number AS UNSIGNED)), 101630) + 1 FROM invoices";
        var invoiceNumber = (await numCmd.ExecuteScalarAsync())!.ToString();

        var invoiceDate = body.InvoiceDate ?? DateTime.Today.ToString("yyyy-MM-dd");
        var dueDate = DateTime.Parse(invoiceDate).AddDays(30).ToString("yyyy-MM-dd");

        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO invoices (invoice_number, load_id, invoice_date, due_date, payment_status)
            VALUES (@num, @loadId, @date, @due, 'unpaid')
        """;
        insertCmd.Parameters.AddWithValue("@num",    invoiceNumber);
        insertCmd.Parameters.AddWithValue("@loadId", body.LoadId);
        insertCmd.Parameters.AddWithValue("@date",   invoiceDate);
        insertCmd.Parameters.AddWithValue("@due",    dueDate);

        await insertCmd.ExecuteNonQueryAsync();
        var newId = (int)insertCmd.LastInsertedId;

        await using var statusCmd = conn.CreateCommand();
        statusCmd.CommandText = "UPDATE loads SET status = 'invoiced' WHERE load_id = @loadId";
        statusCmd.Parameters.AddWithValue("@loadId", body.LoadId);
        await statusCmd.ExecuteNonQueryAsync();

        return Ok(new { invoice_id = newId, invoice_number = invoiceNumber });
    }

    [HttpPatch("{id:int}/mark-paid")]
    public async Task<IActionResult> MarkPaid(int id)
    {
        var paidDate = DateTime.Today.ToString("yyyy-MM-dd");

        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE invoices SET payment_status = 'paid', paid_date = @paidDate
            WHERE invoice_id = @id
        """;
        cmd.Parameters.AddWithValue("@paidDate", paidDate);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();

        await using var loadCmd = conn.CreateCommand();
        loadCmd.CommandText = "UPDATE loads SET status = 'paid' WHERE load_id = (SELECT load_id FROM invoices WHERE invoice_id = @id)";
        loadCmd.Parameters.AddWithValue("@id", id);
        await loadCmd.ExecuteNonQueryAsync();

        return Ok(new { paid_date = paidDate });
    }
}

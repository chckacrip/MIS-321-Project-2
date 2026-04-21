using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace TruckingApi.Controllers;

[ApiController]
[Route("api/driver-pay")]
public class DriverPayController : ControllerBase
{
    private readonly string _conn;

    public DriverPayController(DatabaseOptions db) => _conn = db.ConnectionString;

    [HttpGet]
    public async Task<IActionResult> GetSummaries([FromQuery] int? driverId)
    {
        var where = driverId.HasValue ? "WHERE s.driver_id = @driverId" : "";
        var sql = $"""
            SELECT s.summary_id, s.driver_id, d.first_name || ' ' || d.last_name AS driver_name,
                   d.unit_number, s.pay_period_start, s.pay_period_end,
                   s.total_line_haul, s.commission_rate, s.total_fsc,
                   s.total_advances, s.insurance_deduction, s.workers_comp_deduction, s.net_pay,
                   s.created_at
            FROM driver_pay_summaries s
            JOIN drivers d ON s.driver_id = d.driver_id
            {where}
            ORDER BY s.pay_period_end DESC
        """;

        await using var conn = new SqliteConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (driverId.HasValue) cmd.Parameters.AddWithValue("@driverId", driverId.Value);
        await using var reader = await cmd.ExecuteReaderAsync();

        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return Ok(rows);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetSummary(int id)
    {
        await using var conn = new SqliteConnection(_conn);
        await conn.OpenAsync();

        await using var sumCmd = conn.CreateCommand();
        sumCmd.CommandText = """
            SELECT s.*, d.first_name || ' ' || d.last_name AS driver_name,
                   d.unit_number, d.address
            FROM driver_pay_summaries s
            JOIN drivers d ON s.driver_id = d.driver_id
            WHERE s.summary_id = @id
        """;
        sumCmd.Parameters.AddWithValue("@id", id);
        await using var sumReader = await sumCmd.ExecuteReaderAsync();
        if (!await sumReader.ReadAsync()) return NotFound();

        var summary = new Dictionary<string, object?>();
        for (int i = 0; i < sumReader.FieldCount; i++)
            summary[sumReader.GetName(i)] = sumReader.IsDBNull(i) ? null : sumReader.GetValue(i);
        await sumReader.CloseAsync();

        await using var loadsCmd = conn.CreateCommand();
        loadsCmd.CommandText = """
            SELECT l.load_id, l.load_number, l.ship_date,
                   l.origin || ' → ' || l.destination AS route,
                   l.line_haul_rate, l.fsc_rate
            FROM loads l
            JOIN load_drivers ld ON l.load_id = ld.load_id
            WHERE ld.driver_id = @driverId
              AND l.ship_date BETWEEN @start AND @end
            ORDER BY l.ship_date
        """;
        loadsCmd.Parameters.AddWithValue("@driverId", summary["driver_id"]);
        loadsCmd.Parameters.AddWithValue("@start", summary["pay_period_start"]);
        loadsCmd.Parameters.AddWithValue("@end", summary["pay_period_end"]);
        await using var loadsReader = await loadsCmd.ExecuteReaderAsync();

        var loads = new List<Dictionary<string, object?>>();
        while (await loadsReader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < loadsReader.FieldCount; i++)
                row[loadsReader.GetName(i)] = loadsReader.IsDBNull(i) ? null : loadsReader.GetValue(i);
            loads.Add(row);
        }
        await loadsReader.CloseAsync();

        await using var advCmd = conn.CreateCommand();
        advCmd.CommandText = """
            SELECT advance_id, advance_date, advance_type, amount, notes
            FROM driver_advances
            WHERE driver_id = @driverId
              AND advance_date BETWEEN @start AND @end
            ORDER BY advance_date
        """;
        advCmd.Parameters.AddWithValue("@driverId", summary["driver_id"]);
        advCmd.Parameters.AddWithValue("@start", summary["pay_period_start"]);
        advCmd.Parameters.AddWithValue("@end", summary["pay_period_end"]);
        await using var advReader = await advCmd.ExecuteReaderAsync();

        var advances = new List<Dictionary<string, object?>>();
        while (await advReader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < advReader.FieldCount; i++)
                row[advReader.GetName(i)] = advReader.IsDBNull(i) ? null : advReader.GetValue(i);
            advances.Add(row);
        }

        return Ok(new { summary, loads, advances });
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GeneratePayRequest body)
    {
        await using var conn = new SqliteConnection(_conn);
        await conn.OpenAsync();

        await using var driverCmd = conn.CreateCommand();
        driverCmd.CommandText = "SELECT commission_rate FROM drivers WHERE driver_id = @id";
        driverCmd.Parameters.AddWithValue("@id", body.DriverId);
        var commissionRate = Convert.ToDecimal(await driverCmd.ExecuteScalarAsync());

        await using var loadsCmd = conn.CreateCommand();
        loadsCmd.CommandText = """
            SELECT COALESCE(SUM(l.line_haul_rate), 0), COALESCE(SUM(l.fsc_rate), 0)
            FROM loads l
            JOIN load_drivers ld ON l.load_id = ld.load_id
            WHERE ld.driver_id = @driverId
              AND l.ship_date BETWEEN @start AND @end
        """;
        loadsCmd.Parameters.AddWithValue("@driverId", body.DriverId);
        loadsCmd.Parameters.AddWithValue("@start", body.PayPeriodStart);
        loadsCmd.Parameters.AddWithValue("@end", body.PayPeriodEnd);
        await using var loadsReader = await loadsCmd.ExecuteReaderAsync();
        await loadsReader.ReadAsync();
        var totalLineHaul = loadsReader.GetDecimal(0);
        var totalFsc = loadsReader.GetDecimal(1);
        await loadsReader.CloseAsync();

        await using var advCmd = conn.CreateCommand();
        advCmd.CommandText = """
            SELECT COALESCE(SUM(amount), 0)
            FROM driver_advances
            WHERE driver_id = @driverId
              AND advance_date BETWEEN @start AND @end
              AND advance_type NOT IN ('Insurance', 'WorkersComp')
        """;
        advCmd.Parameters.AddWithValue("@driverId", body.DriverId);
        advCmd.Parameters.AddWithValue("@start", body.PayPeriodStart);
        advCmd.Parameters.AddWithValue("@end", body.PayPeriodEnd);
        var totalAdvances = Convert.ToDecimal(await advCmd.ExecuteScalarAsync());

        var grossPay = (totalLineHaul * (1 - commissionRate)) + totalFsc;
        var netPay = grossPay - totalAdvances - body.InsuranceDeduction - body.WorkersCompDeduction;

        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO driver_pay_summaries
                (driver_id, pay_period_start, pay_period_end, total_line_haul, commission_rate,
                 total_fsc, total_advances, insurance_deduction, workers_comp_deduction, net_pay)
            VALUES (@driverId, @start, @end, @lh, @rate, @fsc, @adv, @ins, @wc, @net);
            SELECT last_insert_rowid();
        """;
        insertCmd.Parameters.AddWithValue("@driverId", body.DriverId);
        insertCmd.Parameters.AddWithValue("@start",    body.PayPeriodStart);
        insertCmd.Parameters.AddWithValue("@end",      body.PayPeriodEnd);
        insertCmd.Parameters.AddWithValue("@lh",       totalLineHaul);
        insertCmd.Parameters.AddWithValue("@rate",     commissionRate);
        insertCmd.Parameters.AddWithValue("@fsc",      totalFsc);
        insertCmd.Parameters.AddWithValue("@adv",      totalAdvances);
        insertCmd.Parameters.AddWithValue("@ins",      body.InsuranceDeduction);
        insertCmd.Parameters.AddWithValue("@wc",       body.WorkersCompDeduction);
        insertCmd.Parameters.AddWithValue("@net",      netPay);

        var newId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
        return Ok(new { summary_id = newId, net_pay = netPay });
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace TruckingApi.Controllers;

[ApiController]
[Route("api/loads")]
public class LoadsController : ControllerBase
{
    private readonly string _conn;

    public LoadsController(DatabaseOptions db) => _conn = db.ConnectionString;

    [HttpGet]
    public async Task<IActionResult> GetLoads([FromQuery] int? driverId)
    {
        string sql = driverId.HasValue
            ? """
                SELECT l.load_id, l.load_number, l.ship_date, l.origin, l.destination,
                       l.description, l.line_haul_rate, l.fsc_rate, l.terms, l.status,
                       l.bill_to_name, l.bill_to_address, l.consignee_name, l.consignee_address,
                       d.first_name || ' ' || d.last_name AS driver, d.driver_id, d.unit_number
                FROM loads l
                JOIN load_drivers ld ON l.load_id = ld.load_id
                JOIN drivers d ON ld.driver_id = d.driver_id
                WHERE ld.driver_id = @driverId
                ORDER BY l.ship_date DESC
              """
            : """
                SELECT l.load_id, l.load_number, l.ship_date, l.origin, l.destination,
                       l.description, l.line_haul_rate, l.fsc_rate, l.terms, l.status,
                       l.bill_to_name, l.bill_to_address, l.consignee_name, l.consignee_address,
                       GROUP_CONCAT(d.first_name || ' ' || d.last_name) AS driver,
                       GROUP_CONCAT(d.driver_id) AS driver_id,
                       GROUP_CONCAT(d.unit_number) AS unit_number
                FROM loads l
                LEFT JOIN load_drivers ld ON l.load_id = ld.load_id
                LEFT JOIN drivers d ON ld.driver_id = d.driver_id
                GROUP BY l.load_id
                ORDER BY l.ship_date DESC
              """;

        if (driverId.HasValue)
        {
            await using var conn = new SqliteConnection(_conn);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@driverId", driverId.Value);
            await using var reader = await cmd.ExecuteReaderAsync();
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            return Content(System.Text.Json.JsonSerializer.Serialize(rows), "application/json");
        }

        return Content(await Database.ExecuteSql(_conn, sql), "application/json");
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetLoad(int id)
    {
        var sql = """
            SELECT l.load_id, l.load_number, l.ship_date, l.origin, l.destination,
                   l.description, l.line_haul_rate, l.fsc_rate, l.terms, l.status,
                   l.bill_to_name, l.bill_to_address, l.consignee_name, l.consignee_address,
                   GROUP_CONCAT(d.first_name || ' ' || d.last_name) AS driver,
                   GROUP_CONCAT(d.driver_id) AS driver_id,
                   GROUP_CONCAT(d.unit_number) AS unit_number
            FROM loads l
            LEFT JOIN load_drivers ld ON l.load_id = ld.load_id
            LEFT JOIN drivers d ON ld.driver_id = d.driver_id
            WHERE l.load_id = @id
            GROUP BY l.load_id
        """;

        await using var conn = new SqliteConnection(_conn);
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

    [HttpPost]
    public async Task<IActionResult> CreateLoad([FromBody] CreateLoadRequest body)
    {
        await using var conn = new SqliteConnection(_conn);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO loads (load_number, ship_date, origin, destination, description,
                line_haul_rate, fsc_rate, terms, status, bill_to_name, bill_to_address,
                consignee_name, consignee_address)
            VALUES (@num, @date, @orig, @dest, @desc, @lh, @fsc, @terms, @status,
                @btn, @bta, @cn, @ca);
            SELECT last_insert_rowid();
        """;
        cmd.Parameters.AddWithValue("@num",    body.LoadNumber);
        cmd.Parameters.AddWithValue("@date",   body.ShipDate);
        cmd.Parameters.AddWithValue("@orig",   body.Origin);
        cmd.Parameters.AddWithValue("@dest",   body.Destination);
        cmd.Parameters.AddWithValue("@desc",   body.Description);
        cmd.Parameters.AddWithValue("@lh",     body.LineHaulRate);
        cmd.Parameters.AddWithValue("@fsc",    body.FscRate);
        cmd.Parameters.AddWithValue("@terms",  body.Terms);
        cmd.Parameters.AddWithValue("@status", body.Status);
        cmd.Parameters.AddWithValue("@btn",    body.BillToName);
        cmd.Parameters.AddWithValue("@bta",    body.BillToAddress);
        cmd.Parameters.AddWithValue("@cn",     body.ConsigneeName);
        cmd.Parameters.AddWithValue("@ca",     body.ConsigneeAddress);

        var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        if (body.DriverId.HasValue)
        {
            await using var ldCmd = conn.CreateCommand();
            ldCmd.CommandText = "INSERT OR IGNORE INTO load_drivers (load_id, driver_id) VALUES (@lid, @did)";
            ldCmd.Parameters.AddWithValue("@lid", newId);
            ldCmd.Parameters.AddWithValue("@did", body.DriverId.Value);
            await ldCmd.ExecuteNonQueryAsync();
        }

        return Ok(new { load_id = newId });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateLoad(int id, [FromBody] CreateLoadRequest body)
    {
        await using var conn = new SqliteConnection(_conn);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE loads SET
                load_number = @num, ship_date = @date, origin = @orig,
                destination = @dest, description = @desc, line_haul_rate = @lh,
                fsc_rate = @fsc, terms = @terms, bill_to_name = @btn,
                bill_to_address = @bta, consignee_name = @cn, consignee_address = @ca
            WHERE load_id = @id
        """;
        cmd.Parameters.AddWithValue("@num",  body.LoadNumber);
        cmd.Parameters.AddWithValue("@date", body.ShipDate);
        cmd.Parameters.AddWithValue("@orig", body.Origin);
        cmd.Parameters.AddWithValue("@dest", body.Destination);
        cmd.Parameters.AddWithValue("@desc", body.Description);
        cmd.Parameters.AddWithValue("@lh",   body.LineHaulRate);
        cmd.Parameters.AddWithValue("@fsc",  body.FscRate);
        cmd.Parameters.AddWithValue("@terms",body.Terms);
        cmd.Parameters.AddWithValue("@btn",  body.BillToName);
        cmd.Parameters.AddWithValue("@bta",  body.BillToAddress);
        cmd.Parameters.AddWithValue("@cn",   body.ConsigneeName);
        cmd.Parameters.AddWithValue("@ca",   body.ConsigneeAddress);
        cmd.Parameters.AddWithValue("@id",   id);
        await cmd.ExecuteNonQueryAsync();

        await using var delCmd = conn.CreateCommand();
        delCmd.CommandText = "DELETE FROM load_drivers WHERE load_id = @id";
        delCmd.Parameters.AddWithValue("@id", id);
        await delCmd.ExecuteNonQueryAsync();

        if (body.DriverId.HasValue)
        {
            await using var ldCmd = conn.CreateCommand();
            ldCmd.CommandText = "INSERT INTO load_drivers (load_id, driver_id) VALUES (@lid, @did)";
            ldCmd.Parameters.AddWithValue("@lid", id);
            ldCmd.Parameters.AddWithValue("@did", body.DriverId.Value);
            await ldCmd.ExecuteNonQueryAsync();
        }

        return Ok();
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest body)
    {
        var validStatuses = new[] { "pending", "complete", "invoiced", "paid", "cancelled" };
        if (!validStatuses.Contains(body.Status))
            return BadRequest(new { error = "Invalid status" });

        await using var conn = new SqliteConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE loads SET status = @status WHERE load_id = @id";
        cmd.Parameters.AddWithValue("@status", body.Status);
        cmd.Parameters.AddWithValue("@id",     id);
        await cmd.ExecuteNonQueryAsync();

        return Ok();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteLoad(int id)
    {
        await using var conn = new SqliteConnection(_conn);
        await conn.OpenAsync();

        await using var ldCmd = conn.CreateCommand();
        ldCmd.CommandText = "DELETE FROM load_drivers WHERE load_id = @id";
        ldCmd.Parameters.AddWithValue("@id", id);
        await ldCmd.ExecuteNonQueryAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM loads WHERE load_id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();

        return NoContent();
    }
}

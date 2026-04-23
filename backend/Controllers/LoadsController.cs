using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

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
                       l.description, l.line_haul_rate, l.fsc_rate, l.tarp_rate, l.extra_fee, l.terms, l.status,
                       l.bill_to_name, l.bill_to_address, l.consignee_name, l.consignee_address,
                       CONCAT(d.first_name, ' ', d.last_name) AS driver, d.driver_id, d.unit_number
                FROM loads l
                JOIN drivers d ON l.driver_id = d.driver_id
                WHERE l.driver_id = @driverId
                ORDER BY l.ship_date DESC
              """
            : """
                SELECT l.load_id, l.load_number, l.ship_date, l.origin, l.destination,
                       l.description, l.line_haul_rate, l.fsc_rate, l.tarp_rate, l.extra_fee, l.terms, l.status,
                       l.bill_to_name, l.bill_to_address, l.consignee_name, l.consignee_address,
                       CONCAT(d.first_name, ' ', d.last_name) AS driver,
                       d.driver_id, d.unit_number
                FROM loads l
                LEFT JOIN drivers d ON l.driver_id = d.driver_id
                ORDER BY l.ship_date DESC
              """;

        if (driverId.HasValue)
        {
            await using var conn = new MySqlConnection(_conn);
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
                   CONCAT(d.first_name, ' ', d.last_name) AS driver,
                   d.driver_id, d.unit_number
            FROM loads l
            LEFT JOIN drivers d ON l.driver_id = d.driver_id
            WHERE l.load_id = @id
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

    [HttpPost]
    public async Task<IActionResult> CreateLoad([FromBody] CreateLoadRequest body)
    {
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO loads (load_number, ship_date, origin, destination, description,
                line_haul_rate, fsc_rate, tarp_rate, extra_fee, terms, status,
                bill_to_name, bill_to_address, consignee_name, consignee_address, driver_id)
            VALUES (@num, @date, @orig, @dest, @desc, @lh, @fsc, @tarp, @extra, @terms, @status,
                @btn, @bta, @cn, @ca, @driverId)
        """;
        cmd.Parameters.AddWithValue("@num",      body.LoadNumber);
        cmd.Parameters.AddWithValue("@date",     body.ShipDate);
        cmd.Parameters.AddWithValue("@orig",     body.Origin);
        cmd.Parameters.AddWithValue("@dest",     body.Destination);
        cmd.Parameters.AddWithValue("@desc",     body.Description);
        cmd.Parameters.AddWithValue("@lh",       body.LineHaulRate);
        cmd.Parameters.AddWithValue("@fsc",      body.FscRate);
        cmd.Parameters.AddWithValue("@tarp",     body.TarpRate);
        cmd.Parameters.AddWithValue("@extra",    body.ExtraFee);
        cmd.Parameters.AddWithValue("@terms",    body.Terms);
        cmd.Parameters.AddWithValue("@status",   body.Status);
        cmd.Parameters.AddWithValue("@btn",      body.BillToName);
        cmd.Parameters.AddWithValue("@bta",      body.BillToAddress);
        cmd.Parameters.AddWithValue("@cn",       body.ConsigneeName);
        cmd.Parameters.AddWithValue("@ca",       body.ConsigneeAddress);
        cmd.Parameters.AddWithValue("@driverId", body.DriverId.HasValue ? body.DriverId.Value : DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
        var newId = (int)cmd.LastInsertedId;

        return Ok(new { load_id = newId });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateLoad(int id, [FromBody] CreateLoadRequest body)
    {
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE loads SET
                load_number = @num, ship_date = @date, origin = @orig,
                destination = @dest, description = @desc, line_haul_rate = @lh,
                fsc_rate = @fsc, tarp_rate = @tarp, extra_fee = @extra, terms = @terms, bill_to_name = @btn,
                bill_to_address = @bta, consignee_name = @cn, consignee_address = @ca,
                driver_id = @driverId
            WHERE load_id = @id
        """;
        cmd.Parameters.AddWithValue("@num",      body.LoadNumber);
        cmd.Parameters.AddWithValue("@date",     body.ShipDate);
        cmd.Parameters.AddWithValue("@orig",     body.Origin);
        cmd.Parameters.AddWithValue("@dest",     body.Destination);
        cmd.Parameters.AddWithValue("@desc",     body.Description);
        cmd.Parameters.AddWithValue("@lh",       body.LineHaulRate);
        cmd.Parameters.AddWithValue("@fsc",      body.FscRate);
        cmd.Parameters.AddWithValue("@tarp",     body.TarpRate);
        cmd.Parameters.AddWithValue("@extra",    body.ExtraFee);
        cmd.Parameters.AddWithValue("@terms",    body.Terms);
        cmd.Parameters.AddWithValue("@btn",      body.BillToName);
        cmd.Parameters.AddWithValue("@bta",      body.BillToAddress);
        cmd.Parameters.AddWithValue("@cn",       body.ConsigneeName);
        cmd.Parameters.AddWithValue("@ca",       body.ConsigneeAddress);
        cmd.Parameters.AddWithValue("@driverId", body.DriverId.HasValue ? body.DriverId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@id",       id);
        await cmd.ExecuteNonQueryAsync();

        return Ok();
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest body)
    {
        var validStatuses = new[] { "pending", "complete", "invoiced", "paid", "cancelled" };
        if (!validStatuses.Contains(body.Status))
            return BadRequest(new { error = "Invalid status" });

        await using var conn = new MySqlConnection(_conn);
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
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM loads WHERE load_id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();

        return NoContent();
    }
}

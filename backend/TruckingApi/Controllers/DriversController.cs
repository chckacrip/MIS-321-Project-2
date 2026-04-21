using Microsoft.AspNetCore.Mvc;

namespace TruckingApi.Controllers;

[ApiController]
[Route("api/drivers")]
public class DriversController : ControllerBase
{
    private readonly string _conn;

    public DriversController(DatabaseOptions db) => _conn = db.ConnectionString;

    [HttpGet]
    public async Task<IActionResult> GetDrivers()
    {
        var sql = """
            SELECT driver_id, first_name || ' ' || last_name AS name, unit_number
            FROM drivers
            ORDER BY first_name
        """;
        return Content(await Database.ExecuteSql(_conn, sql), "application/json");
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDriver(int id)
    {
        var sql = """
            SELECT driver_id, unit_number, first_name, last_name, address, commission_rate
            FROM drivers
            WHERE driver_id = @id
        """;

        await using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_conn);
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
}

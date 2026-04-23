using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TruckingApi.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly string _conn;

    public AccountsController(DatabaseOptions db) => _conn = db.ConnectionString;

    // ── Employees ──────────────────────────────────────────────────────────────

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees()
    {
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT user_id, email, phone, first_name, last_name
            FROM users
            WHERE role = 'employee'
            ORDER BY last_name, first_name
            """;
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

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeRequest req)
    {
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO users (email, phone, password, role, first_name, last_name)
            VALUES (@email, @phone, @password, 'employee', @firstName, @lastName)
            """;
        cmd.Parameters.AddWithValue("@email",     req.Email);
        cmd.Parameters.AddWithValue("@phone",     req.Phone);
        cmd.Parameters.AddWithValue("@password",  req.Password);
        cmd.Parameters.AddWithValue("@firstName", req.FirstName);
        cmd.Parameters.AddWithValue("@lastName",  req.LastName);
        await cmd.ExecuteNonQueryAsync();
        return Ok();
    }

    [HttpPut("employees/{id}")]
    public async Task<IActionResult> UpdateEmployee(int id, [FromBody] UpdateEmployeeRequest req)
    {
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();

        if (!string.IsNullOrEmpty(req.Password))
        {
            cmd.CommandText = """
                UPDATE users SET email=@email, phone=@phone, password=@password,
                first_name=@firstName, last_name=@lastName
                WHERE user_id=@id AND role='employee'
                """;
            cmd.Parameters.AddWithValue("@password", req.Password);
        }
        else
        {
            cmd.CommandText = """
                UPDATE users SET email=@email, phone=@phone,
                first_name=@firstName, last_name=@lastName
                WHERE user_id=@id AND role='employee'
                """;
        }
        cmd.Parameters.AddWithValue("@email",     req.Email);
        cmd.Parameters.AddWithValue("@phone",     req.Phone);
        cmd.Parameters.AddWithValue("@firstName", req.FirstName);
        cmd.Parameters.AddWithValue("@lastName",  req.LastName);
        cmd.Parameters.AddWithValue("@id",        id);
        await cmd.ExecuteNonQueryAsync();
        return Ok();
    }

    [HttpDelete("employees/{id}")]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM users WHERE user_id=@id AND role='employee'";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
        return Ok();
    }

    // ── Truckers ───────────────────────────────────────────────────────────────

    [HttpGet("truckers")]
    public async Task<IActionResult> GetTruckers()
    {
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.user_id, u.email, u.phone, u.first_name, u.last_name,
                   d.driver_id, d.unit_number, d.address, d.commission_rate
            FROM users u
            JOIN drivers d ON u.driver_id = d.driver_id
            WHERE u.role = 'trucker'
            ORDER BY u.last_name, u.first_name
            """;
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

    [HttpPost("truckers")]
    public async Task<IActionResult> CreateTrucker([FromBody] CreateTruckerRequest req)
    {
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();

        // Create driver record first
        long driverId;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO drivers (unit_number, first_name, last_name, address, commission_rate)
                VALUES (@unit, @firstName, @lastName, @address, @rate)
                """;
            cmd.Parameters.AddWithValue("@unit",      req.UnitNumber);
            cmd.Parameters.AddWithValue("@firstName", req.FirstName);
            cmd.Parameters.AddWithValue("@lastName",  req.LastName);
            cmd.Parameters.AddWithValue("@address",   req.Address);
            cmd.Parameters.AddWithValue("@rate",      req.CommissionRate);
            await cmd.ExecuteNonQueryAsync();
            driverId = cmd.LastInsertedId;
        }

        // Create user account linked to driver
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (email, phone, password, role, first_name, last_name, driver_id)
                VALUES (@email, @phone, @password, 'trucker', @firstName, @lastName, @driverId)
                """;
            cmd.Parameters.AddWithValue("@email",     req.Email);
            cmd.Parameters.AddWithValue("@phone",     req.Phone);
            cmd.Parameters.AddWithValue("@password",  req.Password);
            cmd.Parameters.AddWithValue("@firstName", req.FirstName);
            cmd.Parameters.AddWithValue("@lastName",  req.LastName);
            cmd.Parameters.AddWithValue("@driverId",  driverId);
            await cmd.ExecuteNonQueryAsync();
        }

        return Ok();
    }

    [HttpPut("truckers/{id}")]
    public async Task<IActionResult> UpdateTrucker(int id, [FromBody] UpdateTruckerRequest req)
    {
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();

        // Update driver record
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE drivers d
                JOIN users u ON u.driver_id = d.driver_id
                SET d.unit_number=@unit, d.first_name=@firstName, d.last_name=@lastName,
                    d.address=@address, d.commission_rate=@rate
                WHERE u.user_id=@id
                """;
            cmd.Parameters.AddWithValue("@unit",      req.UnitNumber);
            cmd.Parameters.AddWithValue("@firstName", req.FirstName);
            cmd.Parameters.AddWithValue("@lastName",  req.LastName);
            cmd.Parameters.AddWithValue("@address",   req.Address);
            cmd.Parameters.AddWithValue("@rate",      req.CommissionRate);
            cmd.Parameters.AddWithValue("@id",        id);
            await cmd.ExecuteNonQueryAsync();
        }

        // Update user account
        await using (var cmd = conn.CreateCommand())
        {
            if (!string.IsNullOrEmpty(req.Password))
            {
                cmd.CommandText = """
                    UPDATE users SET email=@email, phone=@phone, password=@password,
                    first_name=@firstName, last_name=@lastName
                    WHERE user_id=@id AND role='trucker'
                    """;
                cmd.Parameters.AddWithValue("@password", req.Password);
            }
            else
            {
                cmd.CommandText = """
                    UPDATE users SET email=@email, phone=@phone,
                    first_name=@firstName, last_name=@lastName
                    WHERE user_id=@id AND role='trucker'
                    """;
            }
            cmd.Parameters.AddWithValue("@email",     req.Email);
            cmd.Parameters.AddWithValue("@phone",     req.Phone);
            cmd.Parameters.AddWithValue("@firstName", req.FirstName);
            cmd.Parameters.AddWithValue("@lastName",  req.LastName);
            cmd.Parameters.AddWithValue("@id",        id);
            await cmd.ExecuteNonQueryAsync();
        }

        return Ok();
    }

    [HttpDelete("truckers/{id}")]
    public async Task<IActionResult> DeleteTrucker(int id)
    {
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();

        // Get the driver_id before deleting
        int? driverId;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT driver_id FROM users WHERE user_id=@id AND role='trucker'";
            cmd.Parameters.AddWithValue("@id", id);
            var result = await cmd.ExecuteScalarAsync();
            driverId = result is DBNull || result is null ? null : Convert.ToInt32(result);
        }

        // Delete user account
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM users WHERE user_id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        // Delete driver record if linked
        if (driverId.HasValue)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM drivers WHERE driver_id=@driverId";
            cmd.Parameters.AddWithValue("@driverId", driverId.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        return Ok();
    }
}

using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TruckingApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly string _conn;

    public AuthController(DatabaseOptions db) => _conn = db.ConnectionString;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        await using var conn = new MySqlConnection(_conn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT user_id, email, phone, role, first_name, last_name, driver_id
            FROM users
            WHERE email = @email AND password = @password
            """;
        cmd.Parameters.AddWithValue("@email",    req.Email);
        cmd.Parameters.AddWithValue("@password", req.Password);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return Unauthorized(new { message = "Invalid email or password." });

        var role = reader.GetString(reader.GetOrdinal("role"));

        // Enforce login portal separation
        bool expectingEmployee = req.ExpectedRole == "employee";
        bool isEmployeeRole    = role == "employee" || role == "admin";

        if (expectingEmployee && !isEmployeeRole)
            return Unauthorized(new { message = "Please use the trucker login." });
        if (!expectingEmployee && role != "trucker")
            return Unauthorized(new { message = "Please use the employee login." });

        var driverIdOrdinal = reader.GetOrdinal("driver_id");
        return Ok(new
        {
            userId    = reader.GetInt32(reader.GetOrdinal("user_id")),
            email     = reader.GetString(reader.GetOrdinal("email")),
            role,
            firstName = reader.IsDBNull(reader.GetOrdinal("first_name")) ? null : reader.GetString(reader.GetOrdinal("first_name")),
            lastName  = reader.IsDBNull(reader.GetOrdinal("last_name"))  ? null : reader.GetString(reader.GetOrdinal("last_name")),
            driverId  = reader.IsDBNull(driverIdOrdinal) ? (int?)null : reader.GetInt32(driverIdOrdinal),
        });
    }
}

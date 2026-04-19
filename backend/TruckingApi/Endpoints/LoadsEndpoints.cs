using Microsoft.Data.Sqlite;

namespace TruckingApi;

public static class LoadsEndpoints
{
    public static void MapLoadsEndpoints(this WebApplication app, string connectionString)
    {
        // GET /api/loads?driverId=X
        app.MapGet("/api/loads", async (int? driverId) =>
        {
            string sql = driverId.HasValue
                ? $"""
                    SELECT l.load_id, l.load_number, l.ship_date, l.origin, l.destination,
                           l.description, l.line_haul_rate, l.fsc_rate, l.terms, l.status,
                           l.bill_to_name, l.bill_to_address, l.consignee_name, l.consignee_address,
                           d.first_name || ' ' || d.last_name AS driver, d.driver_id, d.unit_number
                    FROM loads l
                    JOIN load_drivers ld ON l.load_id = ld.load_id
                    JOIN drivers d ON ld.driver_id = d.driver_id
                    WHERE ld.driver_id = {driverId}
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

            return Results.Content(await Database.ExecuteSql(connectionString, sql), "application/json");
        });

        // GET /api/drivers — for the assign driver dropdown
        app.MapGet("/api/drivers", async () =>
        {
            var sql = """
                SELECT driver_id, first_name || ' ' || last_name AS name, unit_number
                FROM drivers
                ORDER BY first_name
            """;
            return Results.Content(await Database.ExecuteSql(connectionString, sql), "application/json");
        });

        // POST /api/loads — create a new load
        app.MapPost("/api/loads", async (CreateLoadRequest body) =>
        {
            await using var conn = new SqliteConnection(connectionString);
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

            return Results.Ok(new { load_id = newId });
        });

        // PATCH /api/loads/{id}/status — update load status
        app.MapPatch("/api/loads/{id}/status", async (int id, UpdateStatusRequest body) =>
        {
            var validStatuses = new[] { "pending", "complete", "invoiced", "paid" };
            if (!validStatuses.Contains(body.Status))
                return Results.BadRequest(new { error = "Invalid status" });

            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE loads SET status = @status WHERE load_id = @id";
            cmd.Parameters.AddWithValue("@status", body.Status);
            cmd.Parameters.AddWithValue("@id",     id);
            await cmd.ExecuteNonQueryAsync();

            return Results.Ok();
        });
    }
}

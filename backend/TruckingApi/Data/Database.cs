using System.Text.Json;
using Microsoft.Data.Sqlite;

// ============================================================
// TO SWITCH TO MYSQL LATER:
//   1. Replace "using Microsoft.Data.Sqlite" with "using MySqlConnector"
//   2. Replace SqliteConnection with MySqlConnection
//   3. Replace SqliteCommand   with MySqlCommand
//   4. Replace GetDatabaseSchema() body with the MySQL version below (commented out)
// ============================================================

namespace TruckingApi;

public static class Database
{
    // --- Get database schema (SQLite version) ---
    // SWAP THIS METHOD BODY for MySQL:
    //   var schema = new Dictionary<string, List<object>>();
    //   await using var conn = new MySqlConnection(connectionString);
    //   await conn.OpenAsync();
    //   var tables = new List<string>();
    //   await using (var cmd = new MySqlCommand(
    //       "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'", conn))
    //   await using (var reader = await cmd.ExecuteReaderAsync())
    //   { while (await reader.ReadAsync()) tables.Add(reader.GetString(0)); }
    //   foreach (var table in tables) {
    //       var cols = new List<object>();
    //       await using var cmd = new MySqlCommand(
    //           "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t", conn);
    //       cmd.Parameters.AddWithValue("@t", table);
    //       await using var reader = await cmd.ExecuteReaderAsync();
    //       while (await reader.ReadAsync())
    //           cols.Add(new { column = reader.GetString(0), type = reader.GetString(1), nullable = reader.GetString(2) == "YES" });
    //       schema[table] = cols;
    //   }
    //   return JsonSerializer.Serialize(schema);
    public static async Task<string> GetDatabaseSchema(string connectionString)
    {
        var schema = new Dictionary<string, List<object>>();

        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();

        var tables = new List<string>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
        }

        foreach (var table in tables)
        {
            var cols = new List<object>();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table})";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                cols.Add(new
                {
                    column   = reader.GetString(1),
                    type     = reader.GetString(2),
                    nullable = reader.GetInt32(3) == 0
                });
            }
            schema[table] = cols;
        }

        return JsonSerializer.Serialize(schema);
    }

    // --- Execute a validated SELECT query ---
    public static async Task<string> ExecuteSql(string connectionString, string sql)
    {
        if (!IsSafeQuery(sql))
            return """{"error": "Only SELECT queries are permitted."}""";

        try
        {
            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await using var reader = await cmd.ExecuteReaderAsync();

            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }

            if (rows.Count == 0)
                return """{"message": "No results found."}""";

            return JsonSerializer.Serialize(rows);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    // --- Validate SQL is read-only ---
    public static bool IsSafeQuery(string sql)
    {
        var trimmed = sql.Trim().ToUpper();
        if (!trimmed.StartsWith("SELECT")) return false;
        var blocked = new[] { "DROP", "DELETE", "UPDATE", "INSERT", "ALTER", "TRUNCATE", "CREATE", "EXEC", "EXECUTE", "GRANT", "REVOKE", ";--", "/*" };
        return !blocked.Any(kw => trimmed.Contains(kw));
    }
}

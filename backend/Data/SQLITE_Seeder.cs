using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenAI.Embeddings;

// SWITCHING TO MYSQL: delete this entire file and the two Seeder calls in Program.cs.
// Your real MySQL DB already has the schema and data — no seeding needed.

namespace TruckingApi;

public static class Seeder
{
    public static async Task SeedMockData(string connectionString, string dataJsonPath)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS drivers (
                    driver_id       INTEGER PRIMARY KEY AUTOINCREMENT,
                    unit_number     TEXT,
                    first_name      TEXT,
                    last_name       TEXT,
                    address         TEXT,
                    commission_rate REAL DEFAULT 0.18,
                    created_at      TEXT DEFAULT (datetime('now'))
                );
                CREATE TABLE IF NOT EXISTS loads (
                    load_id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    load_number       TEXT UNIQUE,
                    ship_date         TEXT,
                    origin            TEXT,
                    destination       TEXT,
                    description       TEXT,
                    line_haul_rate    REAL,
                    fsc_rate          REAL DEFAULT 0,
                    terms             TEXT DEFAULT 'Net 30',
                    status            TEXT DEFAULT 'pending',
                    bill_to_name      TEXT,
                    bill_to_address   TEXT,
                    consignee_name    TEXT,
                    consignee_address TEXT,
                    created_at        TEXT DEFAULT (datetime('now'))
                );
                CREATE TABLE IF NOT EXISTS load_drivers (
                    load_driver_id INTEGER PRIMARY KEY AUTOINCREMENT,
                    load_id        INTEGER REFERENCES loads(load_id),
                    driver_id      INTEGER REFERENCES drivers(driver_id),
                    UNIQUE(load_id, driver_id)
                );
                CREATE TABLE IF NOT EXISTS invoices (
                    invoice_id     INTEGER PRIMARY KEY AUTOINCREMENT,
                    invoice_number TEXT UNIQUE,
                    load_id        INTEGER REFERENCES loads(load_id),
                    invoice_date   TEXT,
                    due_date       TEXT,
                    payment_status TEXT DEFAULT 'unpaid',
                    paid_date      TEXT,
                    created_at     TEXT DEFAULT (datetime('now'))
                );
                CREATE TABLE IF NOT EXISTS driver_advances (
                    advance_id   INTEGER PRIMARY KEY AUTOINCREMENT,
                    driver_id    INTEGER REFERENCES drivers(driver_id),
                    load_id      INTEGER REFERENCES loads(load_id),
                    advance_date TEXT,
                    advance_type TEXT,
                    amount       REAL,
                    notes        TEXT
                );
                CREATE TABLE IF NOT EXISTS driver_pay_summaries (
                    summary_id             INTEGER PRIMARY KEY AUTOINCREMENT,
                    driver_id              INTEGER REFERENCES drivers(driver_id),
                    pay_period_start       TEXT,
                    pay_period_end         TEXT,
                    total_line_haul        REAL,
                    commission_rate        REAL,
                    total_fsc              REAL,
                    total_advances         REAL,
                    insurance_deduction    REAL,
                    workers_comp_deduction REAL,
                    net_pay                REAL,
                    created_at             TEXT DEFAULT (datetime('now'))
                );
                CREATE TABLE IF NOT EXISTS documents (
                    id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    title     TEXT,
                    content   TEXT,
                    embedding TEXT
                );
            """;
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var checkCmd = conn.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM drivers";
            if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
            {
                Console.WriteLine("[Startup] Mock data already seeded, skipping.");
                return;
            }
        }

        if (!File.Exists(dataJsonPath))
        {
            Console.WriteLine($"[Startup] data.json not found at {dataJsonPath}, skipping seed.");
            return;
        }

        Console.WriteLine($"[Startup] Seeding mock data from {dataJsonPath}...");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(dataJsonPath));
        var root = doc.RootElement;

        foreach (var d in root.GetProperty("drivers").EnumerateArray())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO drivers (driver_id,unit_number,first_name,last_name,address,commission_rate) VALUES (@id,@unit,@first,@last,@addr,@rate)";
            cmd.Parameters.AddWithValue("@id",    d.GetProperty("driver_id").GetInt32());
            cmd.Parameters.AddWithValue("@unit",  d.GetProperty("unit_number").GetString());
            cmd.Parameters.AddWithValue("@first", d.GetProperty("first_name").GetString());
            cmd.Parameters.AddWithValue("@last",  d.GetProperty("last_name").GetString());
            cmd.Parameters.AddWithValue("@addr",  d.GetProperty("address").GetString());
            cmd.Parameters.AddWithValue("@rate",  d.GetProperty("commission_rate").GetDouble());
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var l in root.GetProperty("loads").EnumerateArray())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO loads (load_id,load_number,ship_date,origin,destination,description,
                    line_haul_rate,fsc_rate,terms,status,bill_to_name,bill_to_address,consignee_name,consignee_address)
                VALUES (@id,@num,@date,@orig,@dest,@desc,@lh,@fsc,@terms,@status,@btn,@bta,@cn,@ca)
            """;
            cmd.Parameters.AddWithValue("@id",     l.GetProperty("load_id").GetInt32());
            cmd.Parameters.AddWithValue("@num",    l.GetProperty("load_number").GetString());
            cmd.Parameters.AddWithValue("@date",   l.GetProperty("ship_date").GetString());
            cmd.Parameters.AddWithValue("@orig",   l.GetProperty("origin").GetString());
            cmd.Parameters.AddWithValue("@dest",   l.GetProperty("destination").GetString());
            cmd.Parameters.AddWithValue("@desc",   l.GetProperty("description").GetString());
            cmd.Parameters.AddWithValue("@lh",     l.GetProperty("line_haul_rate").GetDouble());
            cmd.Parameters.AddWithValue("@fsc",    l.GetProperty("fsc_rate").GetDouble());
            cmd.Parameters.AddWithValue("@terms",  l.GetProperty("terms").GetString());
            cmd.Parameters.AddWithValue("@status", l.GetProperty("status").GetString());
            cmd.Parameters.AddWithValue("@btn",    l.GetProperty("bill_to_name").GetString());
            cmd.Parameters.AddWithValue("@bta",    l.GetProperty("bill_to_address").GetString());
            cmd.Parameters.AddWithValue("@cn",     l.GetProperty("consignee_name").GetString());
            cmd.Parameters.AddWithValue("@ca",     l.GetProperty("consignee_address").GetString());
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var ld in root.GetProperty("loadDrivers").EnumerateArray())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO load_drivers (load_id,driver_id) VALUES (@lid,@did)";
            cmd.Parameters.AddWithValue("@lid", ld.GetProperty("load_id").GetInt32());
            cmd.Parameters.AddWithValue("@did", ld.GetProperty("driver_id").GetInt32());
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var inv in root.GetProperty("invoices").EnumerateArray())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO invoices (invoice_id,invoice_number,load_id,invoice_date,due_date,payment_status,paid_date)
                VALUES (@id,@num,@lid,@idate,@ddate,@status,@paid)
            """;
            cmd.Parameters.AddWithValue("@id",     inv.GetProperty("invoice_id").GetInt32());
            cmd.Parameters.AddWithValue("@num",    inv.GetProperty("invoice_number").GetString());
            cmd.Parameters.AddWithValue("@lid",    inv.GetProperty("load_id").GetInt32());
            cmd.Parameters.AddWithValue("@idate",  inv.GetProperty("invoice_date").GetString());
            cmd.Parameters.AddWithValue("@ddate",  inv.GetProperty("due_date").GetString());
            cmd.Parameters.AddWithValue("@status", inv.GetProperty("payment_status").GetString());
            var paid = inv.GetProperty("paid_date");
            cmd.Parameters.AddWithValue("@paid",   paid.ValueKind == JsonValueKind.Null ? DBNull.Value : paid.GetString());
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var adv in root.GetProperty("advances").EnumerateArray())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO driver_advances (advance_id,driver_id,load_id,advance_date,advance_type,amount,notes)
                VALUES (@id,@did,@lid,@date,@type,@amt,@notes)
            """;
            cmd.Parameters.AddWithValue("@id",    adv.GetProperty("advance_id").GetInt32());
            cmd.Parameters.AddWithValue("@did",   adv.GetProperty("driver_id").GetInt32());
            var loadId = adv.GetProperty("load_id");
            cmd.Parameters.AddWithValue("@lid",   loadId.ValueKind == JsonValueKind.Null ? DBNull.Value : loadId.GetInt32());
            cmd.Parameters.AddWithValue("@date",  adv.GetProperty("advance_date").GetString());
            cmd.Parameters.AddWithValue("@type",  adv.GetProperty("advance_type").GetString());
            cmd.Parameters.AddWithValue("@amt",   adv.GetProperty("amount").GetDouble());
            cmd.Parameters.AddWithValue("@notes", adv.GetProperty("notes").GetString());
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var ps in root.GetProperty("paySummaries").EnumerateArray())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO driver_pay_summaries
                    (summary_id,driver_id,pay_period_start,pay_period_end,total_line_haul,commission_rate,
                     total_fsc,total_advances,insurance_deduction,workers_comp_deduction,net_pay)
                VALUES (@id,@did,@start,@end,@lh,@rate,@fsc,@adv,@ins,@wc,@net)
            """;
            cmd.Parameters.AddWithValue("@id",    ps.GetProperty("summary_id").GetInt32());
            cmd.Parameters.AddWithValue("@did",   ps.GetProperty("driver_id").GetInt32());
            cmd.Parameters.AddWithValue("@start", ps.GetProperty("pay_period_start").GetString());
            cmd.Parameters.AddWithValue("@end",   ps.GetProperty("pay_period_end").GetString());
            cmd.Parameters.AddWithValue("@lh",    ps.GetProperty("total_line_haul").GetDouble());
            cmd.Parameters.AddWithValue("@rate",  ps.GetProperty("commission_rate").GetDouble());
            cmd.Parameters.AddWithValue("@fsc",   ps.GetProperty("total_fsc").GetDouble());
            cmd.Parameters.AddWithValue("@adv",   ps.GetProperty("total_advances").GetDouble());
            cmd.Parameters.AddWithValue("@ins",   ps.GetProperty("insurance_deduction").GetDouble());
            cmd.Parameters.AddWithValue("@wc",    ps.GetProperty("workers_comp_deduction").GetDouble());
            cmd.Parameters.AddWithValue("@net",   ps.GetProperty("net_pay").GetDouble());
            await cmd.ExecuteNonQueryAsync();
        }

        Console.WriteLine("[Startup] Mock data seeded successfully.");
    }

    public static async Task EmbedSchemaAtStartup(List<SchemaChunk> chunks, EmbeddingClient embedder, string connectionString)
    {
        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync();

        await using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM documents WHERE embedding IS NOT NULL";
            var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            if (count > 0)
            {
                Console.WriteLine($"[Startup] Loading {count} existing document embeddings...");
                await using var loadCmd = conn.CreateCommand();
                loadCmd.CommandText = "SELECT title, content, embedding FROM documents WHERE embedding IS NOT NULL";
                await using var reader = await loadCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var text = reader.GetString(0) + "\n" + reader.GetString(1);
                    var floats = JsonSerializer.Deserialize<float[]>(reader.GetString(2))!;
                    chunks.Add(new SchemaChunk(text, floats));
                }
                Console.WriteLine("[Startup] Documents loaded successfully.");
                return;
            }
        }

        Console.WriteLine("[Startup] No documents found. Generating schema embeddings...");
        var schema = await Database.GetDatabaseSchema(connectionString);
        var schemaDoc = JsonDocument.Parse(schema);

        var texts = new List<string>();
        var titles = new List<string>();
        foreach (var table in schemaDoc.RootElement.EnumerateObject())
        {
            if (table.Name == "documents") continue;
            var cols = string.Join(", ", table.Value.EnumerateArray()
                .Select(c => $"{c.GetProperty("column").GetString()} ({c.GetProperty("type").GetString()})"));
            titles.Add(table.Name);
            texts.Add($"Table: {table.Name}\nColumns: {cols}");
        }

        var embeddings = await embedder.GenerateEmbeddingsAsync(texts);

        for (int i = 0; i < texts.Count; i++)
        {
            var floats = embeddings.Value[i].ToFloats().ToArray();
            var embJson = JsonSerializer.Serialize(floats);

            await using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = "INSERT INTO documents (title, content, embedding) VALUES (@title, @content, @embedding)";
            insertCmd.Parameters.AddWithValue("@title",     titles[i]);
            insertCmd.Parameters.AddWithValue("@content",   texts[i]);
            insertCmd.Parameters.AddWithValue("@embedding", embJson);
            await insertCmd.ExecuteNonQueryAsync();

            chunks.Add(new SchemaChunk(texts[i], floats));
        }

        Console.WriteLine($"[Startup] Generated and stored {texts.Count} document embeddings.");
    }
}

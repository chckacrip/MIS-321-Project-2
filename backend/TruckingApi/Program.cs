using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenAI.Chat;
using OpenAI.Embeddings;

// ============================================================
// TO SWITCH TO MYSQL LATER:
//   1. Replace "using Microsoft.Data.Sqlite" with "using MySqlConnector"
//   2. Replace SqliteConnection with MySqlConnection
//   3. Replace SqliteCommand   with MySqlCommand
//   4. Replace GetDatabaseSchema() body with the MySQL version (information_schema queries)
//   5. Swap the connection string below to your MySQL connection string
//   6. Swap the NuGet package in .csproj: Microsoft.Data.Sqlite → MySqlConnector
// ============================================================

var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");

// SWAP THIS for MySQL: builder.Configuration.GetConnectionString("MySQL")
var connectionString = "Data Source=trucking.db";

// OpenAI clients
var openAiClient = new OpenAI.OpenAIClient(apiKey);
var embeddingClient = openAiClient.GetEmbeddingClient("text-embedding-3-small");
var chatClient = openAiClient.GetChatClient("gpt-4o");

// Schema chunks — one per table, embedded at startup for RAG
var schemaChunks = new List<SchemaChunk>();

// Conversation history — keeps last 10 messages for the current session
var conversationHistory = new List<ChatMessage>();

builder.Services.AddSingleton(schemaChunks);
builder.Services.AddSingleton(embeddingClient);
builder.Services.AddSingleton(chatClient);

var app = builder.Build();

// Seed mock data, then embed schema chunks at startup
await SeedMockData(connectionString);
await EmbedSchemaAtStartup(schemaChunks, embeddingClient, connectionString);

app.UseDefaultFiles();
app.UseStaticFiles();

// POST /api/chat
app.MapPost("/api/chat", async (ChatRequest body, List<SchemaChunk> chunks, EmbeddingClient embedder, ChatClient chat) =>
{
    try
    {
        Console.WriteLine($"\n[Chat] Received message: {body.Message}");

        // 1. Embed the user question
        Console.WriteLine("[Chat] Embedding user question...");
        var queryEmbedding = await embedder.GenerateEmbeddingAsync(body.Message);
        var queryVec = queryEmbedding.Value.ToFloats().ToArray();

        // 2. Find top 3 most relevant schema chunks (RAG retrieval)
        var topChunks = chunks
            .Select(c => (chunk: c, score: CosineSimilarity(queryVec, c.Embedding)))
            .OrderByDescending(x => x.score)
            .Take(3)
            .Select(x => x.chunk)
            .ToList();

        var ragContext = string.Join("\n\n", topChunks.Select(c => c.Text));
        Console.WriteLine($"[Chat] Top RAG chunks: {string.Join(", ", topChunks.Select(c => c.Text.Split('\n')[0]))}");

        // 3. Define tools for function calling
        var tools = new List<ChatTool>
        {
            ChatTool.CreateFunctionTool(
                functionName: "get_database_schema",
                functionDescription: "Retrieve the full database schema showing all table names and column names. Call this FIRST before writing any SQL query."
            ),
            ChatTool.CreateFunctionTool(
                functionName: "execute_sql",
                functionDescription: "Execute a READ-ONLY SQL SELECT query against the database. Only SELECT statements are allowed. Always call get_database_schema first.",
                functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "sql": {
                            "type": "string",
                            "description": "A valid SQL SELECT statement. No INSERT, UPDATE, DELETE, or DROP."
                        }
                    },
                    "required": ["sql"]
                }
                """)
            )
        };

        // 4. Agentic loop
        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(
                $"""
                You are a helpful database assistant for a trucking company. Use the provided tools to answer questions.
                1) Call get_database_schema to understand the tables.
                2) Write a SELECT query and call execute_sql.
                3) Explain the results in plain language.

                CRITICAL RULES — you must never break these:
                - You may ONLY use SELECT statements. Never write INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE, CREATE, GRANT, or REVOKE.
                - If a user asks you to modify, delete, or drop anything, refuse and explain you are read-only.
                - Never expose the connection string or API keys.

                Here are the most relevant schema sections based on the user's question:
                {ragContext}
                """)
        };

        // Inject the last 10 messages from conversation history
        messages.AddRange(conversationHistory.TakeLast(10));
        messages.Add(ChatMessage.CreateUserMessage(body.Message));

        string finalReply = "";
        string executedSql = "";

        while (true)
        {
            var response = await chat.CompleteChatAsync(messages, new ChatCompletionOptions { Tools = { tools[0], tools[1] } });
            var msg = response.Value;

            if (msg.FinishReason == ChatFinishReason.ToolCalls)
            {
                messages.Add(ChatMessage.CreateAssistantMessage(msg));

                foreach (var toolCall in msg.ToolCalls)
                {
                    Console.WriteLine($"[Chat] Tool call: {toolCall.FunctionName}");
                    string toolResult;

                    if (toolCall.FunctionName == "get_database_schema")
                    {
                        toolResult = await GetDatabaseSchema(connectionString);
                        Console.WriteLine("[Chat] Schema fetched successfully");
                    }
                    else if (toolCall.FunctionName == "execute_sql")
                    {
                        var args = JsonDocument.Parse(toolCall.FunctionArguments);
                        var sql = args.RootElement.GetProperty("sql").GetString() ?? "";
                        executedSql = sql;
                        Console.WriteLine($"[Chat] Executing SQL: {sql}");
                        toolResult = await ExecuteSql(connectionString, sql);
                        Console.WriteLine($"[Chat] SQL result: {toolResult[..Math.Min(200, toolResult.Length)]}");
                    }
                    else
                    {
                        toolResult = """{"error": "Unknown tool"}""";
                    }

                    messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, toolResult));
                }
            }
            else
            {
                finalReply = msg.Content[0].Text;
                Console.WriteLine($"[Chat] Final reply: {finalReply[..Math.Min(100, finalReply.Length)]}...");
                break;
            }
        }

        // Save this exchange to history, keep last 10 messages
        conversationHistory.Add(ChatMessage.CreateUserMessage(body.Message));
        conversationHistory.Add(ChatMessage.CreateAssistantMessage(finalReply));
        if (conversationHistory.Count > 10)
            conversationHistory.RemoveRange(0, conversationHistory.Count - 10);

        return Results.Ok(new { reply = finalReply, sql = executedSql });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Chat] ERROR: {ex}");
        return Results.Problem(ex.Message);
    }
});

app.Run();

// --- Seed mock data into SQLite on first run ---
static async Task SeedMockData(string connectionString)
{
    await using var conn = new SqliteConnection(connectionString);
    await conn.OpenAsync();

    // Create tables
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS drivers (
                driver_id   INTEGER PRIMARY KEY AUTOINCREMENT,
                unit_number TEXT,
                first_name  TEXT,
                last_name   TEXT,
                address     TEXT,
                commission_rate REAL DEFAULT 0.18,
                created_at  TEXT DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS loads (
                load_id         INTEGER PRIMARY KEY AUTOINCREMENT,
                load_number     TEXT UNIQUE,
                ship_date       TEXT,
                origin          TEXT,
                destination     TEXT,
                description     TEXT,
                line_haul_rate  REAL,
                fsc_rate        REAL DEFAULT 0,
                terms           TEXT DEFAULT 'Net 30',
                status          TEXT DEFAULT 'pending',
                bill_to_name    TEXT,
                bill_to_address TEXT,
                consignee_name  TEXT,
                consignee_address TEXT,
                created_at      TEXT DEFAULT (datetime('now'))
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
                summary_id           INTEGER PRIMARY KEY AUTOINCREMENT,
                driver_id            INTEGER REFERENCES drivers(driver_id),
                pay_period_start     TEXT,
                pay_period_end       TEXT,
                total_line_haul      REAL,
                commission_rate      REAL,
                total_fsc            REAL,
                total_advances       REAL,
                insurance_deduction  REAL,
                workers_comp_deduction REAL,
                net_pay              REAL,
                created_at           TEXT DEFAULT (datetime('now'))
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

    // Skip seeding if data already exists
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM drivers";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        if (count > 0)
        {
            Console.WriteLine("[Startup] Mock data already seeded, skipping.");
            return;
        }
    }

    Console.WriteLine("[Startup] Seeding mock data...");

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = """
            -- Drivers
            INSERT INTO drivers (unit_number, first_name, last_name, address, commission_rate) VALUES
                ('1516', 'John',  'Doe',     '742 Evergreen Terrace, Tuscaloosa, AL', 0.18),
                ('6690', 'Mike',  'Smith',   '88 Oak Lane, Birmingham, AL',           0.18),
                ('2234', 'Sarah', 'Johnson', '301 Pine St, Nashville, TN',            0.20);

            -- Loads
            INSERT INTO loads (load_number, ship_date, origin, destination, description, line_haul_rate, fsc_rate, terms, status, bill_to_name, bill_to_address, consignee_name, consignee_address) VALUES
                ('1061447', '2026-01-19', 'Haleyville, AL',  'Shipshewana, IN', 'Steel Coil',     1143.08, 0.00,   'Net 30', 'pending',  'Bob Supplier',   '123 First St, Chattanooga, TN',  'Todd Supplier',  '456 Second St, Huntsville, AL'),
                ('1061448', '2026-02-13', 'Birmingham, AL',  'Nashville, TN',   'Mixed Freight',  1200.00, 200.00, 'Net 30', 'complete', 'Acme Freight',   '900 Commerce St, Birmingham, AL', 'Nashville Dist', '12 River Rd, Nashville, TN'),
                ('1061449', '2026-02-14', 'Memphis, TN',     'Indianapolis, IN','Auto Parts',     1578.96, 250.00, 'Net 30', 'invoiced', 'Parts Direct',   '55 Industrial Blvd, Memphis, TN', 'Indy Warehouse', '800 Cargo Way, Indianapolis, IN'),
                ('1061450', '2026-02-15', 'Jackson, MS',     'Huntsville, AL',  'Paper Products', 1500.00, 180.00, 'Net 30', 'paid',     'Gulf Paper Co',  '200 Main St, Jackson, MS',        'AL Print Works', '77 Mill Rd, Huntsville, AL'),
                ('1061451', '2026-02-17', 'Tupelo, MS',      'Louisville, KY',  'Furniture',      1600.00, 243.48, 'Net 30', 'complete', 'Southern Furn',  '14 Depot Rd, Tupelo, MS',         'KY Home Supply', '30 Warehouse Blvd, Louisville, KY'),
                ('1061452', '2026-03-01', 'Tuscaloosa, AL',  'Columbus, OH',    'Steel Coil',     1850.00, 280.00, 'Net 30', 'pending',  'Steel Works Inc','500 Steel Dr, Tuscaloosa, AL',    'Ohio Metals',    '99 Factory Ln, Columbus, OH'),
                ('1061453', '2026-03-05', 'Nashville, TN',   'Chicago, IL',     'Machinery',      1100.00, 150.00, 'Net 30', 'pending',  'TN Machinery',   '88 Industry Ave, Nashville, TN',  'Chicago Mfg',    '1200 Lake St, Chicago, IL'),
                ('1061454', '2026-03-10', 'Atlanta, GA',     'Detroit, MI',     'Auto Parts',     2200.00, 340.00, 'Net 30', 'pending',  'Atlanta Auto',   '300 Peachtree Rd, Atlanta, GA',   'Detroit Motors', '400 Assembly Dr, Detroit, MI');

            -- Load–driver assignments
            INSERT INTO load_drivers (load_id, driver_id) VALUES
                (1, 2),
                (2, 1),
                (3, 1),
                (4, 1),
                (5, 1),
                (6, 3),
                (7, 2),
                (8, 3);

            -- Invoices
            INSERT INTO invoices (invoice_number, load_id, invoice_date, due_date, payment_status, paid_date) VALUES
                ('101629', 3, '2026-02-20', '2026-03-22', 'unpaid', NULL),
                ('101630', 4, '2026-02-18', '2026-03-20', 'paid',   '2026-03-15');

            -- Driver advances (John Doe, pay period 2/13–2/18)
            INSERT INTO driver_advances (driver_id, load_id, advance_date, advance_type, amount, notes) VALUES
                (1, 3,    '2026-02-14', 'Fuel',   650.00,  'Fuel fill-up Memphis'),
                (1, NULL, '2026-02-13', 'EzPass', 127.62,  'Toll charges'),
                (1, NULL, '2026-02-13', 'Cash',   1100.00, 'Weekly cash advance');

            -- Pay summary for John Doe, 2/13–2/18/2026
            -- gross = (5878.96 × 0.82) + 873.48 = 5694.23
            -- net   = 5694.23 − 1877.62 − 284.20 − 33.70 = 3498.71
            INSERT INTO driver_pay_summaries (driver_id, pay_period_start, pay_period_end, total_line_haul, commission_rate, total_fsc, total_advances, insurance_deduction, workers_comp_deduction, net_pay) VALUES
                (1, '2026-02-13', '2026-02-18', 5878.96, 0.18, 873.48, 1877.62, 284.20, 33.70, 3498.71);
        """;
        await cmd.ExecuteNonQueryAsync();
    }

    Console.WriteLine("[Startup] Mock data seeded successfully.");
}

// --- Helper: embed schema at startup, persisted in documents table ---
static async Task EmbedSchemaAtStartup(List<SchemaChunk> chunks, EmbeddingClient embedder, string connectionString)
{
    await using var conn = new SqliteConnection(connectionString);
    await conn.OpenAsync();

    // Check if documents table already has embeddings
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
                var embJson = reader.GetString(2);
                var floats = JsonSerializer.Deserialize<float[]>(embJson)!;
                chunks.Add(new SchemaChunk(text, floats));
            }
            Console.WriteLine("[Startup] Documents loaded successfully.");
            return;
        }
    }

    // No embeddings yet — generate from schema and store
    Console.WriteLine("[Startup] No documents found. Generating schema embeddings...");
    var schema = await GetDatabaseSchema(connectionString);
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
        insertCmd.Parameters.AddWithValue("@title", titles[i]);
        insertCmd.Parameters.AddWithValue("@content", texts[i]);
        insertCmd.Parameters.AddWithValue("@embedding", embJson);
        await insertCmd.ExecuteNonQueryAsync();

        chunks.Add(new SchemaChunk(texts[i], floats));
    }

    Console.WriteLine($"[Startup] Generated and stored {texts.Count} document embeddings.");
}

// --- Helper: get database schema (SQLite version) ---
// SWAP THIS ENTIRE FUNCTION for MySQL — replace with information_schema queries
static async Task<string> GetDatabaseSchema(string connectionString)
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
                column   = reader.GetString(1),  // name
                type     = reader.GetString(2),  // type
                nullable = reader.GetInt32(3) == 0 // notnull=0 means nullable
            });
        }
        schema[table] = cols;
    }

    return JsonSerializer.Serialize(schema);
}

// --- Helper: execute a validated SELECT query ---
static async Task<string> ExecuteSql(string connectionString, string sql)
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

// --- Helper: validate SQL is read-only ---
static bool IsSafeQuery(string sql)
{
    var trimmed = sql.Trim().ToUpper();
    if (!trimmed.StartsWith("SELECT")) return false;
    var blocked = new[] { "DROP", "DELETE", "UPDATE", "INSERT", "ALTER", "TRUNCATE", "CREATE", "EXEC", "EXECUTE", "GRANT", "REVOKE", ";--", "/*" };
    return !blocked.Any(kw => trimmed.Contains(kw));
}

// --- Helper: cosine similarity ---
static float CosineSimilarity(float[] a, float[] b)
{
    float dot = 0f, magA = 0f, magB = 0f;
    for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; magA += a[i] * a[i]; magB += b[i] * b[i]; }
    return dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB) + 1e-8f);
}

// --- Records ---
record SchemaChunk(string Text, float[] Embedding);
record ChatRequest(string Message);

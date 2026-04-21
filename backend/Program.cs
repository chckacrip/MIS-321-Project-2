using OpenAI.Chat;
using TruckingApi;

// ============================================================
// TO SWITCH TO MYSQL LATER:
//   1. Data/Database.cs  — swap SQLite → MySqlConnector (see comments inside)
//   2. Data/Seeder.cs    — delete this file entirely
//   3. Here              — swap connection string line below
//   4. Here              — remove the two Seeder calls
//   5. .csproj           — swap Microsoft.Data.Sqlite → MySqlConnector
// ============================================================

// In Docker, wwwroot is populated by the Dockerfile COPY step.
// Locally, point directly at the frontend source folder.
var contentRoot = Directory.GetCurrentDirectory();
var wwwroot = Path.Combine(contentRoot, "wwwroot");
if (!Directory.Exists(wwwroot))
    wwwroot = Path.GetFullPath(Path.Combine(contentRoot, "..", "frontend"));

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = wwwroot
});

var apiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");

// SWAP THIS for MySQL: builder.Configuration.GetConnectionString("MySQL")
var connectionString = "Data Source=SQLITE_trucking.db";

var openAiClient = new OpenAI.OpenAIClient(apiKey);
var embeddingClient = openAiClient.GetEmbeddingClient("text-embedding-3-small");
var chatClient = openAiClient.GetChatClient("gpt-4o");

var schemaChunks = new List<SchemaChunk>();
var conversationHistory = new List<ChatMessage>();

builder.Services.AddControllers();
builder.Services.AddSingleton(new DatabaseOptions(connectionString));
builder.Services.AddSingleton(schemaChunks);
builder.Services.AddSingleton(conversationHistory);
builder.Services.AddSingleton(embeddingClient);
builder.Services.AddSingleton(chatClient);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("http://127.0.0.1:5500", "http://localhost:5500").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Resolve path to mock data — works locally and in Docker
var dataJsonPath = Path.Combine(app.Environment.WebRootPath ?? "", "mock", "SQLITE_data.json");
if (!File.Exists(dataJsonPath))
    dataJsonPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "frontend", "mock", "SQLITE_data.json"));

await Seeder.SeedMockData(connectionString, dataJsonPath);
await Seeder.EmbedSchemaAtStartup(schemaChunks, embeddingClient, connectionString);

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();

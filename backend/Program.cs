using OpenAI.Chat;
using TruckingApi;

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

var connectionString = builder.Configuration.GetConnectionString("MySQL")
    ?? throw new InvalidOperationException("MySQL connection string is not configured.");

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

await Seeder.EmbedSchemaAtStartup(schemaChunks, embeddingClient, connectionString);

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();

using System.Text.Json;
using MySqlConnector;
using OpenAI.Embeddings;

namespace TruckingApi;

public static class Seeder
{
    public static async Task EmbedSchemaAtStartup(List<SchemaChunk> chunks, EmbeddingClient embedder, string connectionString)
    {
        await using var conn = new MySqlConnection(connectionString);
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

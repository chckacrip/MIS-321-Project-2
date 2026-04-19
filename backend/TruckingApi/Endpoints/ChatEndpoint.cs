using System.Text.Json;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace TruckingApi;

public static class ChatEndpoint
{
    public static void MapChatEndpoint(
        this WebApplication app,
        string connectionString,
        List<ChatMessage> conversationHistory)
    {
        app.MapPost("/api/chat", async (
            ChatRequest body,
            List<SchemaChunk> chunks,
            EmbeddingClient embedder,
            ChatClient chat) =>
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
                                toolResult = await Database.GetDatabaseSchema(connectionString);
                                Console.WriteLine("[Chat] Schema fetched successfully");
                            }
                            else if (toolCall.FunctionName == "execute_sql")
                            {
                                var args = JsonDocument.Parse(toolCall.FunctionArguments);
                                var sql = args.RootElement.GetProperty("sql").GetString() ?? "";
                                executedSql = sql;
                                Console.WriteLine($"[Chat] Executing SQL: {sql}");
                                toolResult = await Database.ExecuteSql(connectionString, sql);
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
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0f, magA = 0f, magB = 0f;
        for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; magA += a[i] * a[i]; magB += b[i] * b[i]; }
        return dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB) + 1e-8f);
    }
}

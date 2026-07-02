using System.Text;
using System.Text.Json;

using AgGradeDesigner.Core.Ai.Chat;

namespace AgGradeDesigner.Core.Ai.Chat
{
    /// <summary>
    /// Google Gemini generateContent client with function calling support.
    /// </summary>
    public sealed class GeminiChatClient : ILlmChatClient, IDisposable
    {
        /// <summary>HTTP client used for Gemini generateContent requests.</summary>
        private readonly HttpClient _HttpClient;

        /// <summary>Trimmed Gemini API base URL.</summary>
        private readonly string _BaseUrl;

        /// <summary>Configured Gemini model identifier.</summary>
        private readonly string _Model;

        /// <summary>Google AI Studio API key.</summary>
        private readonly string _ApiKey;

        /// <summary>
        /// Creates a Gemini chat client.
        /// </summary>
        /// <param name="BaseUrl">Gemini API base URL.</param>
        /// <param name="Model">Model identifier.</param>
        /// <param name="ApiKey">Google AI Studio API key.</param>
        /// <param name="HttpClient">Optional injected HTTP client for testing.</param>
        public GeminiChatClient
            (
            string BaseUrl,
            string Model,
            string ApiKey,
            HttpClient? HttpClient = null
            )
        {
            _BaseUrl = BaseUrl.TrimEnd('/');
            _Model = Model;
            _ApiKey = ApiKey;
            _HttpClient = HttpClient ?? CreateDefaultHttpClient();
        }

        /// <summary>
        /// Sends one generateContent request with optional function declarations.
        /// </summary>
        /// <param name="Messages">Conversation messages including system prompt.</param>
        /// <param name="Tools">Registered tool schemas.</param>
        /// <param name="CancellationToken">Cancellation token.</param>
        /// <returns>Parsed assistant response.</returns>
        public async Task<ChatCompletionResponse> CompleteAsync
            (
            IReadOnlyList<ChatMessage> Messages,
            IReadOnlyList<LlmToolDefinition> Tools,
            CancellationToken CancellationToken
            )
        {
            string Url = $"{_BaseUrl}/models/{Uri.EscapeDataString(_Model)}:generateContent?key={Uri.EscapeDataString(_ApiKey)}";
            using HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);

            Dictionary<string, object?> Body = new Dictionary<string, object?>
            {
                ["contents"] = BuildContents(Messages),
                ["generationConfig"] = new Dictionary<string, object?>
                {
                    ["temperature"] = LlmProviderDefaults.TEMPERATURE
                }
            };

            ChatMessage? SystemMessage = Messages.FirstOrDefault(M => M.Role == ChatRole.System);
            if (SystemMessage != null)
            {
                Body["systemInstruction"] = new Dictionary<string, object?>
                {
                    ["parts"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["text"] = SystemMessage.Content
                        }
                    }
                };
            }

            if (Tools.Count > 0)
            {
                Body["tools"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["functionDeclarations"] = BuildFunctionDeclarations(Tools)
                    }
                };
                Body["toolConfig"] = new Dictionary<string, object?>
                {
                    ["functionCallingConfig"] = new Dictionary<string, object?>
                    {
                        ["mode"] = "AUTO"
                    }
                };
            }

            string Json = JsonSerializer.Serialize(Body);
            Request.Content = new StringContent(Json, Encoding.UTF8, "application/json");

            using HttpResponseMessage Response = await _HttpClient.SendAsync(Request, CancellationToken).ConfigureAwait(false);
            string ResponseJson = await Response.Content.ReadAsStringAsync(CancellationToken).ConfigureAwait(false);
            if (!Response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Gemini request failed ({(int)Response.StatusCode}): {TrimForDisplay(ResponseJson)}");

            return ParseResponse(ResponseJson);
        }

        /// <summary>
        /// Releases the owned HTTP client when this instance created it.
        /// </summary>
        public void Dispose
            (
            )
        {
            _HttpClient.Dispose();
        }

        /// <summary>
        /// Creates an HTTP client with normative provider timeouts.
        /// </summary>
        /// <returns>Configured HTTP client.</returns>
        static private HttpClient CreateDefaultHttpClient
            (
            )
        {
            SocketsHttpHandler Handler = new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(LlmProviderDefaults.CONNECT_TIMEOUT_SECONDS)
            };
            HttpClient Client = new HttpClient(Handler)
            {
                Timeout = TimeSpan.FromSeconds(LlmProviderDefaults.REQUEST_TIMEOUT_SECONDS)
            };
            return Client;
        }

        /// <summary>
        /// Converts internal messages to Gemini contents array, including function responses.
        /// </summary>
        /// <param name="Messages">Conversation messages.</param>
        /// <returns>Serializable Gemini contents.</returns>
        static private List<object> BuildContents
            (
            IReadOnlyList<ChatMessage> Messages
            )
        {
            List<object> Contents = new List<object>();
            foreach (ChatMessage Message in Messages)
            {
                if (Message.Role == ChatRole.System)
                    continue;

                if (Message.Role == ChatRole.Tool)
                {
                    Contents.Add(new Dictionary<string, object?>
                    {
                        ["role"] = "function",
                        ["parts"] = new object[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["functionResponse"] = new Dictionary<string, object?>
                                {
                                    ["name"] = Message.ToolName ?? "tool",
                                    ["response"] = JsonSerializer.Deserialize<object>(Message.Content)
                                }
                            }
                        }
                    });
                    continue;
                }

                if (Message.Role == ChatRole.Assistant && Message.ToolCalls.Count > 0)
                {
                    List<object> Parts = new List<object>();
                    if (!string.IsNullOrWhiteSpace(Message.Content))
                    {
                        Parts.Add(new Dictionary<string, object?>
                        {
                            ["text"] = Message.Content
                        });
                    }

                    foreach (LlmToolCall Call in Message.ToolCalls)
                    {
                        Parts.Add(new Dictionary<string, object?>
                        {
                            ["functionCall"] = new Dictionary<string, object?>
                            {
                                ["name"] = Call.Name,
                                ["args"] = JsonSerializer.Deserialize<object>(Call.ArgumentsJson)
                            }
                        });
                    }

                    Contents.Add(new Dictionary<string, object?>
                    {
                        ["role"] = "model",
                        ["parts"] = Parts
                    });
                    continue;
                }

                Contents.Add(new Dictionary<string, object?>
                {
                    ["role"] = Message.Role == ChatRole.User ? "user" : "model",
                    ["parts"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["text"] = Message.Content
                        }
                    }
                });
            }

            return Contents;
        }

        /// <summary>
        /// Converts tool schemas to Gemini function declarations.
        /// </summary>
        /// <param name="Tools">Registered tool schemas.</param>
        /// <returns>Function declaration objects.</returns>
        static private List<object> BuildFunctionDeclarations
            (
            IReadOnlyList<LlmToolDefinition> Tools
            )
        {
            List<object> Declarations = new List<object>();
            foreach (LlmToolDefinition Tool in Tools)
            {
                Declarations.Add(new Dictionary<string, object?>
                {
                    ["name"] = Tool.Name,
                    ["description"] = Tool.Description,
                    ["parameters"] = JsonSerializer.Deserialize<object>(Tool.Parameters.GetRawText())
                });
            }

            return Declarations;
        }

        /// <summary>
        /// Parses a Gemini generateContent response JSON payload.
        /// </summary>
        /// <param name="ResponseJson">Raw response JSON.</param>
        /// <returns>Parsed assistant response.</returns>
        static private ChatCompletionResponse ParseResponse
            (
            string ResponseJson
            )
        {
            using JsonDocument Document = JsonDocument.Parse(ResponseJson);
            JsonElement Root = Document.RootElement;
            JsonElement Candidate = Root.GetProperty("candidates")[0];
            JsonElement Content = Candidate.GetProperty("content");
            JsonElement Parts = Content.GetProperty("parts");

            ChatCompletionResponse Response = new ChatCompletionResponse();
            foreach (JsonElement Part in Parts.EnumerateArray())
            {
                if (Part.TryGetProperty("text", out JsonElement TextElement))
                {
                    Response.AssistantText += TextElement.GetString() ?? string.Empty;
                }

                if (Part.TryGetProperty("functionCall", out JsonElement FunctionCall))
                {
                    string Name = FunctionCall.GetProperty("name").GetString() ?? string.Empty;
                    string ArgsJson = FunctionCall.TryGetProperty("args", out JsonElement ArgsElement)
                        ? ArgsElement.GetRawText()
                        : "{}";
                    Response.ToolCalls.Add(new LlmToolCall
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = Name,
                        ArgumentsJson = ArgsJson
                    });
                }
            }

            return Response;
        }

        /// <summary>
        /// Trims long provider error payloads for user-visible messages.
        /// </summary>
        /// <param name="Text">Raw error text.</param>
        /// <returns>Shortened message.</returns>
        static private string TrimForDisplay
            (
            string Text
            )
        {
            if (Text.Length <= LlmProviderDefaults.ERROR_DISPLAY_MAX_CHARS)
                return Text;

            return Text[..LlmProviderDefaults.ERROR_DISPLAY_MAX_CHARS] + "...";
        }
    }
}

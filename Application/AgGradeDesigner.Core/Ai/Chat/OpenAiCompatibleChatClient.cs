using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using AgGradeDesigner.Core.Ai;

namespace AgGradeDesigner.Core.Ai.Chat
{
    /// <summary>
    /// OpenAI Chat Completions client shared by OpenAI, OpenRouter, and custom endpoints.
    /// </summary>
    public sealed class OpenAiCompatibleChatClient : ILlmChatClient, IDisposable
    {
        /// <summary>HTTP client used for chat completion requests.</summary>
        private readonly HttpClient _HttpClient;

        /// <summary>Trimmed OpenAI-compatible API base URL.</summary>
        private readonly string _BaseUrl;

        /// <summary>Configured model identifier.</summary>
        private readonly string _Model;

        /// <summary>Bearer token, or empty when authentication is not required.</summary>
        private readonly string _ApiKey;

        /// <summary>Optional provider-specific request headers.</summary>
        private readonly IReadOnlyDictionary<string, string>? _ExtraHeaders;

        /// <summary>
        /// Creates a client for an OpenAI-compatible chat completions endpoint.
        /// </summary>
        /// <param name="BaseUrl">API root URL ending before <c>/chat/completions</c>.</param>
        /// <param name="Model">Model identifier.</param>
        /// <param name="ApiKey">Bearer token, or empty when authentication is not required.</param>
        /// <param name="ExtraHeaders">Optional additional request headers.</param>
        /// <param name="HttpClient">Optional injected HTTP client for testing.</param>
        public OpenAiCompatibleChatClient
            (
            string BaseUrl,
            string Model,
            string ApiKey,
            IReadOnlyDictionary<string, string>? ExtraHeaders = null,
            HttpClient? HttpClient = null
            )
        {
            _BaseUrl = BaseUrl.TrimEnd('/');
            _Model = Model;
            _ApiKey = ApiKey;
            _ExtraHeaders = ExtraHeaders;
            _HttpClient = HttpClient ?? CreateDefaultHttpClient();
        }

        /// <summary>
        /// Sends one chat completion request with optional tool definitions.
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
            string Endpoint = ResolveChatCompletionsUrl();
            using HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            ApplyHeaders(Request);

            Dictionary<string, object?> Body = new Dictionary<string, object?>
            {
                ["model"] = _Model,
                ["temperature"] = LlmProviderDefaults.TEMPERATURE,
                ["tool_choice"] = "auto",
                ["messages"] = BuildMessages(Messages)
            };

            if (Tools.Count > 0)
                Body["tools"] = BuildTools(Tools);

            string Json = JsonSerializer.Serialize(Body);
            Request.Content = new StringContent(Json, Encoding.UTF8, "application/json");

            using HttpResponseMessage Response = await _HttpClient.SendAsync(Request, CancellationToken).ConfigureAwait(false);
            string ResponseJson = await Response.Content.ReadAsStringAsync(CancellationToken).ConfigureAwait(false);
            if (!Response.IsSuccessStatusCode)
                throw new InvalidOperationException($"LLM request failed ({(int)Response.StatusCode}): {TrimForDisplay(ResponseJson)}");

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
        /// Resolves the chat completions endpoint URL from the configured base URL.
        /// </summary>
        /// <returns>Absolute chat completions URL.</returns>
        private string ResolveChatCompletionsUrl
            (
            )
        {
            if (_BaseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                return _BaseUrl;

            if (_BaseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                return _BaseUrl + "/chat/completions";

            return _BaseUrl + "/v1/chat/completions";
        }

        /// <summary>
        /// Applies authentication and provider-specific headers to the request.
        /// </summary>
        /// <param name="Request">Outgoing HTTP request.</param>
        private void ApplyHeaders
            (
            HttpRequestMessage Request
            )
        {
            if (!string.IsNullOrWhiteSpace(_ApiKey))
                Request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _ApiKey);

            if (_ExtraHeaders == null)
                return;

            foreach (KeyValuePair<string, string> Header in _ExtraHeaders)
                Request.Headers.TryAddWithoutValidation(Header.Key, Header.Value);
        }

        /// <summary>
        /// Converts internal chat messages to OpenAI wire-format objects.
        /// </summary>
        /// <param name="Messages">Conversation messages.</param>
        /// <returns>Serializable message list.</returns>
        static private List<object> BuildMessages
            (
            IReadOnlyList<ChatMessage> Messages
            )
        {
            List<object> Result = new List<object>();
            foreach (ChatMessage Message in Messages)
            {
                if (Message.Role == ChatRole.Tool)
                {
                    Result.Add(new Dictionary<string, object?>
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = Message.ToolCallId ?? string.Empty,
                        ["content"] = Message.Content
                    });
                    continue;
                }

                if (Message.Role == ChatRole.Assistant && Message.ToolCalls.Count > 0)
                {
                    List<object> ToolCalls = new List<object>();
                    foreach (LlmToolCall Call in Message.ToolCalls)
                    {
                        ToolCalls.Add(new Dictionary<string, object?>
                        {
                            ["id"] = Call.Id,
                            ["type"] = "function",
                            ["function"] = new Dictionary<string, object?>
                            {
                                ["name"] = Call.Name,
                                ["arguments"] = Call.ArgumentsJson
                            }
                        });
                    }

                    Result.Add(new Dictionary<string, object?>
                    {
                        ["role"] = "assistant",
                        ["content"] = string.IsNullOrWhiteSpace(Message.Content) ? null : Message.Content,
                        ["tool_calls"] = ToolCalls
                    });
                    continue;
                }

                Result.Add(new Dictionary<string, object?>
                {
                    ["role"] = MapRole(Message.Role),
                    ["content"] = Message.Content
                });
            }

            return Result;
        }

        /// <summary>
        /// Maps internal chat roles to OpenAI role strings.
        /// </summary>
        /// <param name="Role">Internal role.</param>
        /// <returns>OpenAI role string.</returns>
        static private string MapRole
            (
            ChatRole Role
            )
        {
            return Role switch
            {
                ChatRole.System => "system",
                ChatRole.User => "user",
                ChatRole.Assistant => "assistant",
                ChatRole.Tool => "tool",
                _ => "user"
            };
        }

        /// <summary>
        /// Converts tool definitions to OpenAI tools array entries.
        /// </summary>
        /// <param name="Tools">Registered tool schemas.</param>
        /// <returns>Serializable tools list.</returns>
        static private List<object> BuildTools
            (
            IReadOnlyList<LlmToolDefinition> Tools
            )
        {
            List<object> Result = new List<object>();
            foreach (LlmToolDefinition Tool in Tools)
            {
                Result.Add(new Dictionary<string, object?>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = Tool.Name,
                        ["description"] = Tool.Description,
                        ["parameters"] = JsonSerializer.Deserialize<object>(Tool.Parameters.GetRawText())
                    }
                });
            }

            return Result;
        }

        /// <summary>
        /// Parses an OpenAI chat completion response JSON payload.
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
            JsonElement Choice = Root.GetProperty("choices")[0];
            JsonElement Message = Choice.GetProperty("message");

            ChatCompletionResponse Response = new ChatCompletionResponse();
            if (Message.TryGetProperty("content", out JsonElement ContentElement) &&
                ContentElement.ValueKind == JsonValueKind.String)
            {
                Response.AssistantText = ContentElement.GetString() ?? string.Empty;
            }

            if (Message.TryGetProperty("tool_calls", out JsonElement ToolCallsElement) &&
                ToolCallsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement CallElement in ToolCallsElement.EnumerateArray())
                {
                    JsonElement Function = CallElement.GetProperty("function");
                    Response.ToolCalls.Add(new LlmToolCall
                    {
                        Id = CallElement.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N"),
                        Name = Function.GetProperty("name").GetString() ?? string.Empty,
                        ArgumentsJson = Function.GetProperty("arguments").GetString() ?? "{}"
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

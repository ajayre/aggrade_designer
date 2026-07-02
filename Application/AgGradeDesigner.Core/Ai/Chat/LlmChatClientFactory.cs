using AgGradeDesigner.Core.Ai;

namespace AgGradeDesigner.Core.Ai.Chat
{
    /// <summary>
    /// Creates configured LLM chat clients from persisted AI settings.
    /// </summary>
    static public class LlmChatClientFactory
    {
        /// <summary>
        /// OpenRouter attribution referer header per §8.4.7.
        /// </summary>
        public const string OPENROUTER_HTTP_REFERER = "https://aggrade.designer";

        /// <summary>
        /// OpenRouter product title header per §8.4.8.
        /// </summary>
        public const string OPENROUTER_TITLE = "AgGrade Designer";

        /// <summary>
        /// Creates an LLM client for the supplied settings, or null when configuration is incomplete.
        /// </summary>
        /// <param name="Settings">User AI settings.</param>
        /// <returns>Configured chat client.</returns>
        static public ILlmChatClient Create
            (
            AiSettings Settings
            )
        {
            AiProviderKind Provider = Settings.ResolvePhase12Provider();
            string Model = LlmProviderDefaults.ResolveModel(Settings);

            switch (Provider)
            {
                case AiProviderKind.GoogleGemini:
                    if (string.IsNullOrWhiteSpace(Settings.ApiKey))
                        throw new InvalidOperationException("Google Gemini requires an API key.");
                    return new GeminiChatClient(LlmProviderDefaults.GEMINI_BASE_URL, Model, Settings.ApiKey);

                case AiProviderKind.OpenAi:
                    if (string.IsNullOrWhiteSpace(Settings.ApiKey))
                        throw new InvalidOperationException("OpenAI requires an API key.");
                    return new OpenAiCompatibleChatClient(
                        LlmProviderDefaults.OPENAI_BASE_URL,
                        Model,
                        Settings.ApiKey);

                case AiProviderKind.OpenRouter:
                    if (string.IsNullOrWhiteSpace(Settings.ApiKey))
                        throw new InvalidOperationException("OpenRouter requires an API key.");
                    return new OpenAiCompatibleChatClient(
                        LlmProviderDefaults.OPENROUTER_BASE_URL,
                        Model,
                        Settings.ApiKey,
                        new Dictionary<string, string>
                        {
                            ["HTTP-Referer"] = OPENROUTER_HTTP_REFERER,
                            ["X-OpenRouter-Title"] = OPENROUTER_TITLE
                        });

                case AiProviderKind.CustomOpenAiCompatible:
                    if (string.IsNullOrWhiteSpace(Settings.CustomBaseUrl))
                        throw new InvalidOperationException("Custom provider requires a base URL.");
                    if (string.IsNullOrWhiteSpace(Settings.CustomModel))
                        throw new InvalidOperationException("Custom provider requires a model.");
                    return new OpenAiCompatibleChatClient(
                        Settings.CustomBaseUrl.TrimEnd('/'),
                        Settings.CustomModel,
                        Settings.ApiKey);

                default:
                    throw new InvalidOperationException("No AI provider is configured.");
            }
        }

        /// <summary>
        /// Returns true when settings contain enough information to start a chat request.
        /// </summary>
        /// <param name="Settings">User AI settings.</param>
        /// <returns>True when chat can be attempted.</returns>
        static public bool CanChat
            (
            AiSettings Settings
            )
        {
            try
            {
                Create(Settings);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}

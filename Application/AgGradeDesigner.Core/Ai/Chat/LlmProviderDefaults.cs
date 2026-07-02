using AgGradeDesigner.Core.Ai;

namespace AgGradeDesigner.Core.Ai.Chat
{
    /// <summary>
    /// Default models, URLs, and timeouts for remote AI providers.
    /// </summary>
    static public class LlmProviderDefaults
    {
        /// <summary>Default OpenAI Chat Completions base URL.</summary>
        public const string OPENAI_BASE_URL = "https://api.openai.com/v1";

        /// <summary>Default OpenAI model when no override is configured.</summary>
        public const string OPENAI_DEFAULT_MODEL = "gpt-4o";

        /// <summary>Default Gemini API base URL.</summary>
        public const string GEMINI_BASE_URL = "https://generativelanguage.googleapis.com/v1beta";

        /// <summary>Default Gemini model when no override is configured.</summary>
        public const string GEMINI_DEFAULT_MODEL = "gemini-2.5-flash";

        /// <summary>Default OpenRouter base URL.</summary>
        public const string OPENROUTER_BASE_URL = "https://openrouter.ai/api/v1";

        /// <summary>Default OpenRouter model when no override is configured.</summary>
        public const string OPENROUTER_DEFAULT_MODEL = "openai/gpt-4o-mini";

        /// <summary>HTTP request timeout in seconds per §8.1.7.</summary>
        public const int REQUEST_TIMEOUT_SECONDS = 120;

        /// <summary>HTTP connect timeout in seconds per §8.1.7.</summary>
        public const int CONNECT_TIMEOUT_SECONDS = 30;

        /// <summary>Maximum tool-call rounds per user message per §8.1.8.</summary>
        public const int MAX_TOOL_ROUNDS = 8;

        /// <summary>Generation temperature per §8.1.9.</summary>
        public const double TEMPERATURE = 0.2;

        /// <summary>
        /// Resolves the effective model identifier for the configured provider.
        /// </summary>
        /// <param name="Settings">User AI settings.</param>
        /// <returns>Model name to send on chat requests.</returns>
        static public string ResolveModel
            (
            AiSettings Settings
            )
        {
            AiProviderKind Provider = Settings.ResolvePhase12Provider();
            return Provider switch
            {
                AiProviderKind.GoogleGemini => string.IsNullOrWhiteSpace(Settings.ModelOverride)
                    ? GEMINI_DEFAULT_MODEL
                    : Settings.ModelOverride,
                AiProviderKind.OpenAi => string.IsNullOrWhiteSpace(Settings.ModelOverride)
                    ? OPENAI_DEFAULT_MODEL
                    : Settings.ModelOverride,
                AiProviderKind.OpenRouter => string.IsNullOrWhiteSpace(Settings.ModelOverride)
                    ? OPENROUTER_DEFAULT_MODEL
                    : Settings.ModelOverride,
                AiProviderKind.CustomOpenAiCompatible => Settings.CustomModel,
                _ => OPENAI_DEFAULT_MODEL
            };
        }

        /// <summary>
        /// Resolves the chat completions base URL for the configured provider.
        /// </summary>
        /// <param name="Settings">User AI settings.</param>
        /// <returns>Base URL without trailing slash.</returns>
        static public string ResolveBaseUrl
            (
            AiSettings Settings
            )
        {
            AiProviderKind Provider = Settings.ResolvePhase12Provider();
            return Provider switch
            {
                AiProviderKind.GoogleGemini => GEMINI_BASE_URL,
                AiProviderKind.OpenAi => OPENAI_BASE_URL,
                AiProviderKind.OpenRouter => OPENROUTER_BASE_URL,
                AiProviderKind.CustomOpenAiCompatible => Settings.CustomBaseUrl.TrimEnd('/'),
                _ => OPENAI_BASE_URL
            };
        }
    }
}

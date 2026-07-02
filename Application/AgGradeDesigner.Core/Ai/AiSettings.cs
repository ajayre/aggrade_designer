namespace AgGradeDesigner.Core.Ai
{
    /// <summary>
    /// User-configurable AI provider connection settings persisted outside project files.
    /// </summary>
    public sealed class AiSettings
    {
        /// <summary>Selected remote AI provider.</summary>
        public AiProviderKind Provider { get; set; } = AiProviderKind.OpenAi;

        /// <summary>Optional model override for named remote providers.</summary>
        public string ModelOverride { get; set; } = string.Empty;

        /// <summary>API key for remote providers when required.</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Base URL for custom OpenAI-compatible endpoints.</summary>
        public string CustomBaseUrl { get; set; } = string.Empty;

        /// <summary>Model identifier for custom OpenAI-compatible endpoints.</summary>
        public string CustomModel { get; set; } = string.Empty;

        /// <summary>
        /// Creates a deep copy suitable for dialog session editing.
        /// </summary>
        /// <returns>Independent settings snapshot.</returns>
        public AiSettings Clone
            (
            )
        {
            return new AiSettings
            {
                Provider = Provider,
                ModelOverride = ModelOverride,
                ApiKey = ApiKey,
                CustomBaseUrl = CustomBaseUrl,
                CustomModel = CustomModel
            };
        }

        /// <summary>
        /// Copies values from another settings instance into this one.
        /// </summary>
        /// <param name="Source">Settings to copy.</param>
        public void CopyFrom
            (
            AiSettings Source
            )
        {
            Provider = Source.Provider;
            ModelOverride = Source.ModelOverride;
            ApiKey = Source.ApiKey;
            CustomBaseUrl = Source.CustomBaseUrl;
            CustomModel = Source.CustomModel;
        }

        /// <summary>
        /// Returns a Phase 1/2-safe provider, falling back when Local LLM is stored.
        /// </summary>
        /// <returns>Resolved provider for chat and settings UI.</returns>
        public AiProviderKind ResolvePhase12Provider
            (
            )
        {
            if (Provider == AiProviderKind.LocalLlm || Provider == AiProviderKind.None)
                return AiProviderKind.OpenAi;

            return Provider;
        }
    }
}

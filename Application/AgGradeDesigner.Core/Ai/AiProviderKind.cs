namespace AgGradeDesigner.Core.Ai
{
    /// <summary>
    /// Identifies a configured remote AI provider for chat completions.
    /// </summary>
    public enum AiProviderKind
    {
        /// <summary>No provider selected.</summary>
        None = 0,

        /// <summary>Google Gemini generative language API.</summary>
        GoogleGemini,

        /// <summary>OpenAI Chat Completions API.</summary>
        OpenAi,

        /// <summary>OpenRouter OpenAI-compatible gateway.</summary>
        OpenRouter,

        /// <summary>User-supplied OpenAI-compatible HTTP endpoint.</summary>
        CustomOpenAiCompatible,

        /// <summary>Local inference server (Phase 3 only).</summary>
        LocalLlm
    }
}

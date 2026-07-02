namespace AgGradeDesigner.Core.Ai.Chat
{
    /// <summary>
    /// One structured tool call requested by the LLM.
    /// </summary>
    public sealed class LlmToolCall
    {
        /// <summary>Provider-assigned tool call identifier.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Surface tool name to execute.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>JSON-encoded arguments object.</summary>
        public string ArgumentsJson { get; set; } = "{}";
    }
}

namespace AgGradeDesigner.Core.Ai.Chat
{
    /// <summary>
    /// Response from one LLM chat completion request.
    /// </summary>
    public sealed class ChatCompletionResponse
    {
        /// <summary>Assistant text when no further tool calls are pending.</summary>
        public string AssistantText { get; set; } = string.Empty;

        /// <summary>Tool calls requested by the assistant.</summary>
        public List<LlmToolCall> ToolCalls { get; set; } = new List<LlmToolCall>();

        /// <summary>True when the model requested one or more tool calls.</summary>
        public bool HasToolCalls => ToolCalls.Count > 0;
    }
}

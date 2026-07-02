namespace AgGradeDesigner.Core.Ai.Chat
{
    /// <summary>
    /// One message in an LLM chat session.
    /// </summary>
    public sealed class ChatMessage
    {
        /// <summary>Message role.</summary>
        public ChatRole Role { get; set; }

        /// <summary>Text content for system, user, and assistant messages.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Tool call identifier for tool result messages.</summary>
        public string? ToolCallId { get; set; }

        /// <summary>Tool name associated with a tool result message.</summary>
        public string? ToolName { get; set; }

        /// <summary>Pending tool calls requested by the assistant.</summary>
        public List<LlmToolCall> ToolCalls { get; set; } = new List<LlmToolCall>();
    }
}

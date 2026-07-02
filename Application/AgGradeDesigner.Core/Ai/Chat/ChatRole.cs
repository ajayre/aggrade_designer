namespace AgGradeDesigner.Core.Ai.Chat
{
    /// <summary>
    /// Role of a chat message in an LLM conversation.
    /// </summary>
    public enum ChatRole
    {
        /// <summary>System instruction message.</summary>
        System,

        /// <summary>End-user message.</summary>
        User,

        /// <summary>Assistant natural-language reply.</summary>
        Assistant,

        /// <summary>Tool execution result returned to the model.</summary>
        Tool
    }
}

using AgGradeDesigner.Core.Ai.Chat;

namespace AgGradeDesigner.Core.Ai.Chat
{
    /// <summary>
    /// Abstraction over remote LLM chat completion providers.
    /// </summary>
    public interface ILlmChatClient
    {
        /// <summary>
        /// Sends one chat completion request with optional tool definitions.
        /// </summary>
        /// <param name="Messages">Conversation messages including system prompt.</param>
        /// <param name="Tools">Registered tool schemas.</param>
        /// <param name="CancellationToken">Cancellation token.</param>
        /// <returns>Parsed assistant response.</returns>
        Task<ChatCompletionResponse> CompleteAsync
            (
            IReadOnlyList<ChatMessage> Messages,
            IReadOnlyList<LlmToolDefinition> Tools,
            CancellationToken CancellationToken
            );
    }
}

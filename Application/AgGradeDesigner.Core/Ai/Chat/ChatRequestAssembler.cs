using AgGradeDesigner.Core.Ai.Chat;
using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Ai.Chat
{
    /// <summary>
    /// Assembles outbound LLM request messages for unit testing and diagnostics.
    /// </summary>
    static public class ChatRequestAssembler
    {
        /// <summary>
        /// Builds the message list that would be sent on the first round of a chat turn.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="SessionMessages">Existing session messages.</param>
        /// <param name="UserMessage">Latest user message.</param>
        /// <param name="DisplayUnits">Active global display unit preference.</param>
        /// <returns>Outbound messages including the system prompt.</returns>
        static public IReadOnlyList<ChatMessage> BuildFirstRoundMessages
            (
            Models.ProjectDocument Project,
            IReadOnlyList<ChatMessage> SessionMessages,
            string UserMessage,
            DisplayUnitSystem DisplayUnits = DisplayUnitSystem.Imperial
            )
        {
            List<ChatMessage> Messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = ChatRole.System,
                    Content = SystemPromptBuilder.Build(Project, AiDeliveryPhase.Phase1, DisplayUnits)
                }
            };

            foreach (ChatMessage Message in SessionMessages)
            {
                Messages.Add(new ChatMessage
                {
                    Role = Message.Role,
                    Content = Message.Content,
                    ToolCallId = Message.ToolCallId,
                    ToolName = Message.ToolName
                });
            }

            Messages.Add(new ChatMessage
            {
                Role = ChatRole.User,
                Content = UserMessage
            });

            return Messages;
        }
    }
}

using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.Chat;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Tests.Ai
{
    /// <summary>
    /// Unit tests for outbound chat request assembly.
    /// </summary>

    public sealed class ChatRequestAssemblerTests
    {
        /// <summary>
        /// Expected value used when asserting first round message count.
        /// </summary>

        private const int EXPECTED_FIRST_ROUND_MESSAGE_COUNT = 2;

        /// <summary>
        /// Verifies the first chat round includes the normative system prompt message.
        /// </summary>
        [Fact]
        public void BuildFirstRoundMessages_IncludesSystemPrompt
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            IReadOnlyList<ChatMessage> Messages = ChatRequestAssembler.BuildFirstRoundMessages(
                Project,
                Array.Empty<ChatMessage>(),
                "What is the cut/fill?");

            Assert.Equal(EXPECTED_FIRST_ROUND_MESSAGE_COUNT, Messages.Count);
            Assert.Equal(ChatRole.System, Messages[0].Role);
            Assert.Contains("AgGrade Designer assistant", Messages[0].Content, StringComparison.Ordinal);
            Assert.Equal(ChatRole.User, Messages[1].Role);
        }
    }
}

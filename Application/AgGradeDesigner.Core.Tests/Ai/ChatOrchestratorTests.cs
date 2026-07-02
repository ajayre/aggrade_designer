using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.Chat;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai
{
    /// <summary>
    /// Unit tests for <see cref="ChatOrchestrator"/> using a mocked LLM client.
    /// </summary>
    public sealed class ChatOrchestratorTests
    {
        /// <summary>
        /// Verifies off-topic input is refused locally without calling the LLM client.
        /// </summary>
        [Fact]
        public async Task SendUserMessage_OffTopic_ReturnsStandardRefusal
            (
            )
        {
            ChatOrchestrator Orchestrator = new ChatOrchestrator(new SurfaceToolDispatcher());
            RecordingChatClient Client = new RecordingChatClient();
            ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                SyntheticDesignGridFixture.CreateFlatGrid(100.0, 100.0));

            ChatTurnResult Result = await Orchestrator.SendUserMessageAsync(
                Client,
                Project,
                Array.Empty<ChatMessage>(),
                "Please tell me a joke about homework",
                CancellationToken.None);

            Assert.Equal(SystemPromptBuilder.STANDARD_REFUSAL_TEXT, Result.AssistantText);
            Assert.Equal(0, Client.CallCount);
        }

        /// <summary>
        /// Verifies tool calls are executed and forwarded back to the LLM until a final reply is returned.
        /// </summary>
        [Fact]
        public async Task SendUserMessage_ToolCallLoop_ExecutesToolAndReturnsAssistantText
            (
            )
        {
            ChatOrchestrator Orchestrator = new ChatOrchestrator(new SurfaceToolDispatcher());
            RecordingChatClient Client = new RecordingChatClient
            {
                Responses =
                [
                    new ChatCompletionResponse
                    {
                        ToolCalls =
                        [
                            new LlmToolCall
                            {
                                Id = "call-1",
                                Name = "get_surface_summary",
                                ArgumentsJson = "{}"
                            }
                        ]
                    },
                    new ChatCompletionResponse
                    {
                        AssistantText = "Cut/fill summary complete."
                    }
                ]
            };

            ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                SyntheticDesignGridFixture.CreateFlatGrid(100.0, 100.0));

            ChatTurnResult Result = await Orchestrator.SendUserMessageAsync(
                Client,
                Project,
                Array.Empty<ChatMessage>(),
                "Summarize the field",
                CancellationToken.None);

            Assert.Equal("Cut/fill summary complete.", Result.AssistantText);
            Assert.Equal(2, Client.CallCount);
        }

        /// <summary>
        /// Test double that records LLM invocation count and returns queued responses.
        /// </summary>
        private sealed class RecordingChatClient : ILlmChatClient
        {
            /// <summary>Queued responses returned in order.</summary>
            public List<ChatCompletionResponse> Responses { get; set; } = new List<ChatCompletionResponse>();

            /// <summary>Number of completion calls made.</summary>
            public int CallCount { get; private set; }

            /// <summary>
            /// Returns the next queued response.
            /// </summary>
            /// <param name="Messages">Ignored for the mock.</param>
            /// <param name="Tools">Ignored for the mock.</param>
            /// <param name="CancellationToken">Cancellation token.</param>
            /// <returns>Next queued response.</returns>
            public Task<ChatCompletionResponse> CompleteAsync
                (
                IReadOnlyList<ChatMessage> Messages,
                IReadOnlyList<LlmToolDefinition> Tools,
                CancellationToken CancellationToken
                )
            {
                CallCount++;
                if (Responses.Count == 0)
                    return Task.FromResult(new ChatCompletionResponse { AssistantText = "done" });

                ChatCompletionResponse Next = Responses[0];
                Responses.RemoveAt(0);
                return Task.FromResult(Next);
            }
        }
    }
}

using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.Chat;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Tests.Ai
{
    /// <summary>
    /// Unit tests for preview cancel handling in <see cref="ChatOrchestrator"/>.
    /// </summary>
    public sealed class SurfacePreviewCancelTests
    {
        /// <summary>Existing elevation used by the fixture grid.</summary>
        private const double EXISTING_ELEVATION_M = 100.0;

        /// <summary>Initial proposed elevation used by the fixture grid.</summary>
        private const double PROPOSED_ELEVATION_M = 100.0;

        /// <summary>Vertical offset applied during preview.</summary>
        private const double OFFSET_DELTA_M = 0.4;

        /// <summary>
        /// Verifies an explicit decline message discards the preview without calling the LLM.
        /// </summary>
        [Fact]
        public async Task SendUserMessage_ExplicitDecline_DiscardsPreviewLocally
            (
            )
        {
            ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M));
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase2);
            ChatOrchestrator Orchestrator = new ChatOrchestrator(Dispatcher);

            Assert.True(Dispatcher.Execute(
                Project,
                "offset_surface",
                $$"""{"delta_z": {{OFFSET_DELTA_M}}}""").Success);

            RecordingChatClient Client = new RecordingChatClient();
            ChatTurnResult Result = await Orchestrator.SendUserMessageAsync(
                Client,
                Project,
                Array.Empty<ChatMessage>(),
                "don't commit",
                DisplayUnitSystem.Imperial,
                CancellationToken.None);

            Assert.False(Client.WasCalled);
            Assert.Contains("discarded", Result.AssistantText, StringComparison.OrdinalIgnoreCase);
            Assert.False(Dispatcher.EditSession.PreviewActive);

            GridBinData Bin = Project.DesignGrid!.Bins.First(B => B.Valid);
            Assert.Equal(PROPOSED_ELEVATION_M, Bin.ProposedM, 6);
        }

        /// <summary>
        /// Test double that records whether the LLM client was invoked.
        /// </summary>
        private sealed class RecordingChatClient : ILlmChatClient
        {
            /// <summary>True after <see cref="CompleteAsync"/> is called.</summary>
            public bool WasCalled { get; private set; }

            /// <summary>
            /// Records that a completion request was attempted.
            /// </summary>
            /// <param name="Messages">Outbound chat messages.</param>
            /// <param name="Tools">Registered tool schemas.</param>
            /// <param name="CancellationToken">Cancellation token.</param>
            /// <returns>Never reached in these tests.</returns>
            public Task<ChatCompletionResponse> CompleteAsync
                (
                IReadOnlyList<ChatMessage> Messages,
                IReadOnlyList<LlmToolDefinition> Tools,
                CancellationToken CancellationToken
                )
            {
                WasCalled = true;
                throw new InvalidOperationException("LLM should not be called for explicit preview cancel.");
            }
        }
    }
}

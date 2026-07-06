using System.Text.Json;

using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.Chat;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;
using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Tests.Ai
{
    /// <summary>
    /// Unit tests for <see cref="AiToolTrace"/> JSONL output.
    /// </summary>
    public sealed class AiToolTraceTests
    {
        /// <summary>Flat elevation m used by this component.</summary>
        private const double FLAT_ELEVATION_M = 100.0;

        /// <summary>
        /// Verifies the chat orchestrator emits replayable tool trace records.
        /// </summary>
        [Fact]
        public async Task ChatOrchestrator_LogsToolTraceRecords
            (
            )
        {
            List<string> Lines = new List<string>();
            AiToolTrace.JsonLineSink = Lines.Add;
            try
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
                            AssistantText = "Done."
                        }
                    ]
                };

                ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                    SyntheticDesignGridFixture.CreateFlatGrid(FLAT_ELEVATION_M, FLAT_ELEVATION_M));

                await Orchestrator.SendUserMessageAsync(
                    Client,
                    Project,
                    Array.Empty<ChatMessage>(),
                    "Summarize the field",
                    DisplayUnitSystem.Imperial,
                    CancellationToken.None);
            }
            finally
            {
                AiToolTrace.JsonLineSink = null;
            }

            Assert.Contains(Lines, Line => Line.Contains("\"event\":\"turn_start\"", StringComparison.Ordinal));
            Assert.Contains(Lines, Line => Line.Contains("\"event\":\"tool\"", StringComparison.Ordinal));
            Assert.Contains(Lines, Line => Line.Contains("\"tool\":\"get_surface_summary\"", StringComparison.Ordinal));
            Assert.Contains(Lines, Line => Line.Contains("\"event\":\"turn_end\"", StringComparison.Ordinal));

            string TempPath = Path.Combine(Path.GetTempPath(), $"aitrace_{Guid.NewGuid():N}.jsonl");
            try
            {
                File.WriteAllLines(TempPath, Lines);
                IReadOnlyList<AiToolTraceStep> Steps = AiToolTraceReader.ReadToolSteps(TempPath);
                Assert.Single(Steps);
                Assert.Equal("get_surface_summary", Steps[0].Tool);
                Assert.Equal("{}", Steps[0].ArgumentsJson);
                Assert.True(Steps[0].Success);
            }
            finally
            {
                if (File.Exists(TempPath))
                    File.Delete(TempPath);
            }
        }

        /// <summary>
        /// Verifies off-topic turns do not emit tool trace records.
        /// </summary>
        [Fact]
        public async Task ChatOrchestrator_OffTopic_DoesNotLogToolTrace
            (
            )
        {
            List<string> Lines = new List<string>();
            AiToolTrace.JsonLineSink = Lines.Add;
            try
            {
                ChatOrchestrator Orchestrator = new ChatOrchestrator(new SurfaceToolDispatcher());
                RecordingChatClient Client = new RecordingChatClient();
                ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                    SyntheticDesignGridFixture.CreateFlatGrid(FLAT_ELEVATION_M, FLAT_ELEVATION_M));

                await Orchestrator.SendUserMessageAsync(
                    Client,
                    Project,
                    Array.Empty<ChatMessage>(),
                    "Tell me a joke",
                    DisplayUnitSystem.Imperial,
                    CancellationToken.None);
            }
            finally
            {
                AiToolTrace.JsonLineSink = null;
            }

            Assert.Empty(Lines);
        }

        /// <summary>Test double that returns queued LLM responses.</summary>
        private sealed class RecordingChatClient : ILlmChatClient
        {
            /// <summary>Queued responses returned in order.</summary>
            public List<ChatCompletionResponse> Responses { get; set; } = new List<ChatCompletionResponse>();

            /// <summary>
            /// Returns the next queued response.
            /// </summary>
            public Task<ChatCompletionResponse> CompleteAsync
                (
                IReadOnlyList<ChatMessage> Messages,
                IReadOnlyList<LlmToolDefinition> Tools,
                CancellationToken CancellationToken
                )
            {
                if (Responses.Count == 0)
                    return Task.FromResult(new ChatCompletionResponse { AssistantText = "done" });

                ChatCompletionResponse Next = Responses[0];
                Responses.RemoveAt(0);
                return Task.FromResult(Next);
            }
        }
    }
}

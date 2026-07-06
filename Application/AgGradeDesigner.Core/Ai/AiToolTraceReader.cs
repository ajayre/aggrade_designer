using System.Text.Json;

namespace AgGradeDesigner.Core.Ai
{
    /// <summary>
    /// One replayable tool invocation parsed from an <see cref="AiToolTrace"/> JSONL file.
    /// </summary>
    public readonly struct AiToolTraceStep
    {
        /// <summary>Monotonic turn identifier from the trace file.</summary>
        public int Turn { get; init; }

        /// <summary>One-based tool index within the turn.</summary>
        public int Step { get; init; }

        /// <summary>Surface tool name.</summary>
        public string Tool { get; init; }

        /// <summary>Tool arguments JSON suitable for <see cref="SurfaceTools.SurfaceToolDispatcher.Execute"/>.</summary>
        public string ArgumentsJson { get; init; }

        /// <summary>True when the traced execution succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>Tool result JSON returned to the LLM when traced.</summary>
        public string ResultJson { get; init; }
    }

    /// <summary>
    /// Reads replayable tool steps from AI tool trace JSONL files.
    /// </summary>
    static public class AiToolTraceReader
    {
        /// <summary>
        /// Loads tool call records from a JSONL trace file.
        /// </summary>
        /// <param name="JsonlPath">Path to a companion <c>*_aitools.jsonl</c> file.</param>
        /// <returns>Ordered tool steps suitable for deterministic replay tests.</returns>
        static public IReadOnlyList<AiToolTraceStep> ReadToolSteps
            (
            string JsonlPath
            )
        {
            List<AiToolTraceStep> Steps = new List<AiToolTraceStep>();
            foreach (string Line in File.ReadLines(JsonlPath))
            {
                if (string.IsNullOrWhiteSpace(Line))
                    continue;

                using JsonDocument Document = JsonDocument.Parse(Line);
                JsonElement Root = Document.RootElement;
                if (!Root.TryGetProperty("event", out JsonElement EventElement))
                    continue;

                if (!string.Equals(EventElement.GetString(), "tool", StringComparison.Ordinal))
                    continue;

                Steps.Add(new AiToolTraceStep
                {
                    Turn = Root.GetProperty("turn").GetInt32(),
                    Step = Root.GetProperty("step").GetInt32(),
                    Tool = Root.GetProperty("tool").GetString() ?? string.Empty,
                    ArgumentsJson = Root.GetProperty("arguments").GetRawText(),
                    Success = Root.GetProperty("success").GetBoolean(),
                    ResultJson = Root.GetProperty("result").GetRawText()
                });
            }

            return Steps;
        }
    }
}

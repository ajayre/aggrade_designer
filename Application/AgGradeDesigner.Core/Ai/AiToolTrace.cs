using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using AgGradeDesigner.Core.Ai.SurfaceTools;

namespace AgGradeDesigner.Core.Ai
{
    /// <summary>
    /// Records AI surface tool calls as JSONL for replay in unit tests without an LLM.
    /// </summary>
    static public class AiToolTrace
    {
        /// <summary>Current trace schema version written to JSONL records.</summary>
        public const int SCHEMA_VERSION = 1;

        /// <summary>Receives one JSONL record per trace event when tracing is enabled.</summary>
        static public Action<string>? JsonLineSink { get; set; }

        /// <summary>Turn counter incremented for each traced user message.</summary>
        static private int _TurnCounter;

        /// <summary>Active turn identifier for in-flight tool records.</summary>
        static private int _CurrentTurn;

        /// <summary>User message associated with the active turn.</summary>
        static private string _CurrentUserMessage = string.Empty;

        /// <summary>Project label associated with the active turn.</summary>
        static private string? _CurrentProject;

        /// <summary>
        /// Opens a traced user turn before the LLM/tool loop runs.
        /// </summary>
        /// <param name="UserMessage">Latest user chat message.</param>
        /// <param name="ProjectDisplayName">Project display name for fixture context.</param>
        static public void BeginTurn
            (
            string UserMessage,
            string? ProjectDisplayName
            )
        {
            _TurnCounter++;
            _CurrentTurn = _TurnCounter;
            _CurrentUserMessage = UserMessage;
            _CurrentProject = ProjectDisplayName;
            WriteRecord(new TurnStartRecord
            {
                Turn = _CurrentTurn,
                UserMessage = UserMessage,
                Project = ProjectDisplayName
            });
        }

        /// <summary>
        /// Records one executed surface tool call with full LLM arguments and C# result JSON.
        /// </summary>
        /// <param name="LlmRound">Zero-based LLM completion round within the turn.</param>
        /// <param name="Step">One-based tool index within the turn.</param>
        /// <param name="ToolCallId">Provider-assigned tool call identifier.</param>
        /// <param name="ToolName">Surface tool name.</param>
        /// <param name="ArgumentsJson">Raw tool arguments JSON from the LLM.</param>
        /// <param name="Result">Tool execution result.</param>
        static public void LogTool
            (
            int LlmRound,
            int Step,
            string ToolCallId,
            string ToolName,
            string ArgumentsJson,
            SurfaceToolResult Result
            )
        {
            WriteRecord(new ToolRecord
            {
                Turn = _CurrentTurn,
                UserMessage = _CurrentUserMessage,
                Project = _CurrentProject,
                LlmRound = LlmRound,
                Step = Step,
                ToolCallId = ToolCallId,
                Tool = ToolName,
                Arguments = ParseJsonNode(ArgumentsJson),
                Success = Result.Success,
                Result = ParseJsonNode(Result.Json),
                Error = Result.Success ? null : Result.ErrorMessage
            });
        }

        /// <summary>
        /// Closes a traced user turn after the final assistant reply is known.
        /// </summary>
        /// <param name="AssistantText">Final assistant natural-language reply.</param>
        /// <param name="ToolCount">Number of tool calls executed during the turn.</param>
        static public void EndTurn
            (
            string AssistantText,
            int ToolCount
            )
        {
            WriteRecord(new TurnEndRecord
            {
                Turn = _CurrentTurn,
                AssistantText = AssistantText,
                ToolCount = ToolCount
            });
        }

        /// <summary>
        /// Serializes and emits one JSONL trace record when a sink is configured.
        /// </summary>
        /// <param name="Record">Record payload.</param>
        static private void WriteRecord
            (
            object Record
            )
        {
            if (JsonLineSink == null)
                return;

            string Line = JsonSerializer.Serialize(Record, SerializerOptions);
            JsonLineSink.Invoke(Line);
        }

        /// <summary>
        /// Parses JSON text into a node for structured JSONL output.
        /// </summary>
        /// <param name="Json">Raw JSON text.</param>
        /// <returns>Parsed node, or a string fallback when parsing fails.</returns>
        static private JsonNode? ParseJsonNode
            (
            string? Json
            )
        {
            if (string.IsNullOrWhiteSpace(Json))
                return new JsonObject();

            try
            {
                return JsonNode.Parse(Json);
            }
            catch (JsonException)
            {
                return JsonValue.Create(Json);
            }
        }

        /// <summary>Shared JSON serializer options for trace output.</summary>
        static private readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>Marks the start of one user chat turn.</summary>
        private sealed record TurnStartRecord
        {
            /// <summary>Trace schema version.</summary>
            public int V { get; init; } = SCHEMA_VERSION;

            /// <summary>Record type discriminator.</summary>
            public string Event { get; init; } = "turn_start";

            /// <summary>Monotonic turn identifier.</summary>
            public int Turn { get; init; }

            /// <summary>User message that opened the turn.</summary>
            public required string UserMessage { get; init; }

            /// <summary>Project display name when available.</summary>
            public string? Project { get; init; }
        }

        /// <summary>Captures one executed tool call for replay.</summary>
        private sealed record ToolRecord
        {
            /// <summary>Trace schema version.</summary>
            public int V { get; init; } = SCHEMA_VERSION;

            /// <summary>Record type discriminator.</summary>
            public string Event { get; init; } = "tool";

            /// <summary>Monotonic turn identifier.</summary>
            public int Turn { get; init; }

            /// <summary>User message that opened the turn.</summary>
            public required string UserMessage { get; init; }

            /// <summary>Project display name when available.</summary>
            public string? Project { get; init; }

            /// <summary>Zero-based LLM completion round within the turn.</summary>
            public int LlmRound { get; init; }

            /// <summary>One-based tool index within the turn.</summary>
            public int Step { get; init; }

            /// <summary>Provider-assigned tool call identifier.</summary>
            public required string ToolCallId { get; init; }

            /// <summary>Surface tool name.</summary>
            public required string Tool { get; init; }

            /// <summary>Tool arguments JSON from the LLM.</summary>
            public JsonNode? Arguments { get; init; }

            /// <summary>True when C# tool execution succeeded.</summary>
            public bool Success { get; init; }

            /// <summary>Tool result JSON returned to the LLM.</summary>
            public JsonNode? Result { get; init; }

            /// <summary>Error message when <see cref="Success"/> is false.</summary>
            public string? Error { get; init; }
        }

        /// <summary>Marks the end of one user chat turn.</summary>
        private sealed record TurnEndRecord
        {
            /// <summary>Trace schema version.</summary>
            public int V { get; init; } = SCHEMA_VERSION;

            /// <summary>Record type discriminator.</summary>
            public string Event { get; init; } = "turn_end";

            /// <summary>Monotonic turn identifier.</summary>
            public int Turn { get; init; }

            /// <summary>Final assistant reply for the turn.</summary>
            public required string AssistantText { get; init; }

            /// <summary>Number of tool calls executed during the turn.</summary>
            public int ToolCount { get; init; }
        }
    }
}

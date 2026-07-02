using System.Text.Json;

namespace AgGradeDesigner.Core.Ai.Chat
{
    /// <summary>
    /// JSON Schema tool definition exposed to OpenAI-family chat APIs.
    /// </summary>
    public sealed class LlmToolDefinition
    {
        /// <summary>Surface tool name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Human-readable tool description for the model.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>JSON Schema parameters object.</summary>
        public JsonElement Parameters { get; set; }
    }
}

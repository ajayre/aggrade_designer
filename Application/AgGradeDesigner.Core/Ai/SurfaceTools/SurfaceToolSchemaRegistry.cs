using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.Chat;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Combines Phase 1 and Phase 2 LLM tool schemas for a delivery phase.
    /// </summary>
    static public class SurfaceToolSchemaRegistry
    {
        /// <summary>
        /// Returns all tool definitions registered for the requested delivery phase.
        /// </summary>
        /// <param name="Phase">Tool delivery phase.</param>
        /// <returns>OpenAI-style function tool schemas.</returns>
        static public IReadOnlyList<LlmToolDefinition> CreateDefinitions
            (
            AiDeliveryPhase Phase
            )
        {
            List<LlmToolDefinition> Definitions = new List<LlmToolDefinition>();
            Definitions.AddRange(Phase1SurfaceToolSchemas.CreateDefinitions());
            if (Phase >= AiDeliveryPhase.Phase2)
                Definitions.AddRange(Phase2SurfaceToolSchemas.CreateDefinitions());

            return Definitions;
        }
    }
}

using System.Text.Json;

using AgGradeDesigner.Core.Ai.Chat;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// JSON Schema definitions for Phase 1 read-only surface tools.
    /// </summary>
    static public class Phase1SurfaceToolSchemas
    {
        /// <summary>
        /// Returns tool definitions registered for Phase 1 chat requests.
        /// </summary>
        /// <returns>OpenAI-style function tool schemas.</returns>
        static public IReadOnlyList<LlmToolDefinition> CreateDefinitions
            (
            )
        {
            return new List<LlmToolDefinition>
            {
                Create(
                    "get_surface_summary",
                    "Return field extents, cell size, elevation and slope ranges, cut/fill balance, low/high points, and drainage heading for the proposed surface. Slope fields include m/m and percent; field_slope_percent and mean_slope_percent are the mean local slope across valid bins — use these when the user asks for 'the slope' or field grade without qualifiers. Use max_slope_percent or min_slope_percent only when they ask about steepest, maximum, minimum, or local extremes. Describe slopes to the user as percent only unless they ask otherwise.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {},
                          "required": []
                        }
                        """)),
                Create(
                    "get_elevation_at",
                    "Return the proposed surface elevation in metres at a UTM point (x, y).",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "x": { "type": "number", "description": "UTM easting in metres" },
                            "y": { "type": "number", "description": "UTM northing in metres" }
                          },
                          "required": ["x", "y"]
                        }
                        """)),
                Create(
                    "get_region_stats",
                    "Return field-wide elevation, slope, volume-above/below-reference, and drainage statistics for the entire proposed surface. Slope fields include m/m and percent; field_slope_percent and mean_slope_percent are the mean local slope across valid bins — use these when the user asks for 'the slope' or field grade without qualifiers. Use max_slope_percent or min_slope_percent only when they ask about steepest, maximum, minimum, or local extremes. Describe slopes to the user as percent only unless they ask otherwise.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {},
                          "required": []
                        }
                        """)),
                Create(
                    "calculate_cut_fill",
                    "Return cut and fill volumes in cubic metres between two surface layers.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "existing_surface": { "type": "string", "enum": ["existing", "proposed"], "description": "Reference surface layer" },
                            "design_surface": { "type": "string", "enum": ["existing", "proposed"], "description": "Comparison surface layer" }
                          },
                          "required": []
                        }
                        """)),
                Create(
                    "find_balance_offset",
                    "Return the vertical offset in metres needed on the proposed surface to achieve a target cut/fill ratio without applying it.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "target_cut_fill_ratio": { "type": "number", "description": "Desired cut-to-fill volume ratio" }
                          },
                          "required": []
                        }
                        """)),
                Create(
                    "compare_surfaces",
                    "Return diff statistics between two surface layers across the field.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "before": { "type": "string", "enum": ["existing", "proposed"], "description": "Before surface layer" },
                            "after": { "type": "string", "enum": ["existing", "proposed"], "description": "After surface layer" }
                          },
                          "required": []
                        }
                        """)),
                Create(
                    "get_tool_data",
                    "Return user-drawn map-editing tool points and polylines from the current session overlay. elevation_m is the proposed surface at creation and may differ from the current proposed surface.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {},
                          "required": []
                        }
                        """))
            };
        }

        /// <summary>
        /// Creates one tool definition record.
        /// </summary>
        /// <param name="Name">Tool name.</param>
        /// <param name="Description">Tool description.</param>
        /// <param name="Parameters">JSON Schema parameters object.</param>
        /// <returns>Tool definition.</returns>
        static private LlmToolDefinition Create
            (
            string Name,
            string Description,
            JsonElement Parameters
            )
        {
            return new LlmToolDefinition
            {
                Name = Name,
                Description = Description,
                Parameters = Parameters
            };
        }

        /// <summary>
        /// Parses a JSON Schema fragment into a <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="Json">Raw JSON text.</param>
        /// <returns>Parsed JSON element.</returns>
        static private JsonElement Parse
            (
            string Json
            )
        {
            using JsonDocument Document = JsonDocument.Parse(Json);
            return Document.RootElement.Clone();
        }
    }
}

using System.Text.Json;

using AgGradeDesigner.Core.Ai.Chat;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// JSON Schema definitions for Phase 3 road surface tools.
    /// </summary>
    static public class Phase3SurfaceToolSchemas
    {
        /// <summary>
        /// Returns tool definitions registered for Phase 3 road tools.
        /// </summary>
        /// <returns>OpenAI-style function tool schemas.</returns>
        static public IReadOnlyList<LlmToolDefinition> CreateDefinitions
            (
            )
        {
            return new List<LlmToolDefinition>
            {
                Create(
                    "create_road",
                    "Define a road corridor geometry from a UTM centerline polyline without changing elevations. Call get_field_geometry first when the user describes location in relative terms, then supply the computed centerline here. Returns a road_id for follow-up build_road calls.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "centerline": { "type": "array", "description": "Ordered UTM point objects with x and y in metres", "items": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] }, "minItems": 2 },
                            "start_point": { "type": "object", "description": "Straight-road shorthand: UTM start point", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] },
                            "end_point": { "type": "object", "description": "Straight-road shorthand: UTM end point", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] },
                            "width_m": { "type": "number", "description": "Full road crown width in metres" },
                            "name": { "type": "string", "description": "Optional road label" }
                          },
                          "required": ["width_m"]
                        }
                        """)),
                Create(
                    "build_road",
                    "Build a linear corridor (road or ditch) along a UTM centerline polyline. Positive vertical offset raises a road; negative offset or depth_below_proposed_m lowers a ditch. crown_width_m is the flat crown or ditch bottom width. Optional side slopes daylight cut or fill (side_slope_hv as H:V such as 2 for 2:1). Use side_slope_side for one-sided slopes relative to centerline travel direction. Creates a preview only.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "road_id": { "type": "string", "description": "Existing road id from create_road; omit when supplying a new centerline" },
                            "centerline": { "type": "array", "description": "Ordered UTM point objects with x and y in metres", "items": { "type": "object", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] }, "minItems": 2 },
                            "start_point": { "type": "object", "description": "Straight-road shorthand: UTM start point", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] },
                            "end_point": { "type": "object", "description": "Straight-road shorthand: UTM end point", "properties": { "x": { "type": "number" }, "y": { "type": "number" } }, "required": ["x", "y"] },
                            "crown_width_m": { "type": "number", "description": "Flat crown or ditch bottom width in metres" },
                            "vertical_offset_m": { "type": "number", "description": "Signed offset from proposed at centerline in metres. Positive = road raise; negative = ditch depth." },
                            "raise_above_proposed_m": { "type": "number", "description": "Road shorthand: positive raise above proposed in metres" },
                            "depth_below_proposed_m": { "type": "number", "description": "Ditch shorthand: positive depth below proposed in metres" },
                            "side_slope_hv": { "type": "number", "description": "Optional side slope as horizontal-to-vertical ratio, e.g. 2 for 2:1" },
                            "side_slope_percent": { "type": "number", "description": "Optional side slope as percent grade, e.g. 20 for 20%" },
                            "side_slope_side": { "type": "string", "enum": ["left", "right", "both"], "description": "Which side of the centerline receives side slope daylighting, relative to travel direction from first centerline vertex to last. Default 'both'. Use 'left' or 'right' for one-sided slopes." },
                            "longitudinal_grade_mm": { "type": "number", "description": "Optional grade along the centerline in m/m" },
                            "crown_crossfall_mm": { "type": "number", "description": "Optional crossfall from center in m/m" },
                            "align_crown_to_field_edge": { "type": "boolean", "description": "When true (default), offset the centerline inward from boundary_vertices so the outer edge of the flat section is flush with the field boundary. Set false for intentionally inset corridors." },
                            "name": { "type": "string", "description": "Optional corridor label when creating a new feature" }
                          },
                          "required": ["crown_width_m"]
                        }
                        """)),
                Create(
                    "calculate_road_volumes",
                    "Return cut/fill volumes in cubic metres for a registered road corridor. Read-only.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "road_id": { "type": "string", "description": "Registered road identifier such as rd_1" }
                          },
                          "required": ["road_id"]
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

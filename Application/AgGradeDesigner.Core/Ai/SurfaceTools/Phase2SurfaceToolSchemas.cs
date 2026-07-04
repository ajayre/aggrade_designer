using System.Text.Json;

using AgGradeDesigner.Core.Ai.Chat;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// JSON Schema definitions for Phase 2 edit-pipeline surface tools.
    /// </summary>
    static public class Phase2SurfaceToolSchemas
    {
        /// <summary>
        /// Returns tool definitions registered for Phase 2 chat requests.
        /// </summary>
        /// <returns>OpenAI-style function tool schemas.</returns>
        static public IReadOnlyList<LlmToolDefinition> CreateDefinitions
            (
            )
        {
            return new List<LlmToolDefinition>
            {
                Create(
                    "offset_surface",
                    "Raise or lower the proposed surface by a uniform vertical offset in metres. Creates a preview; does not commit until commit_surface_edit is called after explicit user approval in chat.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "delta_z": { "type": "number", "description": "Vertical shift added to proposed elevations in metres; positive raises" },
                            "polygon": {
                              "type": "object",
                              "properties": {
                                "vertices": {
                                  "type": "array",
                                  "minItems": 3,
                                  "items": {
                                    "type": "object",
                                    "properties": {
                                      "x": { "type": "number", "description": "UTM easting in metres" },
                                      "y": { "type": "number", "description": "UTM northing in metres" }
                                    },
                                    "required": ["x", "y"]
                                  }
                                }
                              },
                              "required": ["vertices"]
                            }
                          },
                          "required": ["delta_z"]
                        }
                        """)),
                Create(
                    "rebalance_surface",
                    "Apply the vertical offset needed to reach a target cut/fill ratio on the proposed surface. Creates a preview only.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "target_cut_fill_ratio": { "type": "number", "description": "Desired cut-to-fill volume ratio; must be positive" },
                            "polygon": {
                              "type": "object",
                              "properties": {
                                "vertices": {
                                  "type": "array",
                                  "minItems": 3,
                                  "items": {
                                    "type": "object",
                                    "properties": {
                                      "x": { "type": "number" },
                                      "y": { "type": "number" }
                                    },
                                    "required": ["x", "y"]
                                  }
                                }
                              },
                              "required": ["vertices"]
                            }
                          },
                          "required": []
                        }
                        """)),
                Create(
                    "apply_plane_grade",
                    "Set proposed elevations to a plane grade through an origin point with easting and northing slopes. Omit polygon to grade the full field.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "origin": {
                              "type": "object",
                              "properties": {
                                "x": { "type": "number", "description": "UTM easting in metres" },
                                "y": { "type": "number", "description": "UTM northing in metres" },
                                "z": { "type": "number", "description": "Anchor elevation in metres" }
                              },
                              "required": ["x", "y", "z"]
                            },
                            "slope_x": { "type": "number", "description": "Grade in easting direction in m/m" },
                            "slope_y": { "type": "number", "description": "Grade in northing direction in m/m" },
                            "polygon": {
                              "type": "object",
                              "properties": {
                                "vertices": {
                                  "type": "array",
                                  "minItems": 3,
                                  "items": {
                                    "type": "object",
                                    "properties": {
                                      "x": { "type": "number" },
                                      "y": { "type": "number" }
                                    },
                                    "required": ["x", "y"]
                                  }
                                }
                              },
                              "required": ["vertices"]
                            }
                          },
                          "required": ["origin", "slope_x", "slope_y"]
                        }
                        """)),
                Create(
                    "calculate_edit_delta",
                    "Return cut/fill volume delta between two surface layers. When a preview is active, after_surface proposed compares committed before vs preview proposed.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "before_surface": { "type": "string", "enum": ["existing", "proposed"], "description": "Reference surface layer" },
                            "after_surface": { "type": "string", "enum": ["existing", "proposed"], "description": "Comparison surface layer" }
                          },
                          "required": []
                        }
                        """)),
                Create(
                    "validate_surface_edit",
                    "Validate the current preview or committed proposed surface against optional constraints. Read-only.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "surface": { "type": "string", "enum": ["existing", "proposed"], "description": "Surface layer to validate" },
                            "constraints": {
                              "type": "object",
                              "properties": {
                                "max_slope_mm": { "type": "number" },
                                "min_slope_mm": { "type": "number" },
                                "max_cut_m3": { "type": "number" },
                                "max_fill_m3": { "type": "number" },
                                "max_net_export_m3": { "type": "number" },
                                "min_elevation_m": { "type": "number" },
                                "max_elevation_m": { "type": "number" }
                              },
                              "required": []
                            }
                          },
                          "required": []
                        }
                        """)),
                Create(
                    "commit_surface_edit",
                    "Commit the active preview to the project. Call only after the user explicitly instructs commit in chat. Commits are irreversible.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {
                            "description": { "type": "string", "description": "Short label echoed in chat" }
                          },
                          "required": []
                        }
                        """)),
                Create(
                    "discard_surface_edit",
                    "Discard the active preview and restore the proposed surface to its pre-preview state. Call when the user declines commit or asks to cancel the pending change.",
                    Parse("""
                        {
                          "type": "object",
                          "properties": {},
                          "required": []
                        }
                        """)),
                Create(
                    "preview_edit",
                    "Validate and preview an ordered edit plan without commit.",
                    Parse(EditPlanSchemaJson())),
                Create(
                    "apply_edit_plan",
                    "Validate and preview an ordered edit plan without commit. Prompt the user to commit in chat when appropriate.",
                    Parse(EditPlanSchemaJson()))
            };
        }

        /// <summary>
        /// Returns the shared edit_plan argument schema JSON text.
        /// </summary>
        /// <returns>Raw JSON schema fragment.</returns>
        static private string EditPlanSchemaJson
            (
            )
        {
            return """
                {
                  "type": "object",
                  "properties": {
                    "edit_plan": {
                      "type": "object",
                      "properties": {
                        "description": { "type": "string", "description": "Short human-readable plan label" },
                        "steps": {
                          "type": "array",
                          "minItems": 1,
                          "items": {
                            "type": "object",
                            "properties": {
                              "operation": { "type": "string", "description": "Phase 2 surface tool name" },
                              "delta_z": { "type": "number" },
                              "target_cut_fill_ratio": { "type": "number" },
                              "origin": { "type": "object" },
                              "slope_x": { "type": "number" },
                              "slope_y": { "type": "number" },
                              "polygon": { "type": "object" }
                            },
                            "required": ["operation"]
                          }
                        }
                      },
                      "required": ["steps"]
                    }
                  },
                  "required": ["edit_plan"]
                }
                """;
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

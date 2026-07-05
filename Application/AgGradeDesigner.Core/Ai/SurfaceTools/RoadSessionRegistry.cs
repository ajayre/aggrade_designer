using NetTopologySuite.Geometries;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Session-scoped road geometry and corridor metadata.
    /// </summary>
    public sealed class RoadDefinition
    {
        /// <summary>Stable road identifier such as rd_1.</summary>
        public string RoadId { get; init; } = string.Empty;

        /// <summary>Optional display name supplied by the user or LLM.</summary>
        public string? Name { get; init; }

        /// <summary>Road centerline in UTM metres.</summary>
        public LineString Centerline { get; init; } = null!;

        /// <summary>Full crown width in metres.</summary>
        public double CrownWidthM { get; init; }

        /// <summary>Corridor polygon covering the road influence area.</summary>
        public Polygon Corridor { get; init; } = null!;

        /// <summary>Centerline length in metres.</summary>
        public double CenterlineLengthM { get; init; }
    }

    /// <summary>
    /// Stores road definitions created during the current AI session.
    /// </summary>
    public sealed class RoadSessionRegistry
    {
        /// <summary>Registered roads keyed by road id.</summary>
        private readonly Dictionary<string, RoadDefinition> _Roads = new Dictionary<string, RoadDefinition>(StringComparer.Ordinal);

        /// <summary>Next numeric suffix used when allocating road ids.</summary>
        private int _NextRoadNumber = 1;

        /// <summary>
        /// Registers a road definition and returns its identifier.
        /// </summary>
        /// <param name="Definition">Road definition to store.</param>
        /// <returns>Registered road id.</returns>
        public string Register
            (
            RoadDefinition Definition
            )
        {
            _Roads[Definition.RoadId] = Definition;
            return Definition.RoadId;
        }

        /// <summary>
        /// Allocates a new road id such as rd_1.
        /// </summary>
        /// <returns>New road id.</returns>
        public string AllocateRoadId
            (
            )
        {
            return $"rd_{_NextRoadNumber++}";
        }

        /// <summary>
        /// Attempts to retrieve a registered road by id.
        /// </summary>
        /// <param name="RoadId">Road identifier.</param>
        /// <param name="Definition">Matching road definition.</param>
        /// <returns>True when the road exists.</returns>
        public bool TryGet
            (
            string RoadId,
            out RoadDefinition Definition
            )
        {
            return _Roads.TryGetValue(RoadId, out Definition!);
        }
    }
}

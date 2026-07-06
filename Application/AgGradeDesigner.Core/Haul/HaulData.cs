using System.Text.Json.Serialization;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Serializable cell/depth pair from a haul pass.
    /// </summary>
    public sealed class HaulPassCellDto
    {
        /// <summary>Grid column index.</summary>
        public int X { get; set; }

        /// <summary>Grid row index.</summary>
        public int Y { get; set; }

        /// <summary>Depth in metres.</summary>
        public double Depth { get; set; }
    }

    /// <summary>
    /// Serializable haul pass for project persistence and scrub rendering.
    /// </summary>
    public sealed class HaulPassDto
    {
        /// <summary>Cut cells consumed by this pass.</summary>
        public List<HaulPassCellDto> CutCells { get; set; } = new List<HaulPassCellDto>();

        /// <summary>Fill cells consumed by this pass.</summary>
        public List<HaulPassCellDto> FillCells { get; set; } = new List<HaulPassCellDto>();

        /// <summary>Moved load in bank cubic yards.</summary>
        public double LoadBcy { get; set; }

        /// <summary>Haul distance in metres.</summary>
        public double HaulDistanceM { get; set; }

        /// <summary>Cut sweep start easting/northing.</summary>
        public double CutStartX { get; set; }

        /// <summary>Cut sweep start northing.</summary>
        public double CutStartY { get; set; }

        /// <summary>Cut sweep end easting.</summary>
        public double CutEndX { get; set; }

        /// <summary>Cut sweep end northing.</summary>
        public double CutEndY { get; set; }

        /// <summary>Fill sweep start easting.</summary>
        public double FillStartX { get; set; }

        /// <summary>Fill sweep start northing.</summary>
        public double FillStartY { get; set; }

        /// <summary>Fill sweep end easting.</summary>
        public double FillEndX { get; set; }

        /// <summary>Fill sweep end northing.</summary>
        public double FillEndY { get; set; }
    }

    /// <summary>
    /// Serializable guidance waypoint for route symbology.
    /// </summary>
    public sealed class HaulGuidancePointDto
    {
        /// <summary>1-based global sequence number.</summary>
        public int Seq { get; set; }

        /// <summary>1-based pass number in route order.</summary>
        public int PassNo { get; set; }

        /// <summary>Inbound action code.</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>Blade state for the inbound leg.</summary>
        public string Blade { get; set; } = string.Empty;

        /// <summary>Smoothed steering easting in metres.</summary>
        public double X { get; set; }

        /// <summary>Smoothed steering northing in metres.</summary>
        public double Y { get; set; }

        /// <summary>Latitude in degrees.</summary>
        public double Lat { get; set; }

        /// <summary>Longitude in degrees.</summary>
        public double Lon { get; set; }

        /// <summary>Target blade depth in millimetres.</summary>
        public double DepthMm { get; set; }

        /// <summary>Heading to the next waypoint in degrees.</summary>
        public double HeadingDeg { get; set; }
    }

    /// <summary>
    /// Embedded haul payload stored in <c>.agproject</c> files (HAUL-SPEC Appendix A §A.2).
    /// </summary>
    public sealed class HaulPersistedPayload
    {
        /// <summary>Payload format version.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Route-ordered haul passes.</summary>
        public List<HaulPassDto> OrderedPasses { get; set; } = new List<HaulPassDto>();

        /// <summary>Blade-scale guidance track.</summary>
        public List<HaulGuidancePointDto> GuidancePoints { get; set; } = new List<HaulGuidancePointDto>();

        /// <summary>Pixels per grid cell edge used when rendering scrub frames.</summary>
        public int PixelsPerBin { get; set; } = HaulBuilder.Constants.DEFAULT_PIXELS_PER_BIN;

        /// <summary>Grid spacing in feet used for the run.</summary>
        public double GridSizeFt { get; set; } = HaulBuilder.Constants.DEFAULT_GRID_SIZE_FT;

        /// <summary>Informational messages emitted by the haul engine.</summary>
        public List<string> Messages { get; set; } = new List<string>();
    }

    /// <summary>
    /// In-memory haul data retained after generation or restored from a project file (§4.2).
    /// </summary>
    public sealed class HaulData
    {
        /// <summary>Live engine result when haul was generated in this session.</summary>
        [JsonIgnore]
        public HaulBuilder.HaulBuilderResult? Result { get; set; }

        /// <summary>Serializable payload written to project files.</summary>
        public HaulPersistedPayload Payload { get; set; } = new HaulPersistedPayload();

        /// <summary>Opaque field-boundary PNG generated once per in-memory haul session for scrub display.</summary>
        [JsonIgnore]
        public byte[]? FieldBoundaryPngBytes { get; set; }

        /// <summary>Geographic bounds matching the haul grid used for boundary placement.</summary>
        [JsonIgnore]
        public FieldBounds? FieldBoundaryBounds { get; set; }

        /// <summary>True when haul data is present and can be displayed.</summary>
        public bool IsValid => Payload.OrderedPasses.Count > 0;

        /// <summary>Number of discrete route scrub steps (<c>N</c> in HAUL-SPEC §4.4.2).</summary>
        public int RouteStepCount => Payload.OrderedPasses.Count;

        /// <summary>
        /// Builds persisted payload and retains the live result from an engine run.
        /// </summary>
        /// <param name="Result">Successful haul engine output.</param>
        /// <returns>Populated haul data.</returns>
        public static HaulData FromResult
            (
            HaulBuilder.HaulBuilderResult Result
            )
        {
            HaulData Data = new HaulData { Result = Result };
            Data.Payload.Messages.AddRange(Result.Messages);
            Data.Payload.PixelsPerBin = Result.Options?.PixelsPerBin ?? HaulBuilder.Constants.DEFAULT_PIXELS_PER_BIN;
            Data.Payload.GridSizeFt = Result.Options?.GridSizeFt ?? HaulBuilder.Constants.DEFAULT_GRID_SIZE_FT;

            IReadOnlyList<HaulBuilder.Pass> Passes = Result.Route?.OrderedPasses
                ?? Result.PassResult?.Passes
                ?? new List<HaulBuilder.Pass>();

            foreach (HaulBuilder.Pass Pass in Passes)
                Data.Payload.OrderedPasses.Add(HaulPassMapper.ToDto(Pass));

            if (Result.Guidance?.Points != null)
            {
                foreach (HaulBuilder.GuidancePoint Point in Result.Guidance.Points)
                    Data.Payload.GuidancePoints.Add(HaulGuidanceMapper.ToDto(Point));
            }
            else if (Result.GuidancePoints != null)
            {
                foreach (HaulBuilder.GuidancePoint Point in Result.GuidancePoints)
                    Data.Payload.GuidancePoints.Add(HaulGuidanceMapper.ToDto(Point));
            }

            return Data;
        }

        /// <summary>
        /// Resolves the processing grid for scrub rendering.
        /// </summary>
        /// <returns>Grid from the live result or rebuilt from the supplied AGD field.</returns>
        public HaulBuilder.Grid ResolveGrid
            (
            HaulBuilder.AgdField Field
            )
        {
            if (Result?.Grid != null)
                return Result.Grid;

            return HaulBuilder.GridMath.BuildGrid(
                Field,
                Payload.GridSizeFt,
                HaulBuilder.Constants.DEFAULT_SWELL_FACTOR,
                HaulBuilder.Constants.DEFAULT_COMPACTION,
                HaulBuilder.Constants.DEFAULT_MAX_DEPTH_MM);
        }

        /// <summary>
        /// Returns ordered passes for scrub rendering.
        /// </summary>
        /// <returns>Live passes when available, otherwise DTO-mapped passes.</returns>
        public IReadOnlyList<HaulBuilder.Pass> ResolveOrderedPasses
            (
            )
        {
            if (Result?.Route?.OrderedPasses != null && Result.Route.OrderedPasses.Count > 0)
                return Result.Route.OrderedPasses;

            if (Result?.PassResult?.Passes != null && Result.PassResult.Passes.Count > 0)
                return Result.PassResult.Passes;

            return Payload.OrderedPasses.Select(HaulPassMapper.FromDto).ToList();
        }

        /// <summary>
        /// Returns guidance points for route symbology.
        /// </summary>
        /// <returns>Live guidance when available, otherwise DTO-mapped points.</returns>
        public IReadOnlyList<HaulBuilder.GuidancePoint> ResolveGuidancePoints
            (
            )
        {
            if (Result?.Guidance?.Points != null && Result.Guidance.Points.Count > 0)
                return Result.Guidance.Points;

            if (Result?.GuidancePoints != null && Result.GuidancePoints.Count > 0)
                return Result.GuidancePoints;

            return Payload.GuidancePoints.Select(HaulGuidanceMapper.FromDto).ToList();
        }

        /// <summary>
        /// Builds and persists guidance waypoints when missing (for example after project reload).
        /// </summary>
        /// <param name="Project">Open project supplying survey and design.</param>
        /// <param name="Settings">Active haul settings.</param>
        public void EnsureGuidanceForDisplay
            (
            ProjectDocument Project,
            HaulSettings Settings
            )
        {
            HaulGuidanceBuilder.EnsureOnData(this, Project, Settings);
        }
    }
}

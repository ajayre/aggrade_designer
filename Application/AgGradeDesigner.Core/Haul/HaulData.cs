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

        /// <summary>
        /// When true, <see cref="CutCells"/> and <see cref="FillCells"/> are already a blade-wide flow ribbon.
        /// Guidance follows that order instead of tiling the cells into the older lanes.
        /// </summary>
        public bool RibbonOrder { get; set; }

        /// <summary>
        /// Which builder produced this trip. Road trips are drawn from the haul section
        /// rather than clamped onto the field cells. Missing values stay <see cref="HaulBuilder.HaulStage.Flow"/>.
        /// </summary>
        public HaulBuilder.HaulStage Stage { get; set; }
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

        /// <summary>Route-ordered haul passes. This list stays in planner order.</summary>
        public List<HaulPassDto> OrderedPasses { get; set; } = new List<HaulPassDto>();

        /// <summary>
        /// Planner-pass indexes in the order the trips are driven.
        /// Empty until drive order has been built; display then uses planner order.
        /// </summary>
        public List<int> DriveOrder { get; set; } = new List<int>();

        /// <summary>Blade-scale guidance track.</summary>
        public List<HaulGuidancePointDto> GuidancePoints { get; set; } = new List<HaulGuidancePointDto>();

        /// <summary>Pixels per grid cell edge used when rendering scrub frames.</summary>
        public int PixelsPerBin { get; set; } = HaulBuilder.Constants.DEFAULT_PIXELS_PER_BIN;

        /// <summary>Grid spacing in feet used for the run.</summary>
        public double GridSizeFt { get; set; } = HaulBuilder.Constants.DEFAULT_GRID_SIZE_FT;

        /// <summary>Informational messages emitted by the haul engine.</summary>
        public List<string> Messages { get; set; } = new List<string>();

        /// <summary>Persisted transport flow field for haul flow-arrow overlays.</summary>
        public HaulFlowFieldDto? FlowField { get; set; }

        /// <summary>Arrow subsampling stride in grid cells used when flow arrows were computed.</summary>
        public int ArrowStride { get; set; } = HaulFlowFieldResolver.DEFAULT_ARROW_STRIDE;

        /// <summary>Route-ordered cut/fill pair summaries for haul plan statistics.</summary>
        public List<HaulCutFillPairDto> CutFillPairs { get; set; } = new List<HaulCutFillPairDto>();

        /// <summary>Center-pivot overlay radius and per-pass bearings for haul 2D display.</summary>
        public HaulPivotOverlayDto? PivotOverlay { get; set; }

        /// <summary>Guidance-track time estimate for haul plan total time display.</summary>
        public HaulGuidanceTimeDto? GuidanceTime { get; set; }
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

        /// <summary>Number of discrete route scrub steps. Drive order sets the count once it exists.</summary>
        public int RouteStepCount =>
            Payload.DriveOrder.Count > 0 ? Payload.DriveOrder.Count : Payload.OrderedPasses.Count;

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
            Data.Payload.PixelsPerBin = Result.Options?.PixelsPerCell ?? HaulBuilder.Constants.DEFAULT_PIXELS_PER_BIN;
            Data.Payload.GridSizeFt = Result.Options?.GridSizeFt ?? HaulBuilder.Constants.DEFAULT_GRID_SIZE_FT;

            IReadOnlyList<HaulBuilder.Pass> Passes = Result.Route?.OrderedPasses
                ?? Result.PassResult?.Passes
                ?? new List<HaulBuilder.Pass>();

            foreach (HaulBuilder.Pass Pass in Passes)
                Data.Payload.OrderedPasses.Add(HaulPassMapper.ToDto(Pass));

            if (Result.CutFillPairs != null)
            {
                foreach (HaulBuilder.CutFillPair Pair in Result.CutFillPairs)
                    Data.Payload.CutFillPairs.Add(HaulCutFillPairMapper.ToDto(Pair));

                // intent: pair rows are stamped with the old time model; overlay guidance route times before save
                if (Result.DetailedGuidanceTime != null)
                    HaulGuidanceTimeMapper.ApplyRouteTimes(Data.Payload.CutFillPairs, Result.DetailedGuidanceTime);
            }

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

            Data.Payload.FlowField = HaulFlowFieldCodec.Encode(Result.Grid, Result.Flow);
            Data.Payload.ArrowStride = Math.Max(
                1,
                Result.Options?.FlowArrowStrideCells ?? HaulFlowFieldResolver.DEFAULT_ARROW_STRIDE);
            Data.Payload.PivotOverlay = EncodePivotOverlay(Result);
            Data.Payload.GuidanceTime = HaulGuidanceTimeMapper.ToDto(Result);

            return Data;
        }

        /// <summary>
        /// Encodes pivot overlay data from a live engine result when pivot geometry is present.
        /// </summary>
        /// <param name="Result">Successful haul engine output.</param>
        /// <returns>Overlay DTO, or null when the run has no pivot geometry.</returns>
        private static HaulPivotOverlayDto? EncodePivotOverlay
            (
            HaulBuilder.HaulBuilderResult Result
            )
        {
            HaulBuilder.Pivot.PivotPlan? Plan = Result.PivotPlan;
            HaulBuilder.Pivot.PivotGeometry? Geometry = Result.PivotGeometry ?? Plan?.Geometry;
            if (Geometry == null || Geometry.RadiusM <= 0.0)
                return null;

            double[] Bearings = Plan?.BearingByPass ?? Array.Empty<double>();
            HaulPivotOverlayDto Overlay = new HaulPivotOverlayDto
            {
                RadiusM = Geometry.RadiusM,
                BearingByPassDeg = Bearings.Length == 0 ? Array.Empty<double>() : (double[])Bearings.Clone()
            };

            if (Plan?.Positions != null)
            {
                foreach (HaulBuilder.Pivot.PivotPosition Position in Plan.Positions)
                {
                    Overlay.Moves.Add(new HaulPivotMoveDto
                    {
                        Index = Position.Index,
                        FirstRoute = Position.FirstPass,
                        LastRoute = Position.LastPass,
                        BearingDeg = Position.BearingDeg,
                        MoveDeg = Position.MoveDeg
                    });
                }
            }

            // intent: rebuild per-route bearings from the move schedule when BearingByPass was empty
            if (Overlay.BearingByPassDeg.Length == 0 && Overlay.Moves.Count > 0)
                Overlay.BearingByPassDeg = ExpandBearingsFromMoves(Overlay.Moves);

            return Overlay;
        }

        /// <summary>
        /// Expands held-bearing move ranges into a per-route bearing array.
        /// </summary>
        /// <param name="Moves">Contiguous held-bearing positions.</param>
        /// <returns>Zero-based route-ordered bearings.</returns>
        private static double[] ExpandBearingsFromMoves
            (
            IReadOnlyList<HaulPivotMoveDto> Moves
            )
        {
            int MaxRoute = 0;
            foreach (HaulPivotMoveDto Move in Moves)
                MaxRoute = Math.Max(MaxRoute, Move.LastRoute);

            if (MaxRoute <= 0)
                return Array.Empty<double>();

            double[] Bearings = new double[MaxRoute];
            foreach (HaulPivotMoveDto Move in Moves)
            {
                int First = Math.Max(1, Move.FirstRoute);
                int Last = Math.Max(First, Move.LastRoute);
                for (int Route = First; Route <= Last && Route <= MaxRoute; Route++)
                    Bearings[Route - 1] = Move.BearingDeg;
            }

            return Bearings;
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
        /// Returns the 0-based planner pass index shown at a drive step.
        /// </summary>
        /// <param name="DriveIndex">Zero-based position in drive order.</param>
        /// <returns>Planner pass index. Without a drive order this is the same index.</returns>
        public int PlannerPassIndexForDriveStep
            (
            int DriveIndex
            )
        {
            if (Payload.DriveOrder.Count == 0)
                return Math.Max(0, DriveIndex);

            int Index = Math.Clamp(DriveIndex, 0, Payload.DriveOrder.Count - 1);
            return Payload.DriveOrder[Index];
        }

        /// <summary>
        /// Returns the 1-based planner pass number drawn at a drive step.
        /// Guidance waypoints keep this number, so drive step 2 can still be trip 123.
        /// </summary>
        /// <param name="DriveIndex">Zero-based position in drive order.</param>
        /// <returns>Planner pass number stored on guidance waypoints.</returns>
        public int PlannerPassNumberForDriveStep
            (
            int DriveIndex
            )
        {
            return PlannerPassIndexForDriveStep(DriveIndex) + 1;
        }

        /// <summary>
        /// Returns the passes already driven before a drive step.
        /// </summary>
        /// <param name="DriveIndex">Zero-based position in drive order.</param>
        /// <returns>Planner passes in the drive sequence that precedes this step.</returns>
        public IReadOnlyList<HaulBuilder.Pass> PassesBeforeDriveStep
            (
            int DriveIndex
            )
        {
            IReadOnlyList<HaulBuilder.Pass> Passes = ResolveOrderedPasses();
            if (DriveIndex <= 0 || Passes.Count == 0)
                return Array.Empty<HaulBuilder.Pass>();

            if (Payload.DriveOrder.Count == 0)
            {
                int Count = Math.Min(DriveIndex, Passes.Count);
                return Passes.Take(Count).ToList();
            }

            List<HaulBuilder.Pass> Driven = new List<HaulBuilder.Pass>();
            int Limit = Math.Min(DriveIndex, Payload.DriveOrder.Count);
            for (int Step = 0; Step < Limit; Step++)
            {
                int PlannerIndex = Payload.DriveOrder[Step];
                if (PlannerIndex >= 0 && PlannerIndex < Passes.Count)
                    Driven.Add(Passes[PlannerIndex]);
            }

            return Driven;
        }

        /// <summary>
        /// Returns ordered passes for scrub rendering.
        /// </summary>
        /// <returns>Live passes when available, otherwise DTO-mapped passes. The list stays in planner order.</returns>
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
        /// Returns cut/fill pair summaries in drive order for the haul plan overlay.
        /// </summary>
        /// <returns>Pairs sequenced by <see cref="HaulPersistedPayload.DriveOrder"/>, with volumes recounted along that sequence.</returns>
        public IReadOnlyList<HaulCutFillPairDto> ResolveCutFillPairsInDriveOrder
            (
            )
        {
            IReadOnlyList<HaulCutFillPairDto> Pairs = ResolveCutFillPairs();
            if (Payload.DriveOrder.Count == 0)
                return Pairs;

            // intent: "before this route" must follow the trips already driven, not the planner list
            List<HaulCutFillPairDto> Driven = new List<HaulCutFillPairDto>(Payload.DriveOrder.Count);
            double CutBeforeBcy = 0.0;
            double FillBeforeBcy = 0.0;
            foreach (int PlannerIndex in Payload.DriveOrder)
            {
                if (PlannerIndex < 0 || PlannerIndex >= Pairs.Count)
                    continue;

                HaulCutFillPairDto Source = Pairs[PlannerIndex];
                Driven.Add(new HaulCutFillPairDto
                {
                    Order = Source.Order,
                    LoadBcy = Source.LoadBcy,
                    HaulDistanceM = Source.HaulDistanceM,
                    TotalCutBcyBefore = CutBeforeBcy,
                    TotalFillBcyBefore = FillBeforeBcy,
                    RouteTimeSeconds = Source.RouteTimeSeconds
                });
                CutBeforeBcy += Source.LoadBcy;
                FillBeforeBcy += Source.LoadBcy;
            }

            return Driven;
        }

        /// <summary>
        /// Returns route-ordered cut/fill pairs for haul plan statistics.
        /// </summary>
        /// <returns>Live pairs when available, otherwise persisted or synthesized pairs. The list stays in planner order.</returns>
        public IReadOnlyList<HaulCutFillPairDto> ResolveCutFillPairs
            (
            )
        {
            if (Result?.CutFillPairs != null && Result.CutFillPairs.Count > 0)
            {
                List<HaulCutFillPairDto> LivePairs = new List<HaulCutFillPairDto>(Result.CutFillPairs.Count);
                foreach (HaulBuilder.CutFillPair Pair in Result.CutFillPairs)
                    LivePairs.Add(HaulCutFillPairMapper.ToDto(Pair));

                // intent: live pair times are the old time model until guidance route seconds replace them
                if (Result.DetailedGuidanceTime != null)
                    HaulGuidanceTimeMapper.ApplyRouteTimes(LivePairs, Result.DetailedGuidanceTime);

                return LivePairs;
            }

            if (Payload.CutFillPairs.Count > 0)
                return Payload.CutFillPairs;

            return HaulCutFillPairMapper.SynthesizeFromPasses(Payload.OrderedPasses);
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
        /// Returns the guidance-track time estimate for haul plan statistics.
        /// </summary>
        /// <returns>
        /// Production guidance time when the live result has it, otherwise the older time model, otherwise the persisted payload.
        /// </returns>
        public HaulGuidanceTimeDto? ResolveGuidanceTime
            (
            )
        {
            if (Result != null)
            {
                HaulGuidanceTimeDto? Live = HaulGuidanceTimeMapper.ToDto(Result);
                if (Live != null)
                    return Live;
            }

            return Payload.GuidanceTime;
        }

        /// <summary>
        /// Returns center-pivot overlay data for haul 2D display.
        /// </summary>
        /// <returns>Live pivot overlay when available, otherwise the persisted payload.</returns>
        public HaulPivotOverlayDto? ResolvePivotOverlay
            (
            )
        {
            if (Result != null)
            {
                HaulPivotOverlayDto? Live = EncodePivotOverlay(Result);
                if (Live != null)
                    return Live;
            }

            HaulPivotOverlayDto? Persisted = Payload.PivotOverlay;
            if (Persisted == null)
                return null;

            // intent: older payloads may have moves without BearingByPass — expand for scrub display
            if (Persisted.BearingByPassDeg.Length == 0 && Persisted.Moves.Count > 0)
            {
                Persisted.BearingByPassDeg = ExpandBearingsFromMoves(Persisted.Moves);
            }

            return Persisted;
        }

        /// <summary>
        /// Builds and persists guidance waypoints and time when missing (for example after project reload).
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
            HaulDriveOrderBuilder.Ensure(this, Project, Settings);
        }
    }
}

using System.Globalization;
using System.Text;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Captures per-sample outcomes while <see cref="HaulSurfaceBuilder"/> stamps haul elevations onto a 3D sampler.
    /// </summary>
    public sealed class HaulThreeDOverlayDiagnosticsCollector
    {
        /// <summary>Per-sample overlay outcome recorded during the overlay loop.</summary>
        public enum SampleOutcome : byte
        {
            /// <summary>Elevation was replaced by a leveled haul value.</summary>
            Updated = 0,

            /// <summary>Sample lies outside the haul grid UTM envelope.</summary>
            OutsideHaulUtmBox = 1,

            /// <summary>Sample UTM zone or hemisphere does not match the haul grid.</summary>
            WrongUtmZone = 2,

            /// <summary>Sample is inside the haul grid envelope but no valid haul cell could be sampled.</summary>
            SampleFailed = 3,

            /// <summary>Overlay ran but the elevation did not change (within tolerance).</summary>
            Unchanged = 4,
        }

        /// <summary>Elevation delta treated as unchanged when applying haul overlay diagnostics.</summary>
        private const double UNCHANGED_ELEVATION_TOLERANCE_M = 0.001;

        /// <summary>Sample width captured when collection started.</summary>
        private int _SampleWidth;

        /// <summary>Sample height captured when collection started.</summary>
        private int _SampleHeight;

        /// <summary>Existing elevations before overlay, indexed like the sampler flat array.</summary>
        private double[] _BeforeExisting = Array.Empty<double>();

        /// <summary>Per-sample overlay outcome for valid sampler cells.</summary>
        internal SampleOutcome[] Outcomes = Array.Empty<SampleOutcome>();

        /// <summary>Whether this collector is actively recording overlay samples.</summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Begins recording overlay outcomes for the supplied sampler.
        /// </summary>
        /// <param name="Sampler">Surface sampler about to receive haul overlay.</param>
        public void Begin
            (
            FieldSurfaceSampler Sampler
            )
        {
            int Count = Sampler.SampleWidth * Sampler.SampleHeight;
            _SampleWidth = Sampler.SampleWidth;
            _SampleHeight = Sampler.SampleHeight;
            _BeforeExisting = (double[])Sampler.Existing.Clone();
            Outcomes = new SampleOutcome[Count];
            IsActive = true;
        }

        /// <summary>
        /// Records one overlay attempt for a valid sampler cell.
        /// </summary>
        /// <param name="SampleIndex">Flat sampler index.</param>
        /// <param name="Outcome">Overlay classification for the sample.</param>
        /// <param name="AfterElevation">Elevation after overlay when updated.</param>
        public void Record
            (
            int SampleIndex,
            SampleOutcome Outcome,
            double AfterElevation
            )
        {
            if (!IsActive || SampleIndex < 0 || SampleIndex >= Outcomes.Length)
                return;

            if (Outcome == SampleOutcome.Updated &&
                Math.Abs(_BeforeExisting[SampleIndex] - AfterElevation) <= UNCHANGED_ELEVATION_TOLERANCE_M)
            {
                Outcome = SampleOutcome.Unchanged;
            }

            Outcomes[SampleIndex] = Outcome;
        }

        /// <summary>
        /// Builds a diagnostic snapshot from recorded overlay outcomes.
        /// </summary>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Project">Open project used for field and grid bounds.</param>
        /// <param name="RouteScrubIndex">Route scrub index applied to the overlay.</param>
        /// <param name="Sampler">Surface sampler after overlay.</param>
        /// <param name="LeveledGrid">Leveled haul grid used for overlay.</param>
        /// <returns>Structured overlay diagnostics snapshot.</returns>
        internal HaulThreeDOverlayDiagnosticsSnapshot BuildSnapshot
            (
            HaulData Data,
            ProjectDocument Project,
            int RouteScrubIndex,
            FieldSurfaceSampler Sampler,
            HaulBuilder.Grid LeveledGrid
            )
        {
            IsActive = false;
            return HaulThreeDOverlayDiagnostics.BuildSnapshot(
                this,
                Data,
                Project,
                RouteScrubIndex,
                Sampler,
                LeveledGrid);
        }
    }

    /// <summary>
    /// Structured measurements comparing haul-grid overlay coverage to viewer surface extents.
    /// </summary>
    public readonly struct HaulThreeDOverlayDiagnosticsSnapshot
    {
        /// <summary>Route scrub index used for the overlay.</summary>
        public int RouteScrubIndex { get; init; }

        /// <summary>Haul grid width in cells.</summary>
        public int HaulGridWidth { get; init; }

        /// <summary>Haul grid height in cells.</summary>
        public int HaulGridHeight { get; init; }

        /// <summary>Haul grid cell size in metres.</summary>
        public double HaulGridSizeM { get; init; }

        /// <summary>Haul grid cell size in feet.</summary>
        public double HaulGridSizeFt { get; init; }

        /// <summary>Count of valid cells in the haul processing grid.</summary>
        public int HaulValidCellCount { get; init; }

        /// <summary>Count of invalid cells on the haul grid outer perimeter band.</summary>
        public int HaulPerimeterInvalidCellCount { get; init; }

        /// <summary>Viewer sampler width in samples.</summary>
        public int SamplerWidth { get; init; }

        /// <summary>Viewer sampler height in samples.</summary>
        public int SamplerHeight { get; init; }

        /// <summary>Count of valid samples in the viewer surface sampler.</summary>
        public int ValidSamplerCount { get; init; }

        /// <summary>Count of valid samples updated by haul overlay.</summary>
        public int UpdatedSampleCount { get; init; }

        /// <summary>Count of valid samples outside the haul grid UTM envelope.</summary>
        public int OutsideHaulUtmBoxCount { get; init; }

        /// <summary>Count of valid samples rejected due to UTM zone or hemisphere mismatch.</summary>
        public int WrongUtmZoneCount { get; init; }

        /// <summary>Count of valid samples inside the haul envelope where haul sampling failed.</summary>
        public int SampleFailedCount { get; init; }

        /// <summary>Count of valid samples where overlay succeeded but elevation did not change.</summary>
        public int UnchangedSampleCount { get; init; }

        /// <summary>Count of valid samples that did not receive haul overlay.</summary>
        public int NotUpdatedValidCount { get; init; }

        /// <summary>Approximate border width in metres on the north side.</summary>
        public double EstimatedBorderWidthNorthM { get; init; }

        /// <summary>Approximate border width in metres on the south side.</summary>
        public double EstimatedBorderWidthSouthM { get; init; }

        /// <summary>Approximate border width in metres on the west side.</summary>
        public double EstimatedBorderWidthWestM { get; init; }

        /// <summary>Approximate border width in metres on the east side.</summary>
        public double EstimatedBorderWidthEastM { get; init; }

        /// <summary>Median distance in metres from not-updated valid samples to the nearest valid haul cell.</summary>
        public double MedianDistanceToValidHaulCellM { get; init; }

        /// <summary>Maximum distance in metres from not-updated valid samples to the nearest valid haul cell.</summary>
        public double MaxDistanceToValidHaulCellM { get; init; }

        /// <summary>Project field bounds derived from design grid centroids.</summary>
        public FieldBounds ProjectFieldBounds { get; init; }

        /// <summary>Geographic bounds of the haul processing grid.</summary>
        public FieldBounds HaulGridBounds { get; init; }

        /// <summary>Geographic bounds of the viewer surface sampler.</summary>
        public FieldBounds SamplerBounds { get; init; }

        /// <summary>Extent delta in metres between sampler and haul grid on each side.</summary>
        public HaulThreeDOverlayExtentDelta ExtentDeltaSamplerMinusHaulGridM { get; init; }

        /// <summary>Extent delta in metres between project field bounds and haul grid on each side.</summary>
        public HaulThreeDOverlayExtentDelta ExtentDeltaProjectMinusHaulGridM { get; init; }

        /// <summary>
        /// Returns a multi-line human-readable report suitable for logs and test output.
        /// </summary>
        /// <returns>Formatted diagnostic report.</returns>
        public string FormatReport
            (
            )
        {
            return HaulThreeDOverlayDiagnostics.FormatReport(this);
        }

        /// <summary>
        /// Returns one compact JSON object suitable for JSONL logging.
        /// </summary>
        /// <returns>Single-line JSON payload.</returns>
        public string FormatJsonLine
            (
            )
        {
            return HaulThreeDOverlayDiagnostics.FormatJsonLine(this);
        }
    }

    /// <summary>
    /// Side-by-side geographic extent deltas expressed in metres.
    /// </summary>
    public readonly struct HaulThreeDOverlayExtentDelta
    {
        /// <summary>Positive when the outer extent reaches further north than the inner extent.</summary>
        public double NorthM { get; init; }

        /// <summary>Positive when the outer extent reaches further south than the inner extent.</summary>
        public double SouthM { get; init; }

        /// <summary>Positive when the outer extent reaches further east than the inner extent.</summary>
        public double EastM { get; init; }

        /// <summary>Positive when the outer extent reaches further west than the inner extent.</summary>
        public double WestM { get; init; }

        /// <summary>
        /// Returns a compact summary of the extent delta on all four sides.
        /// </summary>
        /// <returns>Formatted extent delta string.</returns>
        public string Describe
            (
            )
        {
            return
                $"N={NorthM:0.###}m S={SouthM:0.###}m E={EastM:0.###}m W={WestM:0.###}m";
        }
    }

    /// <summary>
    /// Computes haul 3D overlay coverage diagnostics for border-width analysis.
    /// </summary>
    public static class HaulThreeDOverlayDiagnostics
    {
        /// <summary>Metres per degree latitude used for quick extent approximations.</summary>
        private const double METRES_PER_DEGREE_LATITUDE = 111_320.0;

        /// <summary>Receives diagnostic lines when the application debug log is enabled.</summary>
        public static Action<string, string, string?>? WriteSink { get; set; }

        /// <summary>
        /// Runs haul overlay analysis on an isolated sampler clone for tests and tooling.
        /// </summary>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Project">Open project containing survey and design.</param>
        /// <param name="RouteScrubIndex">Route scrub index to analyze.</param>
        /// <returns>Overlay diagnostics snapshot.</returns>
        public static HaulThreeDOverlayDiagnosticsSnapshot Analyze
            (
            HaulData Data,
            ProjectDocument Project,
            int RouteScrubIndex
            )
        {
            FieldSurfaceSampler Sampler = new FieldSurfaceSampler();
            Sampler.BuildFromProject(Project);

            HaulBuilder.Grid LeveledGrid = HaulSurfaceBuilder.ResolveLeveledGridForDiagnostics(
                Data,
                Project,
                RouteScrubIndex);

            HaulThreeDOverlayDiagnosticsCollector Collector = new HaulThreeDOverlayDiagnosticsCollector();
            Collector.Begin(Sampler);
            HaulSurfaceBuilder.OverlayLeveledExistingOntoSampler(Sampler, LeveledGrid, Collector);
            return Collector.BuildSnapshot(Data, Project, RouteScrubIndex, Sampler, LeveledGrid);
        }

        /// <summary>
        /// Builds a diagnostic snapshot from a completed collector.
        /// </summary>
        /// <param name="Collector">Completed overlay collector.</param>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Project">Open project used for field and grid bounds.</param>
        /// <param name="RouteScrubIndex">Route scrub index applied to the overlay.</param>
        /// <param name="Sampler">Surface sampler after overlay.</param>
        /// <returns>Structured overlay diagnostics snapshot.</returns>
        public static HaulThreeDOverlayDiagnosticsSnapshot CompleteCapture
            (
            HaulThreeDOverlayDiagnosticsCollector Collector,
            HaulData Data,
            ProjectDocument Project,
            int RouteScrubIndex,
            FieldSurfaceSampler Sampler
            )
        {
            HaulBuilder.Grid LeveledGrid = HaulSurfaceBuilder.ResolveLeveledGridForDiagnostics(
                Data,
                Project,
                RouteScrubIndex);
            return Collector.BuildSnapshot(Data, Project, RouteScrubIndex, Sampler, LeveledGrid);
        }

        /// <summary>
        /// Logs one overlay diagnostics snapshot through the configured sink.
        /// </summary>
        /// <param name="Snapshot">Diagnostics snapshot to record.</param>
        public static void Log
            (
            HaulThreeDOverlayDiagnosticsSnapshot Snapshot
            )
        {
            Write("summary", Snapshot.FormatReport().Replace('\n', '|'));
            Write("json", Snapshot.FormatJsonLine());
        }

        /// <summary>
        /// Writes a multi-line diagnostic report to a text file.
        /// </summary>
        /// <param name="Snapshot">Diagnostics snapshot to serialize.</param>
        /// <param name="Path">Destination file path.</param>
        public static void WriteReportToFile
            (
            HaulThreeDOverlayDiagnosticsSnapshot Snapshot,
            string Path
            )
        {
            File.WriteAllText(Path, Snapshot.FormatReport(), Encoding.UTF8);
        }

        /// <summary>
        /// Builds a diagnostic snapshot from a completed collector.
        /// </summary>
        /// <param name="Collector">Completed overlay collector.</param>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Project">Open project used for field and grid bounds.</param>
        /// <param name="RouteScrubIndex">Route scrub index applied to the overlay.</param>
        /// <param name="Sampler">Surface sampler after overlay.</param>
        /// <param name="LeveledGrid">Leveled haul grid used for overlay.</param>
        /// <returns>Structured overlay diagnostics snapshot.</returns>
        internal static HaulThreeDOverlayDiagnosticsSnapshot BuildSnapshot
            (
            HaulThreeDOverlayDiagnosticsCollector Collector,
            HaulData Data,
            ProjectDocument Project,
            int RouteScrubIndex,
            FieldSurfaceSampler Sampler,
            HaulBuilder.Grid LeveledGrid
            )
        {
            FieldBounds ProjectBounds = default;
            FieldBounds.TryFromProject(Project, out ProjectBounds);
            FieldBounds HaulBounds = HaulScrubFrameBuilder.ComputeGridBounds(LeveledGrid);
            FieldBounds SamplerBounds = new FieldBounds
            {
                MinLat = Sampler.MinLat,
                MaxLat = Sampler.MaxLat,
                MinLon = Sampler.MinLon,
                MaxLon = Sampler.MaxLon,
            };

            CountHaulGridMask(
                LeveledGrid,
                out int ValidCellCount,
                out int PerimeterInvalidCellCount);
            CountSampleOutcomes(
                Collector,
                Sampler,
                out int ValidSamplerCount,
                out int UpdatedSampleCount,
                out int OutsideHaulUtmBoxCount,
                out int WrongUtmZoneCount,
                out int SampleFailedCount,
                out int UnchangedSampleCount,
                out int NotUpdatedValidCount);

            ComputeDistanceToValidHaulCellStats(
                Collector,
                Sampler,
                LeveledGrid,
                out double MedianDistanceM,
                out double MaxDistanceM,
                out double BorderNorthM,
                out double BorderSouthM,
                out double BorderWestM,
                out double BorderEastM);

            double MeanLat = (SamplerBounds.MinLat + SamplerBounds.MaxLat) * 0.5;
            HaulThreeDOverlayExtentDelta SamplerMinusHaul = ComputeExtentDelta(
                SamplerBounds,
                HaulBounds,
                MeanLat);
            HaulThreeDOverlayExtentDelta ProjectMinusHaul = ComputeExtentDelta(
                ProjectBounds,
                HaulBounds,
                MeanLat);

            return new HaulThreeDOverlayDiagnosticsSnapshot
            {
                RouteScrubIndex = RouteScrubIndex,
                HaulGridWidth = LeveledGrid.Width,
                HaulGridHeight = LeveledGrid.Height,
                HaulGridSizeM = LeveledGrid.GridSizeM,
                HaulGridSizeFt = LeveledGrid.GridSizeM / HaulBuilder.Constants.FEET_TO_METERS,
                HaulValidCellCount = ValidCellCount,
                HaulPerimeterInvalidCellCount = PerimeterInvalidCellCount,
                SamplerWidth = Sampler.SampleWidth,
                SamplerHeight = Sampler.SampleHeight,
                ValidSamplerCount = ValidSamplerCount,
                UpdatedSampleCount = UpdatedSampleCount,
                OutsideHaulUtmBoxCount = OutsideHaulUtmBoxCount,
                WrongUtmZoneCount = WrongUtmZoneCount,
                SampleFailedCount = SampleFailedCount,
                UnchangedSampleCount = UnchangedSampleCount,
                NotUpdatedValidCount = NotUpdatedValidCount,
                EstimatedBorderWidthNorthM = BorderNorthM,
                EstimatedBorderWidthSouthM = BorderSouthM,
                EstimatedBorderWidthWestM = BorderWestM,
                EstimatedBorderWidthEastM = BorderEastM,
                MedianDistanceToValidHaulCellM = MedianDistanceM,
                MaxDistanceToValidHaulCellM = MaxDistanceM,
                ProjectFieldBounds = ProjectBounds,
                HaulGridBounds = HaulBounds,
                SamplerBounds = SamplerBounds,
                ExtentDeltaSamplerMinusHaulGridM = SamplerMinusHaul,
                ExtentDeltaProjectMinusHaulGridM = ProjectMinusHaul,
            };
        }

        /// <summary>
        /// Returns a multi-line human-readable report suitable for logs and test output.
        /// </summary>
        /// <param name="Snapshot">Diagnostics snapshot to format.</param>
        /// <returns>Formatted diagnostic report.</returns>
        internal static string FormatReport
            (
            HaulThreeDOverlayDiagnosticsSnapshot Snapshot
            )
        {
            StringBuilder Builder = new StringBuilder();
            Builder.AppendLine("Haul 3D overlay diagnostics");
            Builder.AppendLine($"routeScrubIndex={Snapshot.RouteScrubIndex}");
            Builder.AppendLine(
                $"haulGrid={Snapshot.HaulGridWidth}x{Snapshot.HaulGridHeight} " +
                $"cell={Snapshot.HaulGridSizeM:0.###}m ({Snapshot.HaulGridSizeFt:0.##}ft) " +
                $"validCells={Snapshot.HaulValidCellCount} perimeterInvalid={Snapshot.HaulPerimeterInvalidCellCount}");
            Builder.AppendLine($"sampler={Snapshot.SamplerWidth}x{Snapshot.SamplerHeight} validSamples={Snapshot.ValidSamplerCount}");
            Builder.AppendLine(
                $"overlay updated={Snapshot.UpdatedSampleCount} notUpdated={Snapshot.NotUpdatedValidCount} " +
                $"outsideUtm={Snapshot.OutsideHaulUtmBoxCount} sampleFailed={Snapshot.SampleFailedCount} " +
                $"wrongZone={Snapshot.WrongUtmZoneCount} unchanged={Snapshot.UnchangedSampleCount}");
            Builder.AppendLine(
                $"borderWidthM N={Snapshot.EstimatedBorderWidthNorthM:0.###} " +
                $"S={Snapshot.EstimatedBorderWidthSouthM:0.###} " +
                $"W={Snapshot.EstimatedBorderWidthWestM:0.###} " +
                $"E={Snapshot.EstimatedBorderWidthEastM:0.###}");
            Builder.AppendLine(
                $"distanceToValidHaulCellM median={Snapshot.MedianDistanceToValidHaulCellM:0.###} " +
                $"max={Snapshot.MaxDistanceToValidHaulCellM:0.###}");
            Builder.AppendLine($"extentDelta sampler-haul {Snapshot.ExtentDeltaSamplerMinusHaulGridM.Describe()}");
            Builder.AppendLine($"extentDelta project-haul {Snapshot.ExtentDeltaProjectMinusHaulGridM.Describe()}");
            Builder.AppendLine(
                $"projectBounds lat=[{Snapshot.ProjectFieldBounds.MinLat:0.######},{Snapshot.ProjectFieldBounds.MaxLat:0.######}] " +
                $"lon=[{Snapshot.ProjectFieldBounds.MinLon:0.######},{Snapshot.ProjectFieldBounds.MaxLon:0.######}]");
            Builder.AppendLine(
                $"haulBounds lat=[{Snapshot.HaulGridBounds.MinLat:0.######},{Snapshot.HaulGridBounds.MaxLat:0.######}] " +
                $"lon=[{Snapshot.HaulGridBounds.MinLon:0.######},{Snapshot.HaulGridBounds.MaxLon:0.######}]");
            Builder.AppendLine(
                $"samplerBounds lat=[{Snapshot.SamplerBounds.MinLat:0.######},{Snapshot.SamplerBounds.MaxLat:0.######}] " +
                $"lon=[{Snapshot.SamplerBounds.MinLon:0.######},{Snapshot.SamplerBounds.MaxLon:0.######}]");
            return Builder.ToString().TrimEnd();
        }

        /// <summary>
        /// Returns one compact JSON object suitable for JSONL logging.
        /// </summary>
        /// <param name="Snapshot">Diagnostics snapshot to format.</param>
        /// <returns>Single-line JSON payload.</returns>
        internal static string FormatJsonLine
            (
            HaulThreeDOverlayDiagnosticsSnapshot Snapshot
            )
        {
            return
                "{" +
                $"\"kind\":\"haul3d_overlay\"," +
                $"\"routeScrubIndex\":{Snapshot.RouteScrubIndex}," +
                $"\"haulGridWidth\":{Snapshot.HaulGridWidth}," +
                $"\"haulGridHeight\":{Snapshot.HaulGridHeight}," +
                $"\"haulGridSizeM\":{Snapshot.HaulGridSizeM.ToString(CultureInfo.InvariantCulture)}," +
                $"\"haulValidCellCount\":{Snapshot.HaulValidCellCount}," +
                $"\"validSamplerCount\":{Snapshot.ValidSamplerCount}," +
                $"\"updatedSampleCount\":{Snapshot.UpdatedSampleCount}," +
                $"\"notUpdatedValidCount\":{Snapshot.NotUpdatedValidCount}," +
                $"\"outsideHaulUtmBoxCount\":{Snapshot.OutsideHaulUtmBoxCount}," +
                $"\"sampleFailedCount\":{Snapshot.SampleFailedCount}," +
                $"\"borderNorthM\":{Snapshot.EstimatedBorderWidthNorthM.ToString(CultureInfo.InvariantCulture)}," +
                $"\"borderSouthM\":{Snapshot.EstimatedBorderWidthSouthM.ToString(CultureInfo.InvariantCulture)}," +
                $"\"borderWestM\":{Snapshot.EstimatedBorderWidthWestM.ToString(CultureInfo.InvariantCulture)}," +
                $"\"borderEastM\":{Snapshot.EstimatedBorderWidthEastM.ToString(CultureInfo.InvariantCulture)}," +
                $"\"medianDistanceToValidHaulCellM\":{Snapshot.MedianDistanceToValidHaulCellM.ToString(CultureInfo.InvariantCulture)}," +
                $"\"extentDeltaSamplerMinusHaulNorthM\":{Snapshot.ExtentDeltaSamplerMinusHaulGridM.NorthM.ToString(CultureInfo.InvariantCulture)}," +
                $"\"extentDeltaSamplerMinusHaulSouthM\":{Snapshot.ExtentDeltaSamplerMinusHaulGridM.SouthM.ToString(CultureInfo.InvariantCulture)}," +
                $"\"extentDeltaSamplerMinusHaulEastM\":{Snapshot.ExtentDeltaSamplerMinusHaulGridM.EastM.ToString(CultureInfo.InvariantCulture)}," +
                $"\"extentDeltaSamplerMinusHaulWestM\":{Snapshot.ExtentDeltaSamplerMinusHaulGridM.WestM.ToString(CultureInfo.InvariantCulture)}" +
                "}";
        }

        /// <summary>
        /// Writes one categorized diagnostic line when a sink is configured.
        /// </summary>
        /// <param name="Message">Short message label.</param>
        /// <param name="Detail">Optional detail payload.</param>
        private static void Write
            (
            string Message,
            string? Detail = null
            )
        {
            WriteSink?.Invoke("Haul3DOverlay", Message, Detail);
        }

        /// <summary>
        /// Counts valid and perimeter-invalid cells on the haul processing grid.
        /// </summary>
        /// <param name="Grid">Leveled haul grid.</param>
        /// <param name="ValidCellCount">Count of valid haul cells.</param>
        /// <param name="PerimeterInvalidCellCount">Count of invalid cells adjacent to the grid edge.</param>
        private static void CountHaulGridMask
            (
            HaulBuilder.Grid Grid,
            out int ValidCellCount,
            out int PerimeterInvalidCellCount
            )
        {
            ValidCellCount = 0;
            PerimeterInvalidCellCount = 0;
            for (int Y = 0; Y < Grid.Height; Y++)
            {
                for (int X = 0; X < Grid.Width; X++)
                {
                    if (Grid.Valid[Y, X])
                    {
                        ValidCellCount++;
                        continue;
                    }

                    if (X == 0 || Y == 0 || X == Grid.Width - 1 || Y == Grid.Height - 1)
                        PerimeterInvalidCellCount++;
                }
            }
        }

        /// <summary>
        /// Aggregates overlay outcomes recorded by the collector.
        /// </summary>
        /// <param name="Collector">Completed overlay collector.</param>
        /// <param name="Sampler">Surface sampler after overlay.</param>
        /// <param name="ValidSamplerCount">Count of valid sampler cells.</param>
        /// <param name="UpdatedSampleCount">Count of updated sampler cells.</param>
        /// <param name="OutsideHaulUtmBoxCount">Count of samples outside the haul UTM envelope.</param>
        /// <param name="WrongUtmZoneCount">Count of samples rejected for UTM zone mismatch.</param>
        /// <param name="SampleFailedCount">Count of samples where haul sampling failed.</param>
        /// <param name="UnchangedSampleCount">Count of samples left unchanged by overlay.</param>
        /// <param name="NotUpdatedValidCount">Count of valid samples that did not receive overlay.</param>
        private static void CountSampleOutcomes
            (
            HaulThreeDOverlayDiagnosticsCollector Collector,
            FieldSurfaceSampler Sampler,
            out int ValidSamplerCount,
            out int UpdatedSampleCount,
            out int OutsideHaulUtmBoxCount,
            out int WrongUtmZoneCount,
            out int SampleFailedCount,
            out int UnchangedSampleCount,
            out int NotUpdatedValidCount
            )
        {
            ValidSamplerCount = 0;
            UpdatedSampleCount = 0;
            OutsideHaulUtmBoxCount = 0;
            WrongUtmZoneCount = 0;
            SampleFailedCount = 0;
            UnchangedSampleCount = 0;
            NotUpdatedValidCount = 0;

            bool[] Valid = Sampler.Valid;
            for (int Idx = 0; Idx < Valid.Length; Idx++)
            {
                if (!Valid[Idx])
                    continue;

                ValidSamplerCount++;
                switch (Collector.Outcomes[Idx])
                {
                    case HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.Updated:
                        UpdatedSampleCount++;
                        break;
                    case HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.OutsideHaulUtmBox:
                        OutsideHaulUtmBoxCount++;
                        NotUpdatedValidCount++;
                        break;
                    case HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.WrongUtmZone:
                        WrongUtmZoneCount++;
                        NotUpdatedValidCount++;
                        break;
                    case HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.SampleFailed:
                        SampleFailedCount++;
                        NotUpdatedValidCount++;
                        break;
                    case HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.Unchanged:
                        UnchangedSampleCount++;
                        NotUpdatedValidCount++;
                        break;
                }
            }
        }

        /// <summary>
        /// Computes border-width and nearest-valid-cell distance statistics for not-updated samples.
        /// </summary>
        /// <param name="Collector">Completed overlay collector.</param>
        /// <param name="Sampler">Surface sampler after overlay.</param>
        /// <param name="Grid">Leveled haul grid.</param>
        /// <param name="MedianDistanceM">Median distance to the nearest valid haul cell.</param>
        /// <param name="MaxDistanceM">Maximum distance to the nearest valid haul cell.</param>
        /// <param name="BorderNorthM">Estimated border width on the north side.</param>
        /// <param name="BorderSouthM">Estimated border width on the south side.</param>
        /// <param name="BorderWestM">Estimated border width on the west side.</param>
        /// <param name="BorderEastM">Estimated border width on the east side.</param>
        private static void ComputeDistanceToValidHaulCellStats
            (
            HaulThreeDOverlayDiagnosticsCollector Collector,
            FieldSurfaceSampler Sampler,
            HaulBuilder.Grid Grid,
            out double MedianDistanceM,
            out double MaxDistanceM,
            out double BorderNorthM,
            out double BorderSouthM,
            out double BorderWestM,
            out double BorderEastM
            )
        {
            int[,] DistanceCells = BuildDistanceToValidCellMap(Grid);
            List<double> DistancesM = new List<double>();
            BorderNorthM = 0;
            BorderSouthM = 0;
            BorderWestM = 0;
            BorderEastM = 0;

            double LatSpan = Sampler.MaxLat - Sampler.MinLat;
            double LonSpan = Sampler.MaxLon - Sampler.MinLon;
            int SampleWidthSpan = Math.Max(1, Sampler.SampleWidth - 1);
            int SampleHeightSpan = Math.Max(1, Sampler.SampleHeight - 1);
            double MaxEasting = Grid.MinX + Grid.Width * Grid.GridSizeM;
            double MaxNorthing = Grid.MinY + Grid.Height * Grid.GridSizeM;

            for (int Sy = 0; Sy < Sampler.SampleHeight; Sy++)
            {
                double Lat = Sampler.MaxLat - (Sy / (double)SampleHeightSpan) * LatSpan;
                for (int Sx = 0; Sx < Sampler.SampleWidth; Sx++)
                {
                    int Idx = Sy * Sampler.SampleWidth + Sx;
                    if (!Sampler.Valid[Idx])
                        continue;

                    HaulThreeDOverlayDiagnosticsCollector.SampleOutcome Outcome = Collector.Outcomes[Idx];
                    if (Outcome == HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.Updated)
                        continue;

                    double Lon = Sampler.MinLon + (Sx / (double)SampleWidthSpan) * LonSpan;
                    UTM.UTMCoordinate Utm = UTM.FromLatLon(Lat, Lon);
                    double DistanceM = ComputeSampleDistanceToValidHaulCellM(
                        Grid,
                        DistanceCells,
                        Utm,
                        MaxEasting,
                        MaxNorthing);
                    DistancesM.Add(DistanceM);

                    if (Utm.Northing > MaxNorthing)
                        BorderNorthM = Math.Max(BorderNorthM, Utm.Northing - MaxNorthing);
                    if (Utm.Northing < Grid.MinY)
                        BorderSouthM = Math.Max(BorderSouthM, Grid.MinY - Utm.Northing);
                    if (Utm.Easting > MaxEasting)
                        BorderEastM = Math.Max(BorderEastM, Utm.Easting - MaxEasting);
                    if (Utm.Easting < Grid.MinX)
                        BorderWestM = Math.Max(BorderWestM, Grid.MinX - Utm.Easting);
                }
            }

            if (DistancesM.Count == 0)
            {
                MedianDistanceM = 0;
                MaxDistanceM = 0;
                return;
            }

            DistancesM.Sort();
            MedianDistanceM = DistancesM[DistancesM.Count / 2];
            MaxDistanceM = DistancesM[^1];
        }

        /// <summary>
        /// Builds a BFS distance map from every grid cell to the nearest valid haul cell.
        /// </summary>
        /// <param name="Grid">Leveled haul grid.</param>
        /// <returns>Distance in cells from each grid location to the nearest valid cell.</returns>
        private static int[,] BuildDistanceToValidCellMap
            (
            HaulBuilder.Grid Grid
            )
        {
            int[,] DistanceCells = new int[Grid.Height, Grid.Width];
            for (int Y = 0; Y < Grid.Height; Y++)
            {
                for (int X = 0; X < Grid.Width; X++)
                    DistanceCells[Y, X] = int.MaxValue;
            }

            Queue<(int X, int Y)> Queue = new Queue<(int X, int Y)>();
            for (int Y = 0; Y < Grid.Height; Y++)
            {
                for (int X = 0; X < Grid.Width; X++)
                {
                    if (!Grid.Valid[Y, X])
                        continue;

                    DistanceCells[Y, X] = 0;
                    Queue.Enqueue((X, Y));
                }
            }

            while (Queue.Count > 0)
            {
                (int X, int Y) = Queue.Dequeue();
                int NextDistance = DistanceCells[Y, X] + 1;
                TryEnqueueDistanceNeighbor(Queue, DistanceCells, X - 1, Y, NextDistance);
                TryEnqueueDistanceNeighbor(Queue, DistanceCells, X + 1, Y, NextDistance);
                TryEnqueueDistanceNeighbor(Queue, DistanceCells, X, Y - 1, NextDistance);
                TryEnqueueDistanceNeighbor(Queue, DistanceCells, X, Y + 1, NextDistance);
            }

            return DistanceCells;
        }

        /// <summary>
        /// Enqueues one grid neighbour when its distance can be improved.
        /// </summary>
        /// <param name="Queue">Active BFS queue.</param>
        /// <param name="DistanceCells">Distance map being built.</param>
        /// <param name="X">Neighbour column index.</param>
        /// <param name="Y">Neighbour row index.</param>
        /// <param name="NextDistance">Candidate distance in cells.</param>
        private static void TryEnqueueDistanceNeighbor
            (
            Queue<(int X, int Y)> Queue,
            int[,] DistanceCells,
            int X,
            int Y,
            int NextDistance
            )
        {
            if (Y < 0 || Y >= DistanceCells.GetLength(0) || X < 0 || X >= DistanceCells.GetLength(1))
                return;

            if (NextDistance >= DistanceCells[Y, X])
                return;

            DistanceCells[Y, X] = NextDistance;
            Queue.Enqueue((X, Y));
        }

        /// <summary>
        /// Returns the distance in metres from one sampler location to the nearest valid haul cell.
        /// </summary>
        /// <param name="Grid">Leveled haul grid.</param>
        /// <param name="DistanceCells">Precomputed BFS distance map.</param>
        /// <param name="Utm">Sample location in UTM coordinates.</param>
        /// <param name="MaxEasting">Eastern edge of the haul grid envelope.</param>
        /// <param name="MaxNorthing">Northern edge of the haul grid envelope.</param>
        /// <returns>Distance in metres.</returns>
        private static double ComputeSampleDistanceToValidHaulCellM
            (
            HaulBuilder.Grid Grid,
            int[,] DistanceCells,
            UTM.UTMCoordinate Utm,
            double MaxEasting,
            double MaxNorthing
            )
        {
            if (Utm.Easting < Grid.MinX || Utm.Easting > MaxEasting ||
                Utm.Northing < Grid.MinY || Utm.Northing > MaxNorthing)
            {
                double EastGap = Math.Max(0, Grid.MinX - Utm.Easting) + Math.Max(0, Utm.Easting - MaxEasting);
                double NorthGap = Math.Max(0, Grid.MinY - Utm.Northing) + Math.Max(0, Utm.Northing - MaxNorthing);
                return Math.Sqrt((EastGap * EastGap) + (NorthGap * NorthGap));
            }

            double ColFrac = (Utm.Easting - Grid.MinX) / Grid.GridSizeM - 0.5;
            double RowFrac = (Utm.Northing - Grid.MinY) / Grid.GridSizeM - 0.5;
            int Col = Math.Clamp((int)Math.Round(ColFrac), 0, Grid.Width - 1);
            int Row = Math.Clamp((int)Math.Round(RowFrac), 0, Grid.Height - 1);
            int DistanceCellsCount = DistanceCells[Row, Col];
            if (DistanceCellsCount == int.MaxValue)
                return Grid.GridSizeM;

            return DistanceCellsCount * Grid.GridSizeM;
        }

        /// <summary>
        /// Computes how much further outer bounds extend beyond inner bounds on each side.
        /// </summary>
        /// <param name="Outer">Outer geographic bounds.</param>
        /// <param name="Inner">Inner geographic bounds.</param>
        /// <param name="MeanLat">Mean latitude used for longitude-to-metres conversion.</param>
        /// <returns>Side deltas in metres.</returns>
        private static HaulThreeDOverlayExtentDelta ComputeExtentDelta
            (
            FieldBounds Outer,
            FieldBounds Inner,
            double MeanLat
            )
        {
            double MetresPerDegreeLon = METRES_PER_DEGREE_LATITUDE *
                Math.Cos(MeanLat * Math.PI / 180.0);

            return new HaulThreeDOverlayExtentDelta
            {
                NorthM = Math.Max(0, (Outer.MaxLat - Inner.MaxLat) * METRES_PER_DEGREE_LATITUDE),
                SouthM = Math.Max(0, (Inner.MinLat - Outer.MinLat) * METRES_PER_DEGREE_LATITUDE),
                EastM = Math.Max(0, (Outer.MaxLon - Inner.MaxLon) * MetresPerDegreeLon),
                WestM = Math.Max(0, (Inner.MinLon - Outer.MinLon) * MetresPerDegreeLon),
            };
        }
    }
}

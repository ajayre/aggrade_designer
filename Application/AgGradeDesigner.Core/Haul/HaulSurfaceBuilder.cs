using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Builds leveled haul grid elevations and overlays them onto viewer surface samplers for haul 3D display.
    /// </summary>
    public static class HaulSurfaceBuilder
    {
        /// <summary>
        /// Half-cell offset when converting grid indices to UTM cell centres.
        /// </summary>
        private const double CELL_CENTER_OFFSET = 0.5;

        /// <summary>
        /// Cached source grid reference for leveled-grid reuse.
        /// </summary>
        private static HaulBuilder.Grid? _CachedSourceGrid;

        /// <summary>
        /// Cached route scrub index paired with <see cref="_CachedLeveledGrid"/>.
        /// </summary>
        private static int _CachedRouteScrubIndex = -1;

        /// <summary>
        /// Cached leveled grid for the pinned haul 3D route scrub index.
        /// </summary>
        private static HaulBuilder.Grid? _CachedLeveledGrid;

        /// <summary>
        /// Serializes access to the leveled-grid cache.
        /// </summary>
        private static readonly object LeveledGridCacheLock = new object();

        /// <summary>
        /// Clears cached leveled grids after haul regeneration or scrub discard.
        /// </summary>
        public static void InvalidateLeveledGridCache
            (
            )
        {
            lock (LeveledGridCacheLock)
            {
                _CachedSourceGrid = null;
                _CachedRouteScrubIndex = -1;
                _CachedLeveledGrid = null;
            }
        }

        /// <summary>
        /// Returns the haul processing grid after applying the pass prefix for a route scrub index.
        /// </summary>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Project">Open project used to rebuild the grid when no live result is present.</param>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <returns>Grid snapshot matching the haul 2D cut/fill base map at the supplied index.</returns>
        public static HaulBuilder.Grid ResolveLeveledGrid
            (
            HaulData Data,
            ProjectDocument Project,
            int RouteScrubIndex
            )
        {
            HaulBuilder.Grid Source = ResolveSourceGrid(Data, Project);
            lock (LeveledGridCacheLock)
            {
                if (_CachedLeveledGrid != null &&
                    _CachedRouteScrubIndex == RouteScrubIndex &&
                    ReferenceEquals(_CachedSourceGrid, Source))
                {
                    return _CachedLeveledGrid;
                }
            }

            IReadOnlyList<HaulBuilder.Pass> OrderedPasses = Data.ResolveOrderedPasses();
            HaulBuilder.Grid LeveledGrid;
            if (RouteScrubIndex <= 0 || OrderedPasses.Count == 0)
            {
                LeveledGrid = Source;
            }
            else
            {
                int PreviousPassCount = Math.Min(RouteScrubIndex, OrderedPasses.Count);
                IReadOnlyList<HaulBuilder.Pass> PreviousPasses = OrderedPasses.Take(PreviousPassCount).ToList();
                LeveledGrid = HaulGridLeveling.BuildAfterPasses(Source, PreviousPasses);
            }

            lock (LeveledGridCacheLock)
            {
                _CachedSourceGrid = Source;
                _CachedRouteScrubIndex = RouteScrubIndex;
                _CachedLeveledGrid = LeveledGrid;
            }

            return LeveledGrid;
        }

        /// <summary>
        /// Replaces existing elevation samples inside the haul grid extent with leveled haul values.
        /// </summary>
        /// <param name="Sampler">Viewer surface sampler built from the project survey.</param>
        /// <param name="LeveledGrid">Leveled haul grid for the pinned route scrub index.</param>
        /// <param name="DiagnosticsCollector">Optional overlay diagnostics collector.</param>
        public static void OverlayLeveledExistingOntoSampler
            (
            FieldSurfaceSampler Sampler,
            HaulBuilder.Grid LeveledGrid,
            HaulThreeDOverlayDiagnosticsCollector? DiagnosticsCollector = null
            )
        {
            if (Sampler.SampleWidth <= 0 || Sampler.SampleHeight <= 0)
                return;

            double[] Existing = Sampler.Existing;
            bool[] Valid = Sampler.Valid;
            double LatSpan = Sampler.MaxLat - Sampler.MinLat;
            double LonSpan = Sampler.MaxLon - Sampler.MinLon;
            if (LatSpan <= 0 || LonSpan <= 0 || LeveledGrid.GridSizeM <= 0)
                return;

            int SampleWidthSpan = Math.Max(1, Sampler.SampleWidth - 1);
            int SampleHeightSpan = Math.Max(1, Sampler.SampleHeight - 1);
            double MaxEasting = LeveledGrid.MinX + LeveledGrid.Width * LeveledGrid.GridSizeM;
            double MaxNorthing = LeveledGrid.MinY + LeveledGrid.Height * LeveledGrid.GridSizeM;

            // Resample each survey vertex from the haul grid so the 3D mesh is continuous, not sparsely stamped.
            for (int Sy = 0; Sy < Sampler.SampleHeight; Sy++)
            {
                double Lat = Sampler.MaxLat - (Sy / (double)SampleHeightSpan) * LatSpan;
                for (int Sx = 0; Sx < Sampler.SampleWidth; Sx++)
                {
                    int Idx = Sy * Sampler.SampleWidth + Sx;
                    if (!Valid[Idx])
                        continue;

                    double Lon = Sampler.MinLon + (Sx / (double)SampleWidthSpan) * LonSpan;
                    UTM.UTMCoordinate Utm = UTM.FromLatLon(Lat, Lon);
                    if (Utm.Zone != LeveledGrid.Zone || Utm.IsNorthernHemisphere != LeveledGrid.IsNorthern)
                    {
                        DiagnosticsCollector?.Record(
                            Idx,
                            HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.WrongUtmZone,
                            Existing[Idx]);
                        continue;
                    }

                    if (Utm.Easting < LeveledGrid.MinX || Utm.Easting > MaxEasting ||
                        Utm.Northing < LeveledGrid.MinY || Utm.Northing > MaxNorthing)
                    {
                        DiagnosticsCollector?.Record(
                            Idx,
                            HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.OutsideHaulUtmBox,
                            Existing[Idx]);
                        continue;
                    }

                    if (TrySampleLeveledExistingBilinear(LeveledGrid, Utm.Easting, Utm.Northing, out double Elevation))
                    {
                        Existing[Idx] = Elevation;
                        DiagnosticsCollector?.Record(
                            Idx,
                            HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.Updated,
                            Elevation);
                    }
                    else
                    {
                        DiagnosticsCollector?.Record(
                            Idx,
                            HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.SampleFailed,
                            Existing[Idx]);
                    }
                }
            }

            // Recompute from the full valid surface so the colour map spans all displayed elevations.
            Sampler.RecomputeExistingElevationExtentsFromValid();
            Sampler.InvalidateScalarDerivedCaches();
        }

        /// <summary>
        /// Bilinearly samples leveled haul existing elevation at a UTM location inside the grid extent.
        /// </summary>
        /// <param name="Grid">Leveled haul grid in the grid UTM frame.</param>
        /// <param name="Easting">Sample easting in metres.</param>
        /// <param name="Northing">Sample northing in metres.</param>
        /// <param name="Elevation">Sampled elevation in metres when successful.</param>
        /// <returns>True when a valid elevation was resolved.</returns>
        private static bool TrySampleLeveledExistingBilinear
            (
            HaulBuilder.Grid Grid,
            double Easting,
            double Northing,
            out double Elevation
            )
        {
            Elevation = 0;
            if (Grid.Width <= 0 || Grid.Height <= 0)
                return false;

            double ColFrac = (Easting - Grid.MinX) / Grid.GridSizeM - CELL_CENTER_OFFSET;
            double RowFrac = (Northing - Grid.MinY) / Grid.GridSizeM - CELL_CENTER_OFFSET;
            if (ColFrac < 0 || RowFrac < 0 || ColFrac > Grid.Width - 1 || RowFrac > Grid.Height - 1)
                return false;

            int Col0 = (int)Math.Floor(ColFrac);
            int Row0 = (int)Math.Floor(RowFrac);
            int Col1 = Math.Min(Col0 + 1, Grid.Width - 1);
            int Row1 = Math.Min(Row0 + 1, Grid.Height - 1);
            double Tx = ColFrac - Col0;
            double Ty = RowFrac - Row0;

            bool Valid00 = Grid.Valid[Row0, Col0];
            bool Valid10 = Grid.Valid[Row0, Col1];
            bool Valid01 = Grid.Valid[Row1, Col0];
            bool Valid11 = Grid.Valid[Row1, Col1];
            if (Valid00 && Valid10 && Valid01 && Valid11)
            {
                double E00 = Grid.Existing[Row0, Col0];
                double E10 = Grid.Existing[Row0, Col1];
                double E01 = Grid.Existing[Row1, Col0];
                double E11 = Grid.Existing[Row1, Col1];
                double Top = E00 + ((E10 - E00) * Tx);
                double Bottom = E01 + ((E11 - E01) * Tx);
                Elevation = Top + ((Bottom - Top) * Ty);
                return true;
            }

            return TrySampleNearestLeveledExisting(Grid, ColFrac, RowFrac, out Elevation);
        }

        /// <summary>
        /// Returns the nearest valid haul-grid cell elevation when bilinear corners are incomplete.
        /// </summary>
        /// <param name="Grid">Leveled haul grid in the grid UTM frame.</param>
        /// <param name="ColFrac">Fractional column index measured from the grid western edge.</param>
        /// <param name="RowFrac">Fractional row index measured from the grid southern edge.</param>
        /// <param name="Elevation">Sampled elevation in metres when successful.</param>
        /// <returns>True when a valid nearest cell was found.</returns>
        private static bool TrySampleNearestLeveledExisting
            (
            HaulBuilder.Grid Grid,
            double ColFrac,
            double RowFrac,
            out double Elevation
            )
        {
            Elevation = 0;
            int Col = Math.Clamp((int)Math.Round(ColFrac), 0, Grid.Width - 1);
            int Row = Math.Clamp((int)Math.Round(RowFrac), 0, Grid.Height - 1);
            if (!Grid.Valid[Row, Col])
                return false;

            Elevation = Grid.Existing[Row, Col];
            return true;
        }

        /// <summary>
        /// Applies leveled haul existing elevations for a route scrub index onto a viewer surface sampler.
        /// </summary>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Project">Open project used to rebuild the grid when no live result is present.</param>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <param name="Sampler">Viewer surface sampler built from the project survey.</param>
        /// <param name="DiagnosticsCollector">Optional overlay diagnostics collector.</param>
        public static void ApplyLeveledHaulExistingForScrubIndex
            (
            HaulData Data,
            ProjectDocument Project,
            int RouteScrubIndex,
            FieldSurfaceSampler Sampler,
            HaulThreeDOverlayDiagnosticsCollector? DiagnosticsCollector = null
            )
        {
            HaulBuilder.Grid LeveledGrid = ResolveLeveledGrid(Data, Project, RouteScrubIndex);
            OverlayLeveledExistingOntoSampler(Sampler, LeveledGrid, DiagnosticsCollector);
        }

        /// <summary>
        /// Returns the leveled haul grid for diagnostics without mutating viewer state.
        /// </summary>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Project">Open project used to rebuild the grid when no live result is present.</param>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <returns>Grid snapshot matching the haul 2D cut/fill base map at the supplied index.</returns>
        public static HaulBuilder.Grid ResolveLeveledGridForDiagnostics
            (
            HaulData Data,
            ProjectDocument Project,
            int RouteScrubIndex
            )
        {
            return ResolveLeveledGrid(Data, Project, RouteScrubIndex);
        }

        /// <summary>
        /// Returns the live or rebuilt haul processing grid for overlay work.
        /// </summary>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Project">Open project used to rebuild the grid when no live result is present.</param>
        /// <returns>Processing grid from the live haul result or AGD rebuild fallback.</returns>
        private static HaulBuilder.Grid ResolveSourceGrid
            (
            HaulData Data,
            ProjectDocument Project
            )
        {
            if (Data.Result?.Grid != null)
                return Data.Result.Grid;

            HaulBuilder.AgdField Field = BuildAgdField(Project);
            return Data.ResolveGrid(Field);
        }

        /// <summary>
        /// Builds a parsed AGD field from the current project for haul grid reconstruction.
        /// </summary>
        /// <param name="Project">Open project with survey and design.</param>
        /// <returns>Parsed AGD field consumed by the haul engine.</returns>
        private static HaulBuilder.AgdField BuildAgdField
            (
            ProjectDocument Project
            )
        {
            if (!Project.HasSurvey || !Project.HasDesign || Project.Survey == null || Project.DesignGrid == null)
                throw new InvalidOperationException("Survey and design are required for haul 3D surface rendering.");

            string TempAgd = Path.Combine(Path.GetTempPath(), $"aggrade-haul-3d-{Guid.NewGuid():N}.agd");
            try
            {
                AgdService.Export(
                    TempAgd,
                    Project.Survey,
                    Project.DesignGrid,
                    Project.DesignParameters ?? new DesignParameters());
                return HaulBuilder.Agd.Parse(TempAgd);
            }
            finally
            {
                if (File.Exists(TempAgd))
                    File.Delete(TempAgd);
            }
        }
    }
}

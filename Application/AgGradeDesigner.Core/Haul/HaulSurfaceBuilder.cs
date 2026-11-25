using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Builds leveled haul grid elevations and populates the dedicated haul 3D sampler channel.
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

            IReadOnlyList<HaulBuilder.Pass> PassesAlreadyDriven = Data.PassesBeforeDriveStep(RouteScrubIndex);
            HaulRoadSurface? Road = HaulRoadSurface.ForFrame(Data, RouteScrubIndex, Source);
            HaulBuilder.Grid LeveledGrid;
            if (PassesAlreadyDriven.Count == 0 && Road == null)
            {
                LeveledGrid = Source;
            }
            else
            {
                // intent: the road section is stamped here so 2D, 3D, and water share one surface
                LeveledGrid = HaulGridLeveling.BuildAfterPasses(Source, PassesAlreadyDriven, Road);
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
        /// Populates the dedicated haul-leveled channel from survey existing elevations and leveled haul grid values.
        /// </summary>
        /// <param name="Sampler">Viewer surface sampler built from the project survey.</param>
        /// <param name="LeveledGrid">Leveled haul grid for the pinned route scrub index.</param>
        /// <param name="DiagnosticsCollector">Optional overlay diagnostics collector.</param>
        public static void PopulateHaulLeveledChannel
            (
            FieldSurfaceSampler Sampler,
            HaulBuilder.Grid LeveledGrid,
            HaulThreeDOverlayDiagnosticsCollector? DiagnosticsCollector = null
            )
        {
            PopulateHaulLeveledChannel(Sampler, LeveledGrid, DiagnosticsCollector, null);
        }

        /// <summary>
        /// Populates the dedicated haul-leveled channel from survey existing elevations and leveled haul grid values.
        /// Samples on a road or pad use the haul section at that point.
        /// </summary>
        /// <param name="Sampler">Viewer surface sampler built from the project survey.</param>
        /// <param name="LeveledGrid">Leveled haul grid for the pinned route scrub index.</param>
        /// <param name="DiagnosticsCollector">Optional overlay diagnostics collector.</param>
        /// <param name="Road">Finished road and pad section, or null during field work.</param>
        internal static void PopulateHaulLeveledChannel
            (
            FieldSurfaceSampler Sampler,
            HaulBuilder.Grid LeveledGrid,
            HaulThreeDOverlayDiagnosticsCollector? DiagnosticsCollector,
            HaulRoadSurface? Road
            )
        {
            if (Sampler.SampleWidth <= 0 || Sampler.SampleHeight <= 0)
                return;

            // intent: haul 3D must never seed from survey Existing; unset samples stay NaN until resampled
            double[] HaulLeveled = new double[Sampler.Existing.Length];
            Array.Fill(HaulLeveled, double.NaN);
            bool[] Valid = Sampler.Valid;
            double LatSpan = Sampler.MaxLat - Sampler.MinLat;
            double LonSpan = Sampler.MaxLon - Sampler.MinLon;
            if (LatSpan <= 0 || LonSpan <= 0 || LeveledGrid.GridSizeM <= 0)
                return;

            int SampleWidthSpan = Math.Max(1, Sampler.SampleWidth - 1);
            int SampleHeightSpan = Math.Max(1, Sampler.SampleHeight - 1);
            double MaxEasting = LeveledGrid.MinX + LeveledGrid.Width * LeveledGrid.GridSizeM;
            double MaxNorthing = LeveledGrid.MinY + LeveledGrid.Height * LeveledGrid.GridSizeM;

            // Resample each survey vertex from the haul grid so the 3D mesh is continuous.
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
                            double.NaN);
                        continue;
                    }

                    // intent: clamp perimeter samples to the haul grid so the mesh never falls back to survey Existing
                    if (TrySampleLeveledExistingClamped(
                            LeveledGrid,
                            Utm.Easting,
                            Utm.Northing,
                            MaxEasting,
                            MaxNorthing,
                            out double Elevation,
                            out bool UsedEdgeClamp))
                    {
                        // intent: survey vertices are not the haul cells, so the section is evaluated here
                        double ClampedEasting = Math.Clamp(Utm.Easting, LeveledGrid.MinX, MaxEasting);
                        double ClampedNorthing = Math.Clamp(Utm.Northing, LeveledGrid.MinY, MaxNorthing);
                        if (Road != null && Road.TryElevation(ClampedEasting, ClampedNorthing, out double Finished))
                            Elevation = Finished;

                        HaulLeveled[Idx] = Elevation;
                        DiagnosticsCollector?.Record(
                            Idx,
                            UsedEdgeClamp
                                ? HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.OutsideHaulUtmBox
                                : HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.Updated,
                            Elevation);
                    }
                    else
                    {
                        DiagnosticsCollector?.Record(
                            Idx,
                            HaulThreeDOverlayDiagnosticsCollector.SampleOutcome.SampleFailed,
                            double.NaN);
                    }
                }
            }

            Sampler.SetHaulLeveledChannel(HaulLeveled);
        }

        /// <summary>
        /// Obsolete stamp path retained for diagnostics tests that still reference the old overlay name.
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
            PopulateHaulLeveledChannel(Sampler, LeveledGrid, DiagnosticsCollector);
        }

        /// <summary>
        /// Bilinearly samples the leveled haul grid, clamping perimeter coordinates to the grid extent.
        /// </summary>
        /// <param name="Grid">Leveled haul grid in the grid UTM frame.</param>
        /// <param name="Easting">Sample easting in metres.</param>
        /// <param name="Northing">Sample northing in metres.</param>
        /// <param name="MaxEasting">Eastern edge of the haul grid in metres.</param>
        /// <param name="MaxNorthing">Northern edge of the haul grid in metres.</param>
        /// <param name="Elevation">Sampled elevation in metres when successful.</param>
        /// <param name="UsedEdgeClamp">True when easting or northing were clamped to the haul grid edge.</param>
        /// <returns>True when a valid elevation was resolved.</returns>
        private static bool TrySampleLeveledExistingClamped
            (
            HaulBuilder.Grid Grid,
            double Easting,
            double Northing,
            double MaxEasting,
            double MaxNorthing,
            out double Elevation,
            out bool UsedEdgeClamp
            )
        {
            UsedEdgeClamp = Easting < Grid.MinX || Easting > MaxEasting ||
                            Northing < Grid.MinY || Northing > MaxNorthing;
            double ClampedEasting = Math.Clamp(Easting, Grid.MinX, MaxEasting);
            double ClampedNorthing = Math.Clamp(Northing, Grid.MinY, MaxNorthing);
            return TrySampleLeveledExistingBilinear(Grid, ClampedEasting, ClampedNorthing, out Elevation);
        }

        /// <summary>
        /// Bilinearly samples leveled haul grid elevation at a UTM location inside the grid extent.
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
        /// Builds the dedicated haul-leveled channel for a route scrub index on a viewer surface sampler.
        /// </summary>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Project">Open project used to rebuild the grid when no live result is present.</param>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <param name="Sampler">Viewer surface sampler built from the project survey.</param>
        /// <param name="DiagnosticsCollector">Optional overlay diagnostics collector.</param>
        public static void BuildHaulLeveledChannelForScrubIndex
            (
            HaulData Data,
            ProjectDocument Project,
            int RouteScrubIndex,
            FieldSurfaceSampler Sampler,
            HaulThreeDOverlayDiagnosticsCollector? DiagnosticsCollector = null
            )
        {
            HaulBuilder.Grid LeveledGrid = ResolveLeveledGrid(Data, Project, RouteScrubIndex);
            PopulateHaulLeveledChannel(
                Sampler,
                LeveledGrid,
                DiagnosticsCollector,
                HaulRoadSurface.ForFrame(Data, RouteScrubIndex, LeveledGrid));
        }

        /// <summary>
        /// Obsolete stamp entry point retained for callers not yet migrated to <see cref="BuildHaulLeveledChannelForScrubIndex"/>.
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
            BuildHaulLeveledChannelForScrubIndex(Data, Project, RouteScrubIndex, Sampler, DiagnosticsCollector);
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

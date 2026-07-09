using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Builds elevation grids from project basis surfaces for ponding analysis (WATER-SPEC §6.2, §8.1.13).
    /// </summary>
    public static class WaterElevationGridBuilder
    {
        /// <summary>Sentinel elevation for invalid DEM cells.</summary>
        public const float NO_DATA_VALUE = float.NaN;

        /// <summary>Sample grid resolution per axis when building survey-only elevation grids.</summary>
        private const int SURVEY_SAMPLER_RESOLUTION = 256;

        /// <summary>
        /// Describes a square elevation grid ready for ponding analysis.
        /// </summary>
        public sealed class ElevationGrid
        {
            /// <summary>Row-major elevation samples in metres.</summary>
            public float[,] Dem { get; init; } = new float[0, 0];

            /// <summary>Grid row count.</summary>
            public int Rows { get; init; }

            /// <summary>Grid column count.</summary>
            public int Cols { get; init; }

            /// <summary>Cell size in metres.</summary>
            public double CellSizeM { get; init; }

            /// <summary>Southern edge latitude in decimal degrees.</summary>
            public double MinLat { get; init; }

            /// <summary>Northern edge latitude in decimal degrees.</summary>
            public double MaxLat { get; init; }

            /// <summary>Western edge longitude in decimal degrees.</summary>
            public double MinLon { get; init; }

            /// <summary>Eastern edge longitude in decimal degrees.</summary>
            public double MaxLon { get; init; }
        }

        /// <summary>
        /// Attempts to build an elevation grid for the requested basis surface.
        /// </summary>
        /// <param name="Project">Open project.</param>
        /// <param name="BasisSurface">Target basis surface.</param>
        /// <param name="RouteScrubIndex">Route scrub index for haul cut/fill surfaces.</param>
        /// <returns>Grid when the basis surface is available; otherwise null.</returns>
        public static ElevationGrid? TryBuild
            (
            ProjectDocument Project,
            WaterBasisSurfaceKind BasisSurface,
            int RouteScrubIndex
            )
        {
            if (BasisSurface == WaterBasisSurfaceKind.HaulCutFill)
                return TryBuildFromHaul(Project, RouteScrubIndex);

            if (!Project.HasDesign || Project.DesignGrid == null)
                return TryBuildFromSurveySampler(Project, BasisSurface);

            return TryBuildFromDesignGrid(Project.DesignGrid, BasisSurface);
        }

        /// <summary>
        /// Builds an elevation grid from design grid bin elevations using AgGrade
        /// <c>Field.BuildElevationGrid</c> row/column orientation (north at row 0).
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="BasisSurface">Existing or proposed basis surface.</param>
        /// <returns>Elevation grid aligned to design bins.</returns>
        private static ElevationGrid? TryBuildFromDesignGrid
            (
            DesignGrid Grid,
            WaterBasisSurfaceKind BasisSurface
            )
        {
            if ((Grid.Width <= 0) || (Grid.Height <= 0) || (Grid.Bins.Count == 0))
                return null;

            int MinBinX = int.MaxValue;
            int MaxBinX = int.MinValue;
            int MinBinY = int.MaxValue;
            int MaxBinY = int.MinValue;
            bool HasValid = false;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                MinBinX = Math.Min(MinBinX, Bin.X);
                MaxBinX = Math.Max(MaxBinX, Bin.X);
                MinBinY = Math.Min(MinBinY, Bin.Y);
                MaxBinY = Math.Max(MaxBinY, Bin.Y);
                HasValid = true;
            }

            if (!HasValid)
                return null;

            int Cols = MaxBinX - MinBinX + 1;
            int Rows = MaxBinY - MinBinY + 1;
            float[,] Dem = new float[Rows, Cols];
            for (Int32 Row = 0; Row < Rows; Row++)
            {
                for (Int32 Col = 0; Col < Cols; Col++)
                    Dem[Row, Col] = NO_DATA_VALUE;
            }

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                // Match AgGrade Field.BuildElevationGrid: row 0 is the northern edge (max bin Y).
                int Row = MaxBinY - Bin.Y;
                int Col = Bin.X - MinBinX;
                if ((Row < 0) || (Row >= Rows) || (Col < 0) || (Col >= Cols))
                    continue;

                double Elevation = BasisSurface == WaterBasisSurfaceKind.Proposed ? Bin.ProposedM : Bin.ExistingM;
                Dem[Row, Col] = (float)Elevation;
            }

            ComputeGeographicBoundsFromBins(Grid, MinBinX, MinBinY, MaxBinX, MaxBinY, out double MinLat, out double MaxLat, out double MinLon, out double MaxLon);
            return new ElevationGrid
            {
                Dem = Dem,
                Rows = Rows,
                Cols = Cols,
                CellSizeM = Grid.BinSizeM,
                MinLat = MinLat,
                MaxLat = MaxLat,
                MinLon = MinLon,
                MaxLon = MaxLon,
            };
        }

        /// <summary>
        /// Builds an elevation grid from the viewer surface sampler when no design grid exists.
        /// </summary>
        /// <param name="Project">Open project with survey.</param>
        /// <param name="BasisSurface">Existing or proposed basis surface.</param>
        /// <returns>Elevation grid sampled from survey topology.</returns>
        private static ElevationGrid? TryBuildFromSurveySampler
            (
            ProjectDocument Project,
            WaterBasisSurfaceKind BasisSurface
            )
        {
            if (!Project.HasSurvey)
                return null;

            FieldSurfaceSampler Sampler = new FieldSurfaceSampler();
            Sampler.BuildFromProject(Project, SURVEY_SAMPLER_RESOLUTION, null);
            if (Sampler.SampleWidth <= 0)
                return null;

            int Rows = Sampler.SampleHeight;
            int Cols = Sampler.SampleWidth;
            float[,] Dem = new float[Rows, Cols];
            bool HasValid = false;
            for (Int32 Row = 0; Row < Rows; Row++)
            {
                for (Int32 Col = 0; Col < Cols; Col++)
                {
                    int Index = (Row * Cols) + Col;
                    if (!Sampler.Valid[Index])
                    {
                        Dem[Row, Col] = NO_DATA_VALUE;
                        continue;
                    }

                    double Elevation = BasisSurface == WaterBasisSurfaceKind.Proposed
                        ? Sampler.Proposed[Index]
                        : Sampler.Existing[Index];
                    Dem[Row, Col] = (float)Elevation;
                    HasValid = true;
                }
            }

            if (!HasValid)
                return null;

            double LatSpan = Sampler.MaxLat - Sampler.MinLat;
            double LonSpan = Sampler.MaxLon - Sampler.MinLon;
            double CentreLat = (Sampler.MinLat + Sampler.MaxLat) * 0.5;
            double MetresPerLat = FieldConstants.METRES_PER_DEGREE_LAT;
            double MetresPerLon = MetresPerLat * Math.Cos(CentreLat * Math.PI / FieldConstants.DEGREES_PER_RADIAN);
            double CellSizeM = Math.Max(
                LatSpan > 0 ? (LatSpan * MetresPerLat) / Math.Max(1, Rows - 1) : FieldConstants.BIN_SIZE_M,
                LonSpan > 0 ? (LonSpan * MetresPerLon) / Math.Max(1, Cols - 1) : FieldConstants.BIN_SIZE_M);

            return new ElevationGrid
            {
                Dem = Dem,
                Rows = Rows,
                Cols = Cols,
                CellSizeM = CellSizeM,
                MinLat = Sampler.MinLat,
                MaxLat = Sampler.MaxLat,
                MinLon = Sampler.MinLon,
                MaxLon = Sampler.MaxLon,
            };
        }

        /// <summary>
        /// Builds an elevation grid from a leveled haul cut/fill surface at a route scrub index.
        /// </summary>
        /// <param name="Project">Open project with haul data.</param>
        /// <param name="RouteScrubIndex">Active route scrub index.</param>
        /// <returns>Elevation grid when haul frame data is available.</returns>
        private static ElevationGrid? TryBuildFromHaul
            (
            ProjectDocument Project,
            int RouteScrubIndex
            )
        {
            if (!Project.HasHaulData || Project.HaulData == null)
                return null;

            HaulBuilder.Grid LeveledGrid = HaulSurfaceBuilder.ResolveLeveledGrid(Project.HaulData, Project, RouteScrubIndex);
            return TryBuildFromLeveledHaulGrid(LeveledGrid);
        }

        /// <summary>
        /// Builds an elevation grid from a leveled haul grid for tests and haul water analysis.
        /// </summary>
        /// <param name="LeveledGrid">Leveled haul grid in haul-builder row order (south at row 0).</param>
        /// <returns>Elevation grid with north at row 0, or null when no valid cells exist.</returns>
        internal static ElevationGrid? TryBuildFromLeveledHaulGrid
            (
            HaulBuilder.Grid LeveledGrid
            )
        {
            if ((LeveledGrid.Width <= 0) || (LeveledGrid.Height <= 0))
                return null;

            float[,] Dem = new float[LeveledGrid.Height, LeveledGrid.Width];
            bool HasValid = false;
            for (Int32 Row = 0; Row < LeveledGrid.Height; Row++)
            {
                // HaulBuilder grids grow from south (MinY) upward; water DEM row 0 is the northern edge.
                int SourceRow = LeveledGrid.Height - 1 - Row;
                for (Int32 Col = 0; Col < LeveledGrid.Width; Col++)
                {
                    if (!LeveledGrid.Valid[SourceRow, Col])
                    {
                        Dem[Row, Col] = NO_DATA_VALUE;
                        continue;
                    }

                    Dem[Row, Col] = (float)LeveledGrid.Existing[SourceRow, Col];
                    HasValid = true;
                }
            }

            if (!HasValid)
                return null;

            FieldBounds Bounds = HaulScrubFrameBuilder.ComputeGridBounds(LeveledGrid);
            return BuildElevationGridFromDem(Dem, LeveledGrid.Height, LeveledGrid.Width, LeveledGrid.GridSizeM, Bounds);
        }

        /// <summary>
        /// Wraps a populated DEM and geographic bounds as an elevation grid.
        /// </summary>
        /// <param name="Dem">Row-major elevations with north at row 0.</param>
        /// <param name="Rows">Grid row count.</param>
        /// <param name="Cols">Grid column count.</param>
        /// <param name="CellSizeM">Cell size in metres.</param>
        /// <param name="Bounds">Geographic bounds for the grid footprint.</param>
        /// <returns>Elevation grid ready for ponding analysis.</returns>
        private static ElevationGrid BuildElevationGridFromDem
            (
            float[,] Dem,
            int Rows,
            int Cols,
            double CellSizeM,
            FieldBounds Bounds
            )
        {
            return new ElevationGrid
            {
                Dem = Dem,
                Rows = Rows,
                Cols = Cols,
                CellSizeM = CellSizeM,
                MinLat = Bounds.MinLat,
                MaxLat = Bounds.MaxLat,
                MinLon = Bounds.MinLon,
                MaxLon = Bounds.MaxLon,
            };
        }

        /// <summary>
        /// Computes WGS-84 bounds for a populated bin index range using UTM grid origin (AgGrade SW/NE corners).
        /// </summary>
        /// <param name="Grid">Design grid carrying origin and zone metadata.</param>
        /// <param name="MinBinX">Minimum populated bin column index.</param>
        /// <param name="MinBinY">Minimum populated bin row index.</param>
        /// <param name="MaxBinX">Maximum populated bin column index.</param>
        /// <param name="MaxBinY">Maximum populated bin row index.</param>
        /// <param name="MinLat">Southern bound in decimal degrees.</param>
        /// <param name="MaxLat">Northern bound in decimal degrees.</param>
        /// <param name="MinLon">Western bound in decimal degrees.</param>
        /// <param name="MaxLon">Eastern bound in decimal degrees.</param>
        private static void ComputeGeographicBoundsFromBins
            (
            DesignGrid Grid,
            int MinBinX,
            int MinBinY,
            int MaxBinX,
            int MaxBinY,
            out double MinLat,
            out double MaxLat,
            out double MinLon,
            out double MaxLon
            )
        {
            double SwEastingM = Grid.OriginEastingM + (MinBinX * Grid.BinSizeM);
            double SwNorthingM = Grid.OriginNorthingM + (MinBinY * Grid.BinSizeM);
            double NeEastingM = Grid.OriginEastingM + ((MaxBinX + 1) * Grid.BinSizeM);
            double NeNorthingM = Grid.OriginNorthingM + ((MaxBinY + 1) * Grid.BinSizeM);
            UTM.ToLatLon(Grid.UtmZone, Grid.UtmNorth, SwEastingM, SwNorthingM, out MinLat, out MinLon);
            UTM.ToLatLon(Grid.UtmZone, Grid.UtmNorth, NeEastingM, NeNorthingM, out MaxLat, out MaxLon);
        }
    }
}

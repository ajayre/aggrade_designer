using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Field-wide elevation and slope statistics for read-only surface tools.
    /// </summary>
    public readonly struct SurfaceRegionStats
    {
        /// <summary>Minimum proposed elevation in metres.</summary>
        public double MinElevationM { get; init; }

        /// <summary>Maximum proposed elevation in metres.</summary>
        public double MaxElevationM { get; init; }

        /// <summary>Mean proposed elevation in metres.</summary>
        public double MeanElevationM { get; init; }

        /// <summary>Minimum bin slope magnitude in m/m.</summary>
        public double MinSlopeMm { get; init; }

        /// <summary>Maximum bin slope magnitude in m/m.</summary>
        public double MaxSlopeMm { get; init; }

        /// <summary>Mean bin slope magnitude in m/m.</summary>
        public double MeanSlopeMm { get; init; }

        /// <summary>Volume above the mean elevation reference plane in cubic metres.</summary>
        public double VolumeAboveReferenceM3 { get; init; }

        /// <summary>Volume below the mean elevation reference plane in cubic metres.</summary>
        public double VolumeBelowReferenceM3 { get; init; }

        /// <summary>Drainage heading in degrees clockwise from north.</summary>
        public double DrainageHeadingDeg { get; init; }

        /// <summary>Count of valid bins included in the statistics.</summary>
        public int ValidBinCount { get; init; }
    }

    /// <summary>
    /// Summary payload returned by <c>get_surface_summary</c>.
    /// </summary>
    public readonly struct SurfaceSummary
    {
        /// <summary>Minimum UTM easting in metres.</summary>
        public double MinEastingM { get; init; }

        /// <summary>Maximum UTM easting in metres.</summary>
        public double MaxEastingM { get; init; }

        /// <summary>Minimum UTM northing in metres.</summary>
        public double MinNorthingM { get; init; }

        /// <summary>Maximum UTM northing in metres.</summary>
        public double MaxNorthingM { get; init; }

        /// <summary>Bin edge length in metres.</summary>
        public double CellSizeM { get; init; }

        /// <summary>Minimum proposed elevation in metres.</summary>
        public double MinElevationM { get; init; }

        /// <summary>Maximum proposed elevation in metres.</summary>
        public double MaxElevationM { get; init; }

        /// <summary>Minimum bin slope magnitude in m/m.</summary>
        public double MinSlopeMm { get; init; }

        /// <summary>Maximum bin slope magnitude in m/m.</summary>
        public double MaxSlopeMm { get; init; }

        /// <summary>Cut/fill balance (fill minus cut) in cubic metres.</summary>
        public double CutFillBalanceM3 { get; init; }

        /// <summary>Low point easting in metres.</summary>
        public double LowPointEastingM { get; init; }

        /// <summary>Low point northing in metres.</summary>
        public double LowPointNorthingM { get; init; }

        /// <summary>Low point elevation in metres.</summary>
        public double LowPointElevationM { get; init; }

        /// <summary>High point easting in metres.</summary>
        public double HighPointEastingM { get; init; }

        /// <summary>High point northing in metres.</summary>
        public double HighPointNorthingM { get; init; }

        /// <summary>High point elevation in metres.</summary>
        public double HighPointElevationM { get; init; }

        /// <summary>Drainage heading in degrees clockwise from north.</summary>
        public double DrainageHeadingDeg { get; init; }
    }

    /// <summary>
    /// Computes slopes, summaries, and region statistics from a design grid.
    /// </summary>
    static public class SurfaceAnalysis
    {
        /// <summary>
        /// Builds the Phase 1 surface summary for the proposed layer.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Summary metrics in metric units.</returns>
        static public SurfaceSummary BuildSurfaceSummary
            (
            DesignGrid Grid
            )
        {
            SurfaceRegionStats Region = BuildRegionStats(Grid, SurfaceLayerKind.Proposed);
            SurfaceCutFillVolumes Volumes = SurfaceVolumeCalculator.CalculateCutFill(
                Grid,
                SurfaceLayerKind.Existing,
                SurfaceLayerKind.Proposed);

            FindExtremePoints(Grid, SurfaceLayerKind.Proposed, out GridBinData? LowBin, out GridBinData? HighBin);
            double LowE = BinEasting(Grid, LowBin);
            double LowN = BinNorthing(Grid, LowBin);
            double HighE = BinEasting(Grid, HighBin);
            double HighN = BinNorthing(Grid, HighBin);

            return new SurfaceSummary
            {
                MinEastingM = Grid.MinX,
                MaxEastingM = Grid.MaxX,
                MinNorthingM = Grid.MinY,
                MaxNorthingM = Grid.MaxY,
                CellSizeM = Grid.BinSizeM,
                MinElevationM = Region.MinElevationM,
                MaxElevationM = Region.MaxElevationM,
                MinSlopeMm = Region.MinSlopeMm,
                MaxSlopeMm = Region.MaxSlopeMm,
                CutFillBalanceM3 = Volumes.NetM3,
                LowPointEastingM = LowE,
                LowPointNorthingM = LowN,
                LowPointElevationM = LowBin?.ProposedM ?? 0.0,
                HighPointEastingM = HighE,
                HighPointNorthingM = HighN,
                HighPointElevationM = HighBin?.ProposedM ?? 0.0,
                DrainageHeadingDeg = ResolveDrainageHeadingDeg(Grid)
            };
        }

        /// <summary>
        /// Computes field-wide statistics for the requested surface layer.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Layer">Surface layer to analyze.</param>
        /// <returns>Region statistics in metric units.</returns>
        static public SurfaceRegionStats BuildRegionStats
            (
            DesignGrid Grid,
            SurfaceLayerKind Layer
            )
        {
            List<GridBinData> ValidBins = Grid.Bins.Where(B => B.Valid).ToList();
            if (ValidBins.Count == 0)
            {
                return new SurfaceRegionStats
                {
                    MinElevationM = 0.0,
                    MaxElevationM = 0.0,
                    MeanElevationM = 0.0,
                    MinSlopeMm = 0.0,
                    MaxSlopeMm = 0.0,
                    MeanSlopeMm = 0.0,
                    VolumeAboveReferenceM3 = 0.0,
                    VolumeBelowReferenceM3 = 0.0,
                    DrainageHeadingDeg = ResolveDrainageHeadingDeg(Grid),
                    ValidBinCount = 0
                };
            }

            double MinElev = double.PositiveInfinity;
            double MaxElev = double.NegativeInfinity;
            double SumElev = 0.0;
            foreach (GridBinData Bin in ValidBins)
            {
                double Z = ReadLayer(Bin, Layer);
                MinElev = Math.Min(MinElev, Z);
                MaxElev = Math.Max(MaxElev, Z);
                SumElev += Z;
            }

            double MeanElev = SumElev / ValidBins.Count;
            double BinArea = Grid.BinSizeM * Grid.BinSizeM;
            double Above = 0.0;
            double Below = 0.0;
            foreach (GridBinData Bin in ValidBins)
            {
                double Delta = ReadLayer(Bin, Layer) - MeanElev;
                if (Delta > 0.0)
                    Above += Delta * BinArea;
                else if (Delta < 0.0)
                    Below += (-Delta) * BinArea;
            }

            CollectSlopeStats(Grid, Layer, out double MinSlope, out double MaxSlope, out double MeanSlope);

            return new SurfaceRegionStats
            {
                MinElevationM = MinElev,
                MaxElevationM = MaxElev,
                MeanElevationM = MeanElev,
                MinSlopeMm = MinSlope,
                MaxSlopeMm = MaxSlope,
                MeanSlopeMm = MeanSlope,
                VolumeAboveReferenceM3 = Above,
                VolumeBelowReferenceM3 = Below,
                DrainageHeadingDeg = ResolveDrainageHeadingDeg(Grid),
                ValidBinCount = ValidBins.Count
            };
        }

        /// <summary>
        /// Finds the lowest and highest bins for a surface layer.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Layer">Surface layer.</param>
        /// <param name="LowBin">Lowest bin when found.</param>
        /// <param name="HighBin">Highest bin when found.</param>
        static private void FindExtremePoints
            (
            DesignGrid Grid,
            SurfaceLayerKind Layer,
            out GridBinData? LowBin,
            out GridBinData? HighBin
            )
        {
            LowBin = null;
            HighBin = null;
            double MinZ = double.PositiveInfinity;
            double MaxZ = double.NegativeInfinity;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                double Z = ReadLayer(Bin, Layer);
                if (Z < MinZ)
                {
                    MinZ = Z;
                    LowBin = Bin;
                }

                if (Z > MaxZ)
                {
                    MaxZ = Z;
                    HighBin = Bin;
                }
            }
        }

        /// <summary>
        /// Computes slope magnitude statistics from valid bin neighbourhoods.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Layer">Surface layer.</param>
        /// <param name="MinSlopeMm">Minimum slope magnitude.</param>
        /// <param name="MaxSlopeMm">Maximum slope magnitude.</param>
        /// <param name="MeanSlopeMm">Mean slope magnitude.</param>
        static private void CollectSlopeStats
            (
            DesignGrid Grid,
            SurfaceLayerKind Layer,
            out double MinSlopeMm,
            out double MaxSlopeMm,
            out double MeanSlopeMm
            )
        {
            MinSlopeMm = double.PositiveInfinity;
            MaxSlopeMm = double.NegativeInfinity;
            double Sum = 0.0;
            int Count = 0;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                if (!TryComputeBinSlope(Grid, Layer, Bin.X, Bin.Y, out double Slope))
                    continue;

                MinSlopeMm = Math.Min(MinSlopeMm, Slope);
                MaxSlopeMm = Math.Max(MaxSlopeMm, Slope);
                Sum += Slope;
                Count++;
            }

            if (Count == 0)
            {
                MinSlopeMm = 0.0;
                MaxSlopeMm = 0.0;
                MeanSlopeMm = 0.0;
                return;
            }

            MeanSlopeMm = Sum / Count;
        }

        /// <summary>
        /// Estimates local slope magnitude at a bin using central differences.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Layer">Surface layer.</param>
        /// <param name="X">Bin column index.</param>
        /// <param name="Y">Bin row index.</param>
        /// <param name="SlopeMm">Slope magnitude in m/m.</param>
        /// <returns>True when neighbours exist to estimate slope.</returns>
        static private bool TryComputeBinSlope
            (
            DesignGrid Grid,
            SurfaceLayerKind Layer,
            int X,
            int Y,
            out double SlopeMm
            )
        {
            SlopeMm = 0.0;
            if (!Grid.TryGetValidBin(X, Y, out GridBinData Center))
                return false;

            double DzDx = 0.0;
            double DzDy = 0.0;
            bool HasX = false;
            bool HasY = false;

            if (Grid.TryGetValidBin(X + 1, Y, out GridBinData East) &&
                Grid.TryGetValidBin(X - 1, Y, out GridBinData West))
            {
                DzDx = (ReadLayer(East, Layer) - ReadLayer(West, Layer)) / (2.0 * Grid.BinSizeM);
                HasX = true;
            }

            if (Grid.TryGetValidBin(X, Y + 1, out GridBinData North) &&
                Grid.TryGetValidBin(X, Y - 1, out GridBinData South))
            {
                DzDy = (ReadLayer(North, Layer) - ReadLayer(South, Layer)) / (2.0 * Grid.BinSizeM);
                HasY = true;
            }

            if (!HasX && !HasY)
                return false;

            SlopeMm = Math.Sqrt(DzDx * DzDx + DzDy * DzDy);
            return true;
        }

        /// <summary>
        /// Returns the design plane drainage heading when available.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Heading in degrees clockwise from north.</returns>
        static private double ResolveDrainageHeadingDeg
            (
            DesignGrid Grid
            )
        {
            if (!double.IsNaN(Grid.CompositeSlopeHeadingDeg) && Grid.CompositeSlopeHeadingDeg != 0.0)
                return Grid.CompositeSlopeHeadingDeg;

            double Gradient = Math.Sqrt(Grid.PlaneA * Grid.PlaneA + Grid.PlaneB * Grid.PlaneB);
            if (Gradient <= 1e-12)
                return 0.0;

            double HeadingRad = Math.Atan2(-Grid.PlaneA, -Grid.PlaneB);
            double HeadingDeg = HeadingRad * 180.0 / Math.PI;
            if (HeadingDeg < 0.0)
                HeadingDeg += 360.0;

            return HeadingDeg;
        }

        /// <summary>
        /// Reads the requested elevation from a bin.
        /// </summary>
        /// <param name="Bin">Grid bin.</param>
        /// <param name="Layer">Surface layer.</param>
        /// <returns>Elevation in metres.</returns>
        static private double ReadLayer
            (
            GridBinData Bin,
            SurfaceLayerKind Layer
            )
        {
            return Layer == SurfaceLayerKind.Existing ? Bin.ExistingM : Bin.ProposedM;
        }

        /// <summary>
        /// Returns the UTM easting of a bin centroid.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Bin">Bin or null.</param>
        /// <returns>Easting in metres.</returns>
        static private double BinEasting
            (
            DesignGrid Grid,
            GridBinData? Bin
            )
        {
            if (Bin == null)
                return 0.0;

            return Grid.OriginEastingM + (Bin.X + 0.5) * Grid.BinSizeM;
        }

        /// <summary>
        /// Returns the UTM northing of a bin centroid.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Bin">Bin or null.</param>
        /// <returns>Northing in metres.</returns>
        static private double BinNorthing
            (
            DesignGrid Grid,
            GridBinData? Bin
            )
        {
            if (Bin == null)
                return 0.0;

            return Grid.OriginNorthingM + (Bin.Y + 0.5) * Grid.BinSizeM;
        }
    }
}

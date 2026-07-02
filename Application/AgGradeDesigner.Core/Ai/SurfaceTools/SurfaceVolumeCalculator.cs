using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Aggregated cut and fill volumes between two design grid surface layers.
    /// </summary>
    public readonly struct SurfaceCutFillVolumes
    {
        /// <summary>Volume threshold below which fill is treated as negligible for ratios.</summary>
        private const double NEGLIGIBLE_VOLUME_EPSILON_M3 = 1e-9;

        /// <summary>Cut volume in cubic metres.</summary>
        public double CutM3 { get; init; }

        /// <summary>Fill volume in cubic metres.</summary>
        public double FillM3 { get; init; }

        /// <summary>Net fill minus cut in cubic metres.</summary>
        public double NetM3 => FillM3 - CutM3;

        /// <summary>Cut-to-fill volume ratio, or zero when fill is negligible.</summary>
        public double CutFillRatio => FillM3 > NEGLIGIBLE_VOLUME_EPSILON_M3 ? CutM3 / FillM3 : 0.0;
    }

    /// <summary>
    /// Deterministic cut/fill and balance calculations over <see cref="DesignGrid"/> bins.
    /// </summary>
    static public class SurfaceVolumeCalculator
    {
        /// <summary>Relative tolerance when comparing cut/fill ratio to a target.</summary>
        private const double RATIO_TOLERANCE = 0.02;

        /// <summary>Maximum bisection iterations when searching for balance offset.</summary>
        private const int BALANCE_SEARCH_ITERATIONS = 64;

        /// <summary>Maximum bracket-expansion iterations before bisection.</summary>
        private const int BRACKET_EXPAND_ITERATIONS = 24;

        /// <summary>Initial lower bound for vertical balance-offset search in metres.</summary>
        private const double INITIAL_BALANCE_BRACKET_MIN_M = -50.0;

        /// <summary>Initial upper bound for vertical balance-offset search in metres.</summary>
        private const double INITIAL_BALANCE_BRACKET_MAX_M = 50.0;

        /// <summary>Multiplier applied when expanding the balance-offset search bracket.</summary>
        private const double BRACKET_EXPAND_FACTOR = 2.0;

        /// <summary>Midpoint factor used during bisection (0.5).</summary>
        private const double BISECTION_MIDPOINT_FACTOR = 0.5;

        /// <summary>Volume threshold below which fill is treated as negligible for ratios.</summary>
        private const double NEGLIGIBLE_VOLUME_EPSILON_M3 = 1e-9;

        /// <summary>Elevation delta below which a bin is counted as unchanged in comparisons.</summary>
        private const double BIN_CHANGE_EPSILON_M = 1e-6;

        /// <summary>
        /// Computes cut and fill volumes between two surface layers across valid bins.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="FromLayer">Reference (existing) surface layer.</param>
        /// <param name="ToLayer">Comparison (design) surface layer.</param>
        /// <returns>Cut and fill volumes in cubic metres.</returns>
        static public SurfaceCutFillVolumes CalculateCutFill
            (
            DesignGrid Grid,
            SurfaceLayerKind FromLayer,
            SurfaceLayerKind ToLayer
            )
        {
            double BinArea = Grid.BinSizeM * Grid.BinSizeM;
            double CutM3 = 0.0;
            double FillM3 = 0.0;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                double FromZ = ReadLayer(Bin, FromLayer);
                double ToZ = ReadLayer(Bin, ToLayer);
                double Delta = FromZ - ToZ;
                if (Delta > 0.0)
                    CutM3 += Delta * BinArea;
                else if (Delta < 0.0)
                    FillM3 += (-Delta) * BinArea;
            }

            return new SurfaceCutFillVolumes
            {
                CutM3 = CutM3,
                FillM3 = FillM3
            };
        }

        /// <summary>
        /// Finds the vertical offset in metres to add to the proposed surface to reach a target cut/fill ratio.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="TargetCutFillRatio">Desired cut-to-fill volume ratio.</param>
        /// <returns>Proposed vertical offset in metres; zero when no valid bins exist.</returns>
        static public double FindBalanceOffset
            (
            DesignGrid Grid,
            double TargetCutFillRatio
            )
        {
            if (TargetCutFillRatio <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(TargetCutFillRatio));

            List<GridBinData> ValidBins = Grid.Bins.Where(B => B.Valid).ToList();
            if (ValidBins.Count == 0)
                return 0.0;

            double MinDelta = INITIAL_BALANCE_BRACKET_MIN_M;
            double MaxDelta = INITIAL_BALANCE_BRACKET_MAX_M;
            ExpandBracket(Grid, SurfaceLayerKind.Existing, SurfaceLayerKind.Proposed, TargetCutFillRatio, ref MinDelta, ref MaxDelta);

            for (int Iter = 0; Iter < BALANCE_SEARCH_ITERATIONS; Iter++)
            {
                double Mid = (MinDelta + MaxDelta) * BISECTION_MIDPOINT_FACTOR;
                SurfaceCutFillVolumes Volumes = CalculateCutFillWithProposedOffset(Grid, SurfaceLayerKind.Existing, Mid);
                if (RatioSatisfied(Volumes.CutM3, Volumes.FillM3, TargetCutFillRatio))
                    return Mid;

                double LowScore = RatioScore(CalculateCutFillWithProposedOffset(Grid, SurfaceLayerKind.Existing, MinDelta), TargetCutFillRatio);
                double MidScore = RatioScore(Volumes, TargetCutFillRatio);
                if (LowScore * MidScore <= 0.0)
                    MaxDelta = Mid;
                else
                    MinDelta = Mid;
            }

            return (MinDelta + MaxDelta) * BISECTION_MIDPOINT_FACTOR;
        }

        /// <summary>
        /// Computes summary statistics comparing two surface layers.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="BeforeLayer">Before surface layer.</param>
        /// <param name="AfterLayer">After surface layer.</param>
        /// <returns>Difference statistics in metric units.</returns>
        static public SurfaceCompareStats CompareSurfaces
            (
            DesignGrid Grid,
            SurfaceLayerKind BeforeLayer,
            SurfaceLayerKind AfterLayer
            )
        {
            double MinDelta = double.PositiveInfinity;
            double MaxDelta = double.NegativeInfinity;
            double SumDelta = 0.0;
            int Count = 0;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                double Delta = ReadLayer(Bin, AfterLayer) - ReadLayer(Bin, BeforeLayer);
                MinDelta = Math.Min(MinDelta, Delta);
                MaxDelta = Math.Max(MaxDelta, Delta);
                SumDelta += Delta;
                Count++;
            }

            if (Count == 0)
            {
                return new SurfaceCompareStats
                {
                    MinDeltaM = 0.0,
                    MaxDeltaM = 0.0,
                    MeanDeltaM = 0.0,
                    RaisedBinCount = 0,
                    LoweredBinCount = 0,
                    UnchangedBinCount = 0
                };
            }

            int Raised = 0;
            int Lowered = 0;
            int Unchanged = 0;
            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                double Delta = ReadLayer(Bin, AfterLayer) - ReadLayer(Bin, BeforeLayer);
                if (Delta > BIN_CHANGE_EPSILON_M)
                    Raised++;
                else if (Delta < -BIN_CHANGE_EPSILON_M)
                    Lowered++;
                else
                    Unchanged++;
            }

            return new SurfaceCompareStats
            {
                MinDeltaM = MinDelta,
                MaxDeltaM = MaxDelta,
                MeanDeltaM = SumDelta / Count,
                RaisedBinCount = Raised,
                LoweredBinCount = Lowered,
                UnchangedBinCount = Unchanged
            };
        }

        /// <summary>
        /// Calculates cut/fill between existing and proposed with a uniform proposed offset applied.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="FromLayer">Reference surface layer.</param>
        /// <param name="ProposedOffsetM">Vertical offset added to proposed elevations.</param>
        /// <returns>Cut and fill volumes in cubic metres.</returns>
        static public SurfaceCutFillVolumes CalculateCutFillWithProposedOffset
            (
            DesignGrid Grid,
            SurfaceLayerKind FromLayer,
            double ProposedOffsetM
            )
        {
            double BinArea = Grid.BinSizeM * Grid.BinSizeM;
            double CutM3 = 0.0;
            double FillM3 = 0.0;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                double FromZ = ReadLayer(Bin, FromLayer);
                double ToZ = Bin.ProposedM + ProposedOffsetM;
                double Delta = FromZ - ToZ;
                if (Delta > 0.0)
                    CutM3 += Delta * BinArea;
                else if (Delta < 0.0)
                    FillM3 += (-Delta) * BinArea;
            }

            return new SurfaceCutFillVolumes
            {
                CutM3 = CutM3,
                FillM3 = FillM3
            };
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
        /// Expands the search bracket until the target ratio lies between endpoints.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="FromLayer">Reference surface layer.</param>
        /// <param name="ToLayer">Comparison surface layer (unused; proposed is offset).</param>
        /// <param name="TargetRatio">Target cut/fill ratio.</param>
        /// <param name="MinDelta">Lower bracket bound updated in place.</param>
        /// <param name="MaxDelta">Upper bracket bound updated in place.</param>
        static private void ExpandBracket
            (
            DesignGrid Grid,
            SurfaceLayerKind FromLayer,
            SurfaceLayerKind ToLayer,
            double TargetRatio,
            ref double MinDelta,
            ref double MaxDelta
            )
        {
            for (int Iter = 0; Iter < BRACKET_EXPAND_ITERATIONS; Iter++)
            {
                if (RatioSatisfied(CalculateCutFillWithProposedOffset(Grid, FromLayer, MinDelta), TargetRatio) ||
                    RatioSatisfied(CalculateCutFillWithProposedOffset(Grid, FromLayer, MaxDelta), TargetRatio))
                {
                    return;
                }

                MinDelta *= BRACKET_EXPAND_FACTOR;
                MaxDelta *= BRACKET_EXPAND_FACTOR;
            }
        }

        /// <summary>
        /// Returns true when cut/fill ratio is within tolerance of the target.
        /// </summary>
        /// <param name="CutM3">Cut volume in cubic metres.</param>
        /// <param name="FillM3">Fill volume in cubic metres.</param>
        /// <param name="TargetRatio">Target cut-to-fill ratio.</param>
        /// <returns>True when satisfied.</returns>
        static private bool RatioSatisfied
            (
            double CutM3,
            double FillM3,
            double TargetRatio
            )
        {
            if (FillM3 <= NEGLIGIBLE_VOLUME_EPSILON_M3)
                return CutM3 <= NEGLIGIBLE_VOLUME_EPSILON_M3;

            double Ratio = CutM3 / FillM3;
            return Math.Abs(Ratio - TargetRatio) <= RATIO_TOLERANCE * TargetRatio;
        }

        /// <summary>
        /// Returns true when cut/fill ratio is within tolerance of the target.
        /// </summary>
        /// <param name="Volumes">Computed volumes.</param>
        /// <param name="TargetRatio">Target cut-to-fill ratio.</param>
        /// <returns>True when satisfied.</returns>
        static private bool RatioSatisfied
            (
            SurfaceCutFillVolumes Volumes,
            double TargetRatio
            )
        {
            return RatioSatisfied(Volumes.CutM3, Volumes.FillM3, TargetRatio);
        }

        /// <summary>
        /// Signed error between actual and target cut/fill ratio for bracketing.
        /// </summary>
        /// <param name="Volumes">Computed volumes.</param>
        /// <param name="TargetRatio">Target cut-to-fill ratio.</param>
        /// <returns>Signed ratio error.</returns>
        static private double RatioScore
            (
            SurfaceCutFillVolumes Volumes,
            double TargetRatio
            )
        {
            if (Volumes.FillM3 <= NEGLIGIBLE_VOLUME_EPSILON_M3)
                return -TargetRatio;

            return (Volumes.CutM3 / Volumes.FillM3) - TargetRatio;
        }
    }

    /// <summary>
    /// Difference statistics between two surface layers across valid bins.
    /// </summary>
    public readonly struct SurfaceCompareStats
    {
        /// <summary>Minimum elevation change in metres.</summary>
        public double MinDeltaM { get; init; }

        /// <summary>Maximum elevation change in metres.</summary>
        public double MaxDeltaM { get; init; }

        /// <summary>Mean elevation change in metres.</summary>
        public double MeanDeltaM { get; init; }

        /// <summary>Count of bins raised by more than one millimetre.</summary>
        public int RaisedBinCount { get; init; }

        /// <summary>Count of bins lowered by more than one millimetre.</summary>
        public int LoweredBinCount { get; init; }

        /// <summary>Count of bins unchanged within one millimetre.</summary>
        public int UnchangedBinCount { get; init; }
    }
}

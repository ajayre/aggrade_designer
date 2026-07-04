using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Models;

using NetTopologySuite.Geometries;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Result of one mutating surface operation on a preview grid.
    /// </summary>
    public readonly struct SurfaceMutationResult
    {
        /// <summary>Number of valid bins modified by the operation.</summary>
        public int AffectedBinCount { get; init; }

        /// <summary>Vertical offset applied by rebalance operations in metres.</summary>
        public double AppliedOffsetM { get; init; }

        /// <summary>Target cut/fill ratio for rebalance operations.</summary>
        public double TargetCutFillRatio { get; init; }

        /// <summary>Vertical offset requested by offset_surface in metres.</summary>
        public double DeltaZM { get; init; }

        /// <summary>Plane grade in the easting direction in m/m.</summary>
        public double SlopeX { get; init; }

        /// <summary>Plane grade in the northing direction in m/m.</summary>
        public double SlopeY { get; init; }

        /// <summary>True when a polygon limited the edit extent.</summary>
        public bool PolygonApplied { get; init; }
    }

    /// <summary>
    /// Deterministic Phase 2 mutators that update proposed elevations on a preview grid.
    /// </summary>
    static public class SurfaceMutators
    {
        /// <summary>Elevation delta below which a bin is not counted as modified.</summary>
        private const double BIN_CHANGE_EPSILON_M = 1e-6;

        /// <summary>Offset from bin index to bin centre in cell units.</summary>
        private const double BIN_CELL_CENTRE_OFFSET = 0.5;

        /// <summary>
        /// Adds a uniform vertical offset to proposed elevations across the field or polygon.
        /// </summary>
        /// <param name="Grid">Preview design grid.</param>
        /// <param name="DeltaZM">Vertical shift in metres.</param>
        /// <param name="Polygon">Optional polygon limiting the edit.</param>
        /// <returns>Mutation summary.</returns>
        static public SurfaceMutationResult OffsetSurface
            (
            DesignGrid Grid,
            double DeltaZM,
            Polygon? Polygon = null
            )
        {
            int Affected = 0;
            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                if (!SurfacePolygonHelper.IsBinIncluded(Grid, Bin, Polygon))
                    continue;

                double Before = Bin.ProposedM;
                Bin.ProposedM += DeltaZM;
                if (Math.Abs(Bin.ProposedM - Before) > BIN_CHANGE_EPSILON_M)
                    Affected++;
            }

            Grid.InvalidateBinIndex();
            return new SurfaceMutationResult
            {
                AffectedBinCount = Affected,
                DeltaZM = DeltaZM,
                PolygonApplied = Polygon != null
            };
        }

        /// <summary>
        /// Applies the vertical offset needed to reach a target cut/fill ratio on the proposed surface.
        /// </summary>
        /// <param name="Grid">Preview design grid.</param>
        /// <param name="TargetCutFillRatio">Desired cut-to-fill volume ratio.</param>
        /// <param name="Polygon">Optional polygon limiting the edit.</param>
        /// <returns>Mutation summary including the applied offset.</returns>
        static public SurfaceMutationResult RebalanceSurface
            (
            DesignGrid Grid,
            double TargetCutFillRatio,
            Polygon? Polygon = null
            )
        {
            double OffsetM = SurfaceVolumeCalculator.FindBalanceOffset(Grid, TargetCutFillRatio);
            SurfaceMutationResult Result = OffsetSurface(Grid, OffsetM, Polygon);
            return new SurfaceMutationResult
            {
                AffectedBinCount = Result.AffectedBinCount,
                AppliedOffsetM = OffsetM,
                TargetCutFillRatio = TargetCutFillRatio,
                PolygonApplied = Polygon != null
            };
        }

        /// <summary>
        /// Sets proposed elevations inside a polygon, or the full field, to a plane grade.
        /// </summary>
        /// <param name="Grid">Preview design grid.</param>
        /// <param name="OriginX">Plane anchor easting in metres.</param>
        /// <param name="OriginY">Plane anchor northing in metres.</param>
        /// <param name="OriginZ">Plane anchor elevation in metres.</param>
        /// <param name="SlopeX">Grade in the easting direction in m/m.</param>
        /// <param name="SlopeY">Grade in the northing direction in m/m.</param>
        /// <param name="Polygon">Optional polygon limiting the edit.</param>
        /// <returns>Mutation summary.</returns>
        static public SurfaceMutationResult ApplyPlaneGrade
            (
            DesignGrid Grid,
            double OriginX,
            double OriginY,
            double OriginZ,
            double SlopeX,
            double SlopeY,
            Polygon? Polygon = null
            )
        {
            int Affected = 0;
            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                if (!SurfacePolygonHelper.IsBinIncluded(Grid, Bin, Polygon))
                    continue;

                double EastingM = Grid.OriginEastingM + (Bin.X + BIN_CELL_CENTRE_OFFSET) * Grid.BinSizeM;
                double NorthingM = Grid.OriginNorthingM + (Bin.Y + BIN_CELL_CENTRE_OFFSET) * Grid.BinSizeM;
                double TargetZ = OriginZ + SlopeX * (EastingM - OriginX) + SlopeY * (NorthingM - OriginY);
                if (Math.Abs(Bin.ProposedM - TargetZ) > BIN_CHANGE_EPSILON_M)
                    Affected++;

                Bin.ProposedM = TargetZ;
            }

            Grid.InvalidateBinIndex();
            return new SurfaceMutationResult
            {
                AffectedBinCount = Affected,
                SlopeX = SlopeX,
                SlopeY = SlopeY,
                PolygonApplied = Polygon != null
            };
        }
    }
}

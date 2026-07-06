using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Verifies side-slope daylighting is smooth along the road corridor.
    /// </summary>
    public sealed class RoadSideSlopeSmoothnessTests
    {
        /// <summary>Maximum allowed raised/not-raised transitions per 100 bins along the slope toe.</summary>
        private const int MAX_TOE_TRANSITIONS_PER_100_BINS = 8;

        /// <summary>
        /// Verifies a north-edge road with a one-sided slope does not produce a square-wave toe.
        /// </summary>
        [Fact]
        public void BuildRoad_DesignAgd_OneSideSlope_HasSmoothSlopeToe
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            DesignGrid Grid = Project.DesignGrid!;
            SurfaceSummary Summary = SurfaceAnalysis.BuildSurfaceSummary(Grid);

            string Arguments = $$"""
                {
                  "centerline": [
                    { "x": {{Summary.MinEastingM}}, "y": {{Summary.MaxNorthingM}} },
                    { "x": {{Summary.MaxEastingM}}, "y": {{Summary.MaxNorthingM}} }
                  ],
                  "crown_width_m": 3.6576,
                  "raise_above_proposed_m": 0.4572,
                  "side_slope_hv": 2.0,
                  "side_slope_side": "right"
                }
                """;

            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);
            SurfaceToolResult Result = Dispatcher.Execute(Project, "build_road", Arguments);
            Assert.True(Result.Success, Result.ErrorMessage);

            int? SlopeRowY = FindSouthernSlopeToeRow(Grid);
            Assert.NotNull(SlopeRowY);

            int Transitions = CountRaiseTransitionsAlongRow(Grid, SlopeRowY.Value);
            int BinCount = Grid.Bins.Count(B => B.Valid && B.Y == SlopeRowY.Value);
            double TransitionsPer100 = Transitions * 100.0 / Math.Max(1, BinCount);

            Assert.True(
                TransitionsPer100 <= MAX_TOE_TRANSITIONS_PER_100_BINS,
                $"Expected a smooth slope toe, got {Transitions} transitions across {BinCount} bins ({TransitionsPer100:F1}/100).");
        }

        /// <summary>
        /// Finds a valid row just south of the raised crown where slope daylighting begins.
        /// </summary>
        /// <param name="Grid">Design grid after road build.</param>
        /// <returns>Bin row index or null when not found.</returns>
        static private int? FindSouthernSlopeToeRow
            (
            DesignGrid Grid
            )
        {
            int MaxValidY = Grid.Bins.Where(B => B.Valid).Max(B => B.Y);
            for (int RowY = MaxValidY - 1; RowY >= 0; RowY--)
            {
                if (!Grid.Bins.Any(B => B.Valid && B.Y == RowY))
                    continue;

                if (Grid.Bins.Any(B => B.Valid && B.Y == RowY && IsRaisedByRoad(Grid, B)))
                    return RowY;
            }

            return null;
        }

        /// <summary>
        /// Counts transitions between raised and unraised bins along one row.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="RowY">Row index.</param>
        /// <returns>Transition count.</returns>
        static private int CountRaiseTransitionsAlongRow
            (
            DesignGrid Grid,
            int RowY
            )
        {
            int Transitions = 0;
            bool? PreviousRaised = null;
            foreach (GridBinData Bin in Grid.Bins.Where(B => B.Valid && B.Y == RowY).OrderBy(B => B.X))
            {
                bool Raised = IsRaisedByRoad(Grid, Bin);
                if (PreviousRaised.HasValue && PreviousRaised.Value != Raised)
                    Transitions++;

                PreviousRaised = Raised;
            }

            return Transitions;
        }

        /// <summary>
        /// Returns true when a bin was raised materially above the local design plane.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Bin">Bin to inspect.</param>
        /// <returns>True when the bin appears raised by the road build.</returns>
        static private bool IsRaisedByRoad
            (
            DesignGrid Grid,
            GridBinData Bin
            )
        {
            double EastingM = Grid.OriginEastingM + (Bin.X + 0.5) * Grid.BinSizeM;
            double NorthingM = Grid.OriginNorthingM + (Bin.Y + 0.5) * Grid.BinSizeM;
            double PlaneM = DesignGridProposedSurfaceBuilder.EvaluateProposedPlaneAtUtm(Grid, EastingM, NorthingM);
            return Bin.ProposedM - PlaneM >= 0.05;
        }
    }
}

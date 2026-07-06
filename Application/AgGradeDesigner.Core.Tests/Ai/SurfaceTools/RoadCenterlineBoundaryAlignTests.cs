using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;
using AgGradeDesigner.Core.Tests.Import;
using NetTopologySuite.Geometries;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Verifies boundary-following crown alignment for edge roads.
    /// </summary>
    public sealed class RoadCenterlineBoundaryAlignTests
    {
        /// <summary>Flat crown width in metres.</summary>
        private const double CROWN_WIDTH_M = 2.0;

        /// <summary>
        /// Verifies a curved north boundary offsets the centerline inward while following the curve.
        /// </summary>
        [Fact]
        public void AlignCrownToFieldEdge_CurvedNorthBoundary_FollowsCurveInward()
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateRectGrid(20, 10, 1000.0, 2000.0);
            double MinE = Grid.OriginEastingM;
            double MaxE = Grid.OriginEastingM + Grid.Width * Grid.BinSizeM;
            double MinN = Grid.OriginNorthingM;
            double MaxN = Grid.OriginNorthingM + Grid.Height * Grid.BinSizeM;
            double MidE = (MinE + MaxE) * 0.5;
            double BulgeN = MaxN + Grid.BinSizeM * 2.0;

            Coordinate[] RingCoordinates =
            {
                new Coordinate(MinE, MinN),
                new Coordinate(MaxE, MinN),
                new Coordinate(MaxE, MaxN),
                new Coordinate(MidE + Grid.BinSizeM * 2.0, BulgeN),
                new Coordinate(MidE - Grid.BinSizeM * 2.0, BulgeN),
                new Coordinate(MinE, MaxN),
                new Coordinate(MinE, MinN)
            };
            LinearRing BoundaryRing = new GeometryFactory().CreateLinearRing(RingCoordinates);
            Coordinate Centroid = new Coordinate(MidE, (MinN + MaxN) * 0.5);

            LineString Requested = new GeometryFactory().CreateLineString(new[]
            {
                new Coordinate(MinE + Grid.BinSizeM, BulgeN + Grid.BinSizeM),
                new Coordinate(MidE, BulgeN + Grid.BinSizeM * 1.5),
                new Coordinate(MaxE - Grid.BinSizeM, BulgeN + Grid.BinSizeM)
            });

            LineString Aligned = RoadCenterlineBoundaryAlign.AlignCrownToFieldEdge(
                Grid,
                BoundaryRing,
                Centroid,
                Requested,
                CROWN_WIDTH_M,
                Enabled: true);

            Assert.True(Aligned.Coordinates.Length >= 3);
            foreach (Coordinate Point in Aligned.Coordinates)
            {
                Assert.True(Point.Y <= BulgeN - (CROWN_WIDTH_M * 0.45), $"Expected inward offset, got y={Point.Y}");
                Assert.True(Point.Y >= MaxN - (CROWN_WIDTH_M * 0.6), $"Expected near north edge, got y={Point.Y}");
            }
        }

        /// <summary>
        /// Verifies interior roads are not pulled toward the boundary when alignment is enabled.
        /// </summary>
        [Fact]
        public void AlignCrownToFieldEdge_InteriorCenterline_RemainsUnchanged()
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateRectGrid(20, 10, 1000.0, 2000.0);
            ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(Grid);
            Assert.True(FieldBoundaryGeometry.TryResolveBoundaryRing(
                Project,
                Grid,
                out LinearRing? BoundaryRing,
                out Coordinate Centroid));
            Assert.NotNull(BoundaryRing);

            double CenterE = Grid.OriginEastingM + Grid.Width * Grid.BinSizeM * 0.5;
            double CenterN = Grid.OriginNorthingM + Grid.Height * Grid.BinSizeM * 0.5;
            LineString Requested = new GeometryFactory().CreateLineString(new[]
            {
                new Coordinate(CenterE - Grid.BinSizeM * 2.0, CenterN),
                new Coordinate(CenterE + Grid.BinSizeM * 2.0, CenterN)
            });

            LineString Aligned = RoadCenterlineBoundaryAlign.AlignCrownToFieldEdge(
                Grid,
                BoundaryRing!,
                Centroid,
                Requested,
                CROWN_WIDTH_M,
                Enabled: true);

            Assert.True(Aligned.Coordinates.Length >= Requested.Coordinates.Length);
            Assert.Equal(Requested.Coordinates[0].X, Aligned.Coordinates[0].X, 3);
            Assert.Equal(Requested.Coordinates[0].Y, Aligned.Coordinates[0].Y, 3);
            Assert.Equal(Requested.Coordinates[^1].X, Aligned.Coordinates[^1].X, 3);
            Assert.Equal(Requested.Coordinates[^1].Y, Aligned.Coordinates[^1].Y, 3);

            Coordinate Midpoint = Aligned.Coordinates[Aligned.Coordinates.Length / 2];
            double OuterMaxN = Grid.OriginNorthingM + Grid.Height * Grid.BinSizeM;
            Assert.True(
                OuterMaxN - Midpoint.Y > CROWN_WIDTH_M,
                "Interior centerline should remain away from the north boundary.");
        }

        /// <summary>
        /// Verifies build_road on a north-edge bbox centerline reaches the outer field edge on design.agd.
        /// </summary>
        [Fact]
        public void BuildRoad_DesignAgd_BoundaryAlignedCenterline_ReachesOuterEdge()
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            DesignGrid Grid = Project.DesignGrid!;
            SurfaceSummary Summary = SurfaceAnalysis.BuildSurfaceSummary(Grid);
            Assert.True(FieldBoundaryGeometry.TryComputeValidBinOuterExtents(
                Grid,
                out _,
                out _,
                out _,
                out double OuterMaxN));

            string Arguments = $$"""
                {
                  "centerline": [
                    { "x": {{Summary.MinEastingM}}, "y": {{Summary.MaxNorthingM}} },
                    { "x": {{Summary.MaxEastingM}}, "y": {{Summary.MaxNorthingM}} }
                  ],
                  "crown_width_m": 3.6576,
                  "raise_above_proposed_m": 0.4572
                }
                """;

            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);
            SurfaceToolResult Result = Dispatcher.Execute(Project, "build_road", Arguments);
            Assert.True(Result.Success, Result.ErrorMessage);

            double MaxAffectedOuterNorthM = double.MinValue;
            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                double PlaneM = DesignGridProposedSurfaceBuilder.EvaluateProposedPlaneAtUtm(
                    Grid,
                    Grid.OriginEastingM + (Bin.X + 0.5) * Grid.BinSizeM,
                    Grid.OriginNorthingM + (Bin.Y + 0.5) * Grid.BinSizeM);
                if (Bin.ProposedM - PlaneM < 0.456)
                    continue;

                double BinOuterNorthM = Grid.OriginNorthingM + (Bin.Y + 1.0) * Grid.BinSizeM;
                MaxAffectedOuterNorthM = Math.Max(MaxAffectedOuterNorthM, BinOuterNorthM);
            }

            Assert.True(
                MaxAffectedOuterNorthM >= OuterMaxN - Grid.BinSizeM * 0.51,
                $"Expected road crown to reach outer north edge {OuterMaxN}, got {MaxAffectedOuterNorthM}");
        }
    }
}

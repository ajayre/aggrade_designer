using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Unit tests for <see cref="HaulFlowArrowGeometry"/>.
    /// </summary>
    public sealed class HaulFlowArrowGeometryTests
    {
        /// <summary>
        /// Verifies triangles with vertices outside the valid mask are rejected.
        /// </summary>
        [Fact]
        public void AreAllVerticesInValidCells_RejectsTriangleOutsideValidMask
            (
            )
        {
            HaulBuilder.Grid Grid = CreateUniformGrid(3, 3);
            HaulFlowArrowGeometry.ArrowTriangleUtm Triangle = new HaulFlowArrowGeometry.ArrowTriangleUtm
            {
                TipE = Grid.MinX + (Grid.GridSizeM * 0.5),
                TipN = Grid.MinY - (Grid.GridSizeM * 0.1),
                LeftE = Grid.MinX + (Grid.GridSizeM * 0.3),
                LeftN = Grid.MinY + (Grid.GridSizeM * 0.5),
                RightE = Grid.MinX + (Grid.GridSizeM * 0.7),
                RightN = Grid.MinY + (Grid.GridSizeM * 0.5)
            };

            Assert.False(HaulFlowArrowGeometry.AreAllVerticesInValidCells(Grid, Triangle));
        }

        /// <summary>
        /// Verifies real haul plans still expose interior drawable arrows after validation.
        /// </summary>
        [Fact]
        public void TryBuildDrawableTriangle_AcceptsInteriorArrowsOnGeneratedHaul
            (
            )
        {
            HaulFlowFieldResolver.ClearCache();
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();
            HaulData Data = HaulEngineService.Generate(Project, Settings);
            HaulFlowFieldResolver.ResolvedHaulFlow? Resolved = HaulFlowFieldResolver.TryResolve(Data, Project, Settings)
                ?? throw new InvalidOperationException("Expected resolved haul flow data.");

            int AcceptedInterior = 0;
            int InteriorCandidates = 0;
            int MarginCells = Math.Max(Resolved.ArrowStride, 2);

            for (int Bx = MarginCells; Bx < Resolved.Grid.Width - MarginCells; Bx += Resolved.ArrowStride)
            {
                for (int By = MarginCells; By < Resolved.Grid.Height - MarginCells; By += Resolved.ArrowStride)
                {
                    if (!Resolved.Grid.Valid[By, Bx] || !Resolved.Flow.HasArrow[By, Bx])
                        continue;

                    InteriorCandidates++;
                    if (HaulFlowArrowGeometry.TryBuildDrawableTriangle(
                            Resolved.Grid,
                            Resolved.Flow,
                            Bx,
                            By,
                            out HaulFlowArrowGeometry.ArrowTriangleUtm Triangle))
                    {
                        AcceptedInterior++;
                        Assert.True(HaulFlowArrowGeometry.AreAllVerticesInValidCells(Resolved.Grid, Triangle));
                    }
                }
            }

            Assert.True(InteriorCandidates > 0, "Expected interior arrow candidates on The Shop 2.");
            Assert.True(
                AcceptedInterior > 0,
                $"Expected at least one interior arrow to remain drawable, candidates={InteriorCandidates}.");
        }

        /// <summary>
        /// Verifies invalid UTM points outside the grid extent are rejected.
        /// </summary>
        [Fact]
        public void IsValidUtmPoint_ReturnsFalse_OutsideGridExtent
            (
            )
        {
            HaulBuilder.Grid Grid = new HaulBuilder.Grid(
                2.0,
                3,
                3,
                100.0,
                200.0,
                15,
                true,
                36.0,
                -90.0,
                new double[3, 3],
                new double[3, 3],
                new bool[3, 3]
                {
                    { true, true, true },
                    { true, true, true },
                    { true, true, true }
                },
                1.0,
                1.0,
                50.0,
                new List<List<(double X, double Y)>>());

            Assert.False(HaulFlowArrowGeometry.IsValidUtmPoint(Grid, Grid.MinX - 0.1, Grid.MinY + 1.0));
            Assert.False(HaulFlowArrowGeometry.IsValidUtmPoint(Grid, Grid.MinX + 1.0, Grid.MinY - 0.1));
            Assert.True(HaulFlowArrowGeometry.IsValidUtmPoint(Grid, Grid.MinX + 1.0, Grid.MinY + 1.0));
        }

        /// <summary>
        /// Verifies haul-grid UTM conversion uses the grid zone consistently.
        /// </summary>
        [Fact]
        public void TryUtmToLatLon_ReturnsCoordinatesInsideGridExtent
            (
            )
        {
            HaulBuilder.Grid Grid = CreateUniformGrid(3, 3);
            double CenterE = Grid.MinX + (Grid.GridSizeM * 1.5);
            double CenterN = Grid.MinY + (Grid.GridSizeM * 1.5);

            Assert.True(HaulFlowArrowGeometry.TryUtmToLatLon(Grid, CenterE, CenterN, out double Lat, out double Lon));
            Assert.True(HaulFlowArrowGeometry.TryGetCellCenterLatLon(Grid, 1, 1, out double CellLat, out double CellLon));
            Assert.InRange(Lat, -90.0, 90.0);
            Assert.InRange(Lon, -180.0, 180.0);
            Assert.InRange(CellLat, -90.0, 90.0);
            Assert.InRange(CellLon, -180.0, 180.0);
            Assert.False(HaulFlowArrowGeometry.TryUtmToLatLon(Grid, Grid.MinX - 1.0, CenterN, out _, out _));
        }

        /// <summary>
        /// Creates a square grid with every cell marked valid.
        /// </summary>
        private static HaulBuilder.Grid CreateUniformGrid
            (
            int Width,
            int Height
            )
        {
            bool[,] Valid = new bool[Height, Width];
            for (int By = 0; By < Height; By++)
            {
                for (int Bx = 0; Bx < Width; Bx++)
                    Valid[By, Bx] = true;
            }

            return new HaulBuilder.Grid(
                2.0,
                Width,
                Height,
                100.0,
                200.0,
                15,
                true,
                36.0,
                -90.0,
                new double[Height, Width],
                new double[Height, Width],
                Valid,
                1.0,
                1.0,
                50.0,
                new List<List<(double X, double Y)>>());
        }
    }
}

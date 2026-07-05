using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Unit tests for <see cref="DesignGridProposedSurfaceBuilder"/>.
    /// </summary>
    public sealed class DesignGridProposedSurfaceBuilderTests
    {
        /// <summary>
        /// Maximum neighbour cut/fill jump allowed on a checkerboard proposed grid after smoothing.
        /// </summary>
        private const double MAX_NEIGHBOUR_CUT_FILL_JUMP_M = 0.05;

        /// <summary>
        /// Verifies warp smoothing keeps observed bin elevations within the design tolerance band.
        /// </summary>
        [Fact]
        public void BuildContinuousProposedField_CheckerboardInput_PreservesBinValuesWithinTolerance
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateRectGrid(8, 8, 100.0, 100.0);
            for (int Index = 0; Index < Grid.Bins.Count; Index++)
            {
                GridBinData Bin = Grid.Bins[Index];
                if (!Bin.Valid)
                    continue;

                Bin.ProposedM = (Bin.X + Bin.Y) % 2 == 0 ? 100.0 : 101.0;
            }

            (double[] Field, bool[] ValidMask) = DesignGridProposedSurfaceBuilder.BuildContinuousProposedField(Grid);
            const double WARP_TOLERANCE_M = 0.009;
            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                int Idx = Bin.Y * Grid.Width + Bin.X;
                Assert.True(ValidMask[Idx]);
                Assert.True(
                    Math.Abs(Field[Idx] - Bin.ProposedM) <= WARP_TOLERANCE_M + FieldConstants.EPSILON,
                    $"Bin ({Bin.X},{Bin.Y}) expected {Bin.ProposedM}, got {Field[Idx]:F4}.");
            }
        }

        /// <summary>
        /// Verifies bilinear sampling returns the centre value on a flat continuous field.
        /// </summary>
        [Fact]
        public void TrySampleBilinear_FlatField_ReturnsCentreValue
            (
            )
        {
            const int WIDTH = 4;
            const int HEIGHT = 4;
            const double FLAT_ELEVATION_M = 42.0;
            double[] Field = new double[WIDTH * HEIGHT];
            bool[] ValidMask = new bool[WIDTH * HEIGHT];
            for (int Idx = 0; Idx < Field.Length; Idx++)
            {
                Field[Idx] = FLAT_ELEVATION_M;
                ValidMask[Idx] = true;
            }

            bool Sampled = DesignGridProposedSurfaceBuilder.TrySampleBilinear(
                Field,
                ValidMask,
                WIDTH,
                HEIGHT,
                Ex: 1.5,
                Ey: 1.5,
                out double ValueM);

            Assert.True(Sampled);
            Assert.Equal(FLAT_ELEVATION_M, ValueM, precision: 6);
        }

        /// <summary>
        /// Verifies a use-existing-topology design stays visually flat on FakeField01.
        /// </summary>
        [Fact]
        public void BuildFromProject_UseExistingTopology_ProposedSurfaceIsSpatiallyFlat
            (
            )
        {
            if (!File.Exists(TestDataPaths.FakeField01Ags))
                return;

            SurveyModel Survey = new SurveyModel();
            Survey.LoadAuto(TestDataPaths.FakeField01Ags);

            FieldCreator Creator = new FieldCreator();
            (DesignGrid Grid, _) = Creator.CreateDesign(
                Survey,
                new DesignParameters { UseExistingTopology = true });

            ProjectDocument Project = new ProjectDocument
            {
                Survey = Survey,
                DesignGrid = Grid,
                DesignParameters = new DesignParameters { UseExistingTopology = true }
            };

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);

            double MaxCutFillJump = MeasureMaxViewerNeighbourCutFillJump(Surface);
            Assert.True(
                MaxCutFillJump <= MAX_NEIGHBOUR_CUT_FILL_JUMP_M,
                $"Cut/fill neighbour jump {MaxCutFillJump:F4} m exceeds {MAX_NEIGHBOUR_CUT_FILL_JUMP_M} m.");
        }

        /// <summary>
        /// Measures the largest cut/fill step between adjacent valid viewer cells.
        /// </summary>
        static private double MeasureMaxViewerNeighbourCutFillJump
            (
            FieldSurfaceSampler Surface
            )
        {
            double MaxJump = 0.0;
            for (int Y = 0; Y < Surface.SampleHeight; Y++)
            {
                for (int X = 0; X < Surface.SampleWidth; X++)
                {
                    int Idx = Y * Surface.SampleWidth + X;
                    if (!Surface.Valid[Idx])
                        continue;

                    double CutFill = Surface.Existing[Idx] - Surface.Proposed[Idx];
                    if (X + 1 < Surface.SampleWidth)
                    {
                        int RightIdx = Idx + 1;
                        if (Surface.Valid[RightIdx])
                        {
                            double RightCutFill = Surface.Existing[RightIdx] - Surface.Proposed[RightIdx];
                            MaxJump = Math.Max(MaxJump, Math.Abs(RightCutFill - CutFill));
                        }
                    }

                    if (Y + 1 < Surface.SampleHeight)
                    {
                        int DownIdx = Idx + Surface.SampleWidth;
                        if (Surface.Valid[DownIdx])
                        {
                            double DownCutFill = Surface.Existing[DownIdx] - Surface.Proposed[DownIdx];
                            MaxJump = Math.Max(MaxJump, Math.Abs(DownCutFill - CutFill));
                        }
                    }
                }
            }

            return MaxJump;
        }

        /// <summary>
        /// Verifies a mild slope design renders as a uniform plane across the entire viewer field.
        /// </summary>
        [Fact]
        public void BuildFromProject_MildSlopeDesign_ProposedMatchesDesignPlaneAcrossField
            (
            )
        {
            if (!File.Exists(TestDataPaths.FakeField01Ags))
                return;

            SurveyModel Survey = new SurveyModel();
            Survey.LoadAuto(TestDataPaths.FakeField01Ags);

            FieldCreator Creator = new FieldCreator();
            (DesignGrid Grid, _) = Creator.CreateDesign(
                Survey,
                new DesignParameters
                {
                    MainSlopeDirectionDeg = 90,
                    MainSlopePercent = 0.1,
                    CrossSlopePercent = 0.0
                });

            Assert.True(DesignGridProposedSurfaceBuilder.IsUniformPlaneDesign(Grid));

            ProjectDocument Project = new ProjectDocument
            {
                Survey = Survey,
                DesignGrid = Grid,
                DesignParameters = new DesignParameters { MainSlopePercent = 0.1 }
            };

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);

            const double PLANE_SAMPLE_TOLERANCE_M = 0.001;
            for (int Sy = 0; Sy < Surface.SampleHeight; Sy++)
            {
                double Lat = Surface.MaxLat -
                    (Surface.MaxLat - Surface.MinLat) * Sy / Math.Max(1, Surface.SampleHeight - 1);
                for (int Sx = 0; Sx < Surface.SampleWidth; Sx++)
                {
                    int Idx = Sy * Surface.SampleWidth + Sx;
                    if (!Surface.Valid[Idx])
                        continue;

                    double Lon = Surface.MinLon +
                        (Surface.MaxLon - Surface.MinLon) * Sx / Math.Max(1, Surface.SampleWidth - 1);
                    UTM.UTMCoordinate Utm = UTM.FromLatLon(Lat, Lon);
                    double ExpectedM = DesignGridProposedSurfaceBuilder.EvaluateProposedPlaneAtUtm(
                        Grid,
                        Utm.Easting,
                        Utm.Northing);

                    Assert.Equal(
                        ExpectedM,
                        Surface.Proposed[Idx],
                        PLANE_SAMPLE_TOLERANCE_M);
                }
            }
        }

        /// <summary>
        /// Verifies localized bin edits are not treated as a uniform plane design.
        /// </summary>
        [Fact]
        public void IsUniformPlaneDesign_LocalizedBinEdit_ReturnsFalse
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateRectGrid(4, 4, 100.0, 100.0);
            Grid.PlaneA = 0.0;
            Grid.PlaneB = 0.001;
            Grid.PlaneC = 100.0;
            Grid.EnsureCentroidsFromUtm();

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                UTM.UTMCoordinate Utm = UTM.FromLatLon(Bin.CentroidLat, Bin.CentroidLon);
                Bin.ProposedM = DesignGridProposedSurfaceBuilder.EvaluateProposedPlaneAtUtm(
                    Grid,
                    Utm.Easting,
                    Utm.Northing);
            }

            Grid.Bins[0].ProposedM += 1.0;
            Assert.False(DesignGridProposedSurfaceBuilder.IsUniformPlaneDesign(Grid));
        }

        /// <summary>
        /// Verifies bilinear sampling works at a subgrid edge with partial corners.
        /// </summary>
        [Fact]
        public void TrySampleContinuousField_PartialCorners_ReturnsAverage
            (
            )
        {
            const int WIDTH = 4;
            const int HEIGHT = 4;
            double[] Field = new double[WIDTH * HEIGHT];
            bool[] SampleMask = new bool[WIDTH * HEIGHT];
            for (int Y = 0; Y < HEIGHT; Y++)
            {
                for (int X = 0; X < WIDTH; X++)
                {
                    int Idx = Y * WIDTH + X;
                    if (X >= 1 && Y >= 1)
                    {
                        Field[Idx] = 100.0;
                        SampleMask[Idx] = true;
                    }
                }
            }

            bool Sampled = DesignGridProposedSurfaceBuilder.TrySampleContinuousField(
                Field,
                SampleMask,
                WIDTH,
                HEIGHT,
                Ex: 0.5,
                Ey: 0.5,
                out double ValueM);

            Assert.True(Sampled);
            Assert.Equal(100.0, ValueM, precision: 6);
        }
    }
}

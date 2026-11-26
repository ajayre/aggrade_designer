using AgGradeDesigner.Core.Haul;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Verifies road and pad trips keep the haul section instead of the field-cell clamp.
    /// </summary>
    public sealed class HaulRoadSurfaceTests
    {
        /// <summary>Flat ground elevation used by the synthetic grid, in metres.</summary>
        private const double GROUND_M = 30.0;

        /// <summary>Design target above the ground, so a clamped fill stops here.</summary>
        private const double TARGET_M = 31.0;

        /// <summary>Fill depth large enough to be obvious if a road cell is simulated from the pass.</summary>
        private const double HOSTILE_FILL_M = 20.0;

        /// <summary>Road-stage cut that must not be stopped at the field target.</summary>
        private const double ROAD_CUT_M = 1.5;

        /// <summary>Cell size for the synthetic haul grid, in metres. Smaller than the road toe so a cell centre stays on the section after pan snap.</summary>
        private const double CELL_M = 2.0;

        /// <summary>Grid width and height in cells.</summary>
        private const int GRID_CELLS = 8;

        /// <summary>Shoulder height of the test road, in feet.</summary>
        private const double ROAD_HEIGHT_FT = 1.0;

        /// <summary>Side slope of the test road, horizontal over vertical.</summary>
        private const double ROAD_SIDE_SLOPE = 3.0;

        /// <summary>Minimum lanes on the test road.</summary>
        private const int ROAD_MIN_LANES = 1;

        /// <summary>Minimum roadbed of the test road, in feet.</summary>
        private const double ROAD_MIN_ROADBED_FT = 8.5;

        /// <summary>Offset from a cell centre that still lies on the section, in metres.</summary>
        private const double BETWEEN_CENTRES_M = 0.25;

        /// <summary>
        /// Verifies a field fill stops at the cell target.
        /// </summary>
        [Fact]
        public void FieldFill_StopsAtCellTarget
            (
            )
        {
            HaulBuilder.Grid Grid = CreateFlatGrid(GROUND_M, TARGET_M);
            HaulBuilder.Pass Pass = PassAt(0, 0, HOSTILE_FILL_M, Cut: false, HaulBuilder.HaulStage.Flow);

            HaulBuilder.Grid Leveled = HaulGridLeveling.BuildAfterPasses(Grid, new[] { Pass });

            Assert.Equal(TARGET_M, Leveled.Existing[0, 0]);
        }

        /// <summary>
        /// Verifies a road cut is applied in full when the finished section is not available.
        /// </summary>
        [Fact]
        public void RoadCut_WithoutSection_AppliesRecordedDepth
            (
            )
        {
            HaulBuilder.Grid Grid = CreateFlatGrid(GROUND_M, TARGET_M);
            HaulBuilder.Pass Pass = PassAt(1, 1, ROAD_CUT_M, Cut: true, HaulBuilder.HaulStage.Road);

            HaulBuilder.Grid Leveled = HaulGridLeveling.BuildAfterPasses(Grid, new[] { Pass });

            Assert.Equal(GROUND_M - ROAD_CUT_M, Leveled.Existing[1, 1]);
        }

        /// <summary>
        /// Verifies a hostile road fill is replaced by the haul section, not added onto the cell.
        /// </summary>
        [Fact]
        public void RoadFill_WithSection_UsesHaulSectionInsteadOfCellDepth
            (
            )
        {
            HaulBuilder.Grid Grid = CreateFlatGrid(GROUND_M, TARGET_M);
            HaulBuilder.HaulBuilderOptions Options = RoadOptions(Grid);
            HaulBuilder.RoadPadSurface Surface = HaulBuilder.RoadPadBuild.Prepare(Grid, Options, null);
            HaulRoadSurface Road = HaulRoadSurface.FromEngine(Grid, Surface, Options);
            (int Col, int Row) OnRoad = CellOnSection(Grid, Road);
            HaulBuilder.Pass Pass = PassAt(OnRoad.Col, OnRoad.Row, HOSTILE_FILL_M, Cut: false, HaulBuilder.HaulStage.Road);

            HaulBuilder.Grid Leveled = HaulGridLeveling.BuildAfterPasses(Grid, new[] { Pass }, Road);

            double CentreX = Leveled.CenterX[OnRoad.Row, OnRoad.Col];
            double CentreY = Leveled.CenterY[OnRoad.Row, OnRoad.Col];
            Assert.True(Road.TryElevation(CentreX, CentreY, out double SectionM));
            Assert.Equal(SectionM, Leveled.Existing[OnRoad.Row, OnRoad.Col], precision: 6);
            Assert.True(SectionM < GROUND_M + HOSTILE_FILL_M);
            Assert.True(SectionM > Surface.FieldDesignM[OnRoad.Row, OnRoad.Col]);
        }

        /// <summary>
        /// Verifies a point between cell centres uses the haul section.
        /// </summary>
        [Fact]
        public void PointBetweenCellCentres_UsesHaulSection
            (
            )
        {
            HaulBuilder.Grid Grid = CreateFlatGrid(GROUND_M, TARGET_M);
            HaulBuilder.HaulBuilderOptions Options = RoadOptions(Grid);
            HaulBuilder.RoadPadSurface Surface = HaulBuilder.RoadPadBuild.Prepare(Grid, Options, null);
            HaulRoadSurface Road = HaulRoadSurface.FromEngine(Grid, Surface, Options);
            (int Col, int Row) OnRoad = CellOnSection(Grid, Road);
            double CentreX = Grid.CenterX[OnRoad.Row, OnRoad.Col];
            double CentreY = Grid.CenterY[OnRoad.Row, OnRoad.Col];
            Assert.True(Road.TryElevation(CentreX, CentreY, out double CentreM));
            Assert.True(CentreM > Surface.FieldDesignM[OnRoad.Row, OnRoad.Col]);

            bool North = Road.TryElevation(CentreX, CentreY + BETWEEN_CENTRES_M, out double NorthM);
            bool South = Road.TryElevation(CentreX, CentreY - BETWEEN_CENTRES_M, out double SouthM);
            Assert.True(North || South);
            double OffCentreM = North ? NorthM : SouthM;
            Assert.True(OffCentreM > Surface.FieldDesignM[OnRoad.Row, OnRoad.Col]);
            Assert.True(OffCentreM < GROUND_M + HOSTILE_FILL_M);
        }

        /// <summary>
        /// Finds a cell centre that the finished section raises.
        /// </summary>
        /// <param name="Grid">Haul grid.</param>
        /// <param name="Road">Finished section.</param>
        /// <returns>Column and row of a cell on the embankment.</returns>
        private static (int Col, int Row) CellOnSection
            (
            HaulBuilder.Grid Grid,
            HaulRoadSurface Road
            )
        {
            for (int Row = 0; Row < Grid.Height; Row++)
            {
                for (int Col = 0; Col < Grid.Width; Col++)
                {
                    if (Road.TryElevation(Grid.CenterX[Row, Col], Grid.CenterY[Row, Col], out _))
                        return (Col, Row);
                }
            }

            throw new InvalidOperationException("The test road did not land on a cell centre.");
        }

        /// <summary>
        /// Builds haul options with one east-west road through the middle of the grid.
        /// </summary>
        /// <param name="Grid">Grid the road must sit inside.</param>
        /// <returns>Options whose road list contains that centreline.</returns>
        private static HaulBuilder.HaulBuilderOptions RoadOptions
            (
            HaulBuilder.Grid Grid
            )
        {
            double Northing = Grid.CenterY[GRID_CELLS / 2, 0];
            double StartEasting = Grid.MinX + CELL_M;
            double EndEasting = Grid.MinX + ((GRID_CELLS - 1) * CELL_M);
            HaulBuilder.HaulBuilderOptions Options = new HaulBuilder.HaulBuilderOptions();
            Options.Roads.Add(new HaulBuilder.RoadCentreline(new (double X, double Y)[]
            {
                (StartEasting, Northing),
                (EndEasting, Northing)
            })
            {
                MinLanes = ROAD_MIN_LANES,
                MinRoadbedFt = ROAD_MIN_ROADBED_FT,
                HeightFt = ROAD_HEIGHT_FT,
                SideSlope = ROAD_SIDE_SLOPE
            });
            return Options;
        }

        /// <summary>
        /// Builds one cut or fill trip on a single cell.
        /// </summary>
        /// <param name="Col">Column.</param>
        /// <param name="Row">Row.</param>
        /// <param name="DepthM">Depth in metres.</param>
        /// <param name="Cut">True for a cut trip, false for a fill trip.</param>
        /// <param name="Stage">Builder that produced the trip.</param>
        /// <returns>The trip.</returns>
        private static HaulBuilder.Pass PassAt
            (
            int Col,
            int Row,
            double DepthM,
            bool Cut,
            HaulBuilder.HaulStage Stage
            )
        {
            List<(ValueTuple<int, int>, double)> Cells = new List<(ValueTuple<int, int>, double)>
            {
                ((Col, Row), DepthM)
            };
            List<(ValueTuple<int, int>, double)> Empty = new List<(ValueTuple<int, int>, double)>();
            return new HaulBuilder.Pass(
                Cut ? Cells : Empty,
                Cut ? Empty : Cells,
                0,
                0,
                (0, 0),
                (0, 0),
                (0, 0),
                (0, 0))
            {
                Stage = Stage
            };
        }

        /// <summary>
        /// Builds a flat valid haul grid.
        /// </summary>
        /// <param name="GroundM">Existing elevation in metres.</param>
        /// <param name="TargetM">Target elevation in metres.</param>
        /// <returns>The grid.</returns>
        private static HaulBuilder.Grid CreateFlatGrid
            (
            double GroundM,
            double TargetM
            )
        {
            double[,] Existing = new double[GRID_CELLS, GRID_CELLS];
            double[,] Target = new double[GRID_CELLS, GRID_CELLS];
            bool[,] Valid = new bool[GRID_CELLS, GRID_CELLS];
            for (int Row = 0; Row < GRID_CELLS; Row++)
            {
                for (int Col = 0; Col < GRID_CELLS; Col++)
                {
                    Existing[Row, Col] = GroundM;
                    Target[Row, Col] = TargetM;
                    Valid[Row, Col] = true;
                }
            }

            return new HaulBuilder.Grid(
                CELL_M,
                GRID_CELLS,
                GRID_CELLS,
                500000.0,
                4000000.0,
                15,
                true,
                36.0,
                -90.0,
                Existing,
                Target,
                Valid,
                1.2,
                1.0,
                150.0,
                new List<List<(double X, double Y)>>());
        }
    }
}

using AgGradeDesigner.Core.Haul;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Pan-target miss persistence and the outline drawn on the haul cut/fill map.
    /// </summary>
    public sealed class HaulPanTargetMissTests
    {
        /// <summary>Pixels along one cell edge in the highlight fixture.</summary>
        private const int PIXELS_PER_CELL = 8;

        /// <summary>Grid width and height of the highlight fixture.</summary>
        private const int GRID_CELLS = 2;

        /// <summary>On-grade green from the cut/fill palette.</summary>
        private const byte ON_GRADE_GREEN = 205;

        /// <summary>
        /// Copies the engine warning, including every failing cell, into the persisted row.
        /// </summary>
        [Fact]
        public void FromEngine_CopiesTheMiss
            (
            )
        {
            HaulBuilder.PanTargetMiss Miss = new HaulBuilder.PanTargetMiss(
                116,
                50,
                519.7,
                12.5,
                3.25,
                new HaulBuilder.PanTargetMiss.Cell[]
                {
                    new HaulBuilder.PanTargetMiss.Cell(116, 50),
                    new HaulBuilder.PanTargetMiss.Cell(10, 4)
                });

            HaulPanTargetMissDto? Dto = HaulPanTargetMissDto.FromEngine(Miss);

            Assert.NotNull(Dto);
            Assert.Equal(2, Dto.CellCount);
            Assert.Equal(116, Dto.WorstColumn);
            Assert.Equal(50, Dto.WorstRow);
            Assert.Equal(519.7, Dto.WorstMm, 6);
            Assert.Equal(12.5, Dto.AboveBcy, 6);
            Assert.Equal(3.25, Dto.BelowBcy, 6);
            Assert.Equal(116, Dto.Cells[0].X);
            Assert.Equal(50, Dto.Cells[0].Y);
            Assert.Equal(10, Dto.Cells[1].X);
            Assert.Equal(4, Dto.Cells[1].Y);
        }

        /// <summary>
        /// A finished pan produces no persisted warning.
        /// </summary>
        [Fact]
        public void FromEngine_NullWhenThereIsNoMiss
            (
            )
        {
            Assert.Null(HaulPanTargetMissDto.FromEngine(null));
        }

        /// <summary>
        /// The outline sits on the failing cell and leaves the cut/fill colour in the middle.
        /// </summary>
        [Fact]
        public void Paint_OutlinesTheFailingCell()
        {
            HaulBuilder.Grid Grid = CreateOnGradeGrid();
            byte[] Map = HaulBuilder.Render.RenderPng(Grid, null, PIXELS_PER_CELL, 0);
            HaulPanTargetMissDto Miss = new HaulPanTargetMissDto
            {
                CellCount = 1,
                WorstColumn = 1,
                WorstRow = 1,
                WorstMm = 40.0,
                Cells = new List<HaulPanTargetMissDto.Cell>
                {
                    new HaulPanTargetMissDto.Cell { X = 1, Y = 1 }
                }
            };

            byte[] Marked = HaulPanTargetHighlight.Paint(
                Map,
                GRID_CELLS,
                GRID_CELLS,
                PIXELS_PER_CELL,
                Miss);

            using SKBitmap Bitmap = SKBitmap.Decode(Marked);
            Assert.NotNull(Bitmap);
            // Row 1 is the north edge of a 2-row grid, so the outlined cell starts at the top of the image.
            SKColor Outline = Bitmap.GetPixel(PIXELS_PER_CELL, 0);
            SKColor Interior = Bitmap.GetPixel(PIXELS_PER_CELL + (PIXELS_PER_CELL / 2), PIXELS_PER_CELL / 2);
            SKColor OtherCell = Bitmap.GetPixel(0, PIXELS_PER_CELL);

            Assert.Equal(255, Outline.Red);
            Assert.Equal(0, Outline.Green);
            Assert.Equal(255, Outline.Blue);
            Assert.Equal(0, Interior.Red);
            Assert.Equal(ON_GRADE_GREEN, Interior.Green);
            Assert.Equal(0, Interior.Blue);
            Assert.Equal(0, OtherCell.Red);
            Assert.Equal(ON_GRADE_GREEN, OtherCell.Green);
            Assert.Equal(0, OtherCell.Blue);
        }

        /// <summary>
        /// An empty miss does not rewrite the map.
        /// </summary>
        [Fact]
        public void Paint_LeavesTheMapWhenNothingFailed
            (
            )
        {
            byte[] Map = new byte[] { 1, 2, 3 };

            byte[] Painted = HaulPanTargetHighlight.Paint(
                Map,
                GRID_CELLS,
                GRID_CELLS,
                PIXELS_PER_CELL,
                null);

            Assert.Same(Map, Painted);
        }

        /// <summary>
        /// Builds a flat on-grade grid so unmarked cells stay green.
        /// </summary>
        /// <returns>The grid.</returns>
        private static HaulBuilder.Grid CreateOnGradeGrid
            (
            )
        {
            double[,] Existing = new double[GRID_CELLS, GRID_CELLS];
            double[,] Target = new double[GRID_CELLS, GRID_CELLS];
            bool[,] Valid = new bool[GRID_CELLS, GRID_CELLS];
            for (int Row = 0; Row < GRID_CELLS; Row++)
            {
                for (int Column = 0; Column < GRID_CELLS; Column++)
                {
                    Existing[Row, Column] = 100.0;
                    Target[Row, Column] = 100.0;
                    Valid[Row, Column] = true;
                }
            }

            return new HaulBuilder.Grid(
                1.0,
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

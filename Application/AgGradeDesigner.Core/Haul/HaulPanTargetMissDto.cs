namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Road and pad cells that finished outside the finish tolerance.
    /// The haul plan is still complete. Null on a payload means there was no miss.
    /// </summary>
    public sealed class HaulPanTargetMissDto
    {
        /// <summary>
        /// One failing pan cell, stored by grid column and row.
        /// </summary>
        public sealed class Cell
        {
            /// <summary>Grid column.</summary>
            public int X { get; set; }

            /// <summary>Grid row.</summary>
            public int Y { get; set; }
        }

        /// <summary>How many pan cells finished outside the finish tolerance.</summary>
        public int CellCount { get; set; }

        /// <summary>Column of the cell with the largest absolute miss.</summary>
        public int WorstColumn { get; set; }

        /// <summary>Row of the cell with the largest absolute miss.</summary>
        public int WorstRow { get; set; }

        /// <summary>
        /// Signed miss of the worst cell, in millimetres.
        /// Positive means the finished ground is above the pan target.
        /// </summary>
        public double WorstMm { get; set; }

        /// <summary>Bank volume of failing cells that finished above the pan target, in BCY.</summary>
        public double AboveBcy { get; set; }

        /// <summary>
        /// Bank volume of failing cells that finished below the pan target, in BCY.
        /// Compaction is included.
        /// </summary>
        public double BelowBcy { get; set; }

        /// <summary>Failing cells to outline on the cut/fill map.</summary>
        public List<Cell> Cells { get; set; } = new List<Cell>();

        /// <summary>
        /// Copies an engine warning into the persisted haul payload.
        /// </summary>
        /// <param name="Miss">Engine warning. Null when the pan finished inside the tolerance.</param>
        /// <returns>The payload row, or null when there is nothing to show.</returns>
        public static HaulPanTargetMissDto? FromEngine
            (
            HaulBuilder.PanTargetMiss? Miss
            )
        {
            if (Miss == null || Miss.CellCount <= 0)
                return null;

            HaulPanTargetMissDto Dto = new HaulPanTargetMissDto
            {
                CellCount = Miss.CellCount,
                WorstColumn = Miss.WorstColumn,
                WorstRow = Miss.WorstRow,
                WorstMm = Miss.WorstMm,
                AboveBcy = Miss.AboveBcy,
                BelowBcy = Miss.BelowBcy
            };

            foreach (HaulBuilder.PanTargetMiss.Cell Cell in Miss.Cells)
            {
                Dto.Cells.Add(new Cell
                {
                    X = Cell.Column,
                    Y = Cell.Row
                });
            }

            return Dto;
        }
    }
}

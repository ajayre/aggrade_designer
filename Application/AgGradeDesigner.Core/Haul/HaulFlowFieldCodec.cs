namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Encodes and decodes haul transport flow fields for project-file persistence.
    /// </summary>
    public static class HaulFlowFieldCodec
    {
        /// <summary>
        /// Captures the haul grid and flow field produced by a successful engine run.
        /// </summary>
        /// <param name="Grid">Processing grid from the haul engine.</param>
        /// <param name="Flow">Transport flow field from the haul engine.</param>
        /// <returns>Serializable flow-field snapshot, or null when inputs are incomplete.</returns>
        public static HaulFlowFieldDto? Encode
            (
            HaulBuilder.Grid? Grid,
            HaulBuilder.FlowField? Flow
            )
        {
            if (Grid == null || Flow == null || Grid.Width <= 0 || Grid.Height <= 0)
                return null;

            int CellCount = Grid.Width * Grid.Height;
            bool[] Valid = new bool[CellCount];
            double[] Fx = new double[CellCount];
            double[] Fy = new double[CellCount];
            double[] Magnitude = new double[CellCount];
            bool[] HasArrow = new bool[CellCount];

            for (int By = 0; By < Grid.Height; By++)
            {
                for (int Bx = 0; Bx < Grid.Width; Bx++)
                {
                    int Index = ToFlatIndex(Bx, By, Grid.Width);
                    Valid[Index] = Grid.Valid[By, Bx];
                    Fx[Index] = Flow.Fx[By, Bx];
                    Fy[Index] = Flow.Fy[By, Bx];
                    Magnitude[Index] = Flow.Magnitude[By, Bx];
                    HasArrow[Index] = Flow.HasArrow[By, Bx];
                }
            }

            return new HaulFlowFieldDto
            {
                GridSizeM = Grid.GridSizeM,
                Width = Grid.Width,
                Height = Grid.Height,
                MinX = Grid.MinX,
                MinY = Grid.MinY,
                Zone = Grid.Zone,
                IsNorthern = Grid.IsNorthern,
                MeanLat = Grid.MeanLat,
                MeanLon = Grid.MeanLon,
                Valid = Valid,
                Fx = Fx,
                Fy = Fy,
                Magnitude = Magnitude,
                HasArrow = HasArrow,
                Mode = Flow.Mode
            };
        }

        /// <summary>
        /// Restores haul grid and flow field objects from a persisted snapshot.
        /// </summary>
        /// <param name="Dto">Persisted flow-field snapshot.</param>
        /// <param name="Grid">Reconstructed processing grid when decoding succeeds.</param>
        /// <param name="Flow">Reconstructed transport flow field when decoding succeeds.</param>
        /// <returns>True when the snapshot is structurally valid.</returns>
        public static bool TryDecode
            (
            HaulFlowFieldDto? Dto,
            out HaulBuilder.Grid? Grid,
            out HaulBuilder.FlowField? Flow
            )
        {
            Grid = null;
            Flow = null;
            if (Dto == null || !IsStructurallyValid(Dto))
                return false;

            double[,] Existing = new double[Dto.Height, Dto.Width];
            double[,] Target = new double[Dto.Height, Dto.Width];
            bool[,] Valid = new bool[Dto.Height, Dto.Width];
            double[,] Phi = new double[Dto.Height, Dto.Width];
            double[,] Fx = new double[Dto.Height, Dto.Width];
            double[,] Fy = new double[Dto.Height, Dto.Width];
            double[,] Magnitude = new double[Dto.Height, Dto.Width];
            bool[,] HasArrow = new bool[Dto.Height, Dto.Width];

            for (int By = 0; By < Dto.Height; By++)
            {
                for (int Bx = 0; Bx < Dto.Width; Bx++)
                {
                    int Index = ToFlatIndex(Bx, By, Dto.Width);
                    Valid[By, Bx] = Dto.Valid[Index];
                    Fx[By, Bx] = Dto.Fx[Index];
                    Fy[By, Bx] = Dto.Fy[Index];
                    Magnitude[By, Bx] = Dto.Magnitude[Index];
                    HasArrow[By, Bx] = Dto.HasArrow[Index];
                }
            }

            Grid = new HaulBuilder.Grid(
                Dto.GridSizeM,
                Dto.Width,
                Dto.Height,
                Dto.MinX,
                Dto.MinY,
                Dto.Zone,
                Dto.IsNorthern,
                Dto.MeanLat,
                Dto.MeanLon,
                Existing,
                Target,
                Valid,
                HaulBuilder.Constants.DEFAULT_SWELL_FACTOR,
                HaulBuilder.Constants.DEFAULT_COMPACTION,
                HaulBuilder.Constants.DEFAULT_MAX_DEPTH_MM,
                new List<List<(double X, double Y)>>());

            Flow = new HaulBuilder.FlowField(
                Phi,
                Fx,
                Fy,
                Magnitude,
                HasArrow,
                string.IsNullOrWhiteSpace(Dto.Mode) ? "poisson" : Dto.Mode);

            return true;
        }

        /// <summary>
        /// Returns true when the persisted snapshot exposes at least one flow arrow.
        /// </summary>
        /// <param name="Dto">Persisted flow-field snapshot.</param>
        /// <returns>True when at least one arrow cell is marked visible.</returns>
        public static bool HasVisibleArrows
            (
            HaulFlowFieldDto? Dto
            )
        {
            if (!IsStructurallyValid(Dto))
                return false;

            for (int Index = 0; Index < Dto!.HasArrow.Length; Index++)
            {
                if (!Dto.Valid[Index])
                    continue;

                if (Dto.HasArrow[Index])
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true when all required buffers are present with matching dimensions.
        /// </summary>
        /// <param name="Dto">Persisted flow-field snapshot.</param>
        /// <returns>True when the snapshot can be decoded.</returns>
        public static bool IsStructurallyValid
            (
            HaulFlowFieldDto? Dto
            )
        {
            if (Dto == null || Dto.Width <= 0 || Dto.Height <= 0 || Dto.GridSizeM <= 0.0)
                return false;

            int CellCount = Dto.Width * Dto.Height;
            return Dto.Valid.Length == CellCount &&
                Dto.Fx.Length == CellCount &&
                Dto.Fy.Length == CellCount &&
                Dto.Magnitude.Length == CellCount &&
                Dto.HasArrow.Length == CellCount;
        }

        /// <summary>
        /// Maps a grid cell to a row-major flat buffer index.
        /// </summary>
        /// <param name="Bx">Cell column index.</param>
        /// <param name="By">Cell row index.</param>
        /// <param name="Width">Grid width in bins.</param>
        /// <returns>Flat buffer index.</returns>
        private static int ToFlatIndex
            (
            int Bx,
            int By,
            int Width
            )
        {
            return (By * Width) + Bx;
        }
    }
}

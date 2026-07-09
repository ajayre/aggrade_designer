namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// SCS curve-number ponding analysis compatible with AgGrade <c>PondingMapGenerator</c> (WATER-SPEC §8.1).
    /// </summary>
    public static class PondingAnalysisEngine
    {
        /// <summary>Eight-connected neighbour offsets for grid traversal.</summary>
        private static readonly (int Dr, int Dc)[] Neigh8 =
        [
            (-1, -1), (-1, 0), (-1, 1),
            (0, -1),           (0, 1),
            (1, -1),  (1, 0),  (1, 1),
        ];

        /// <summary>RGBA bytes per overlay cell.</summary>
        private const int RGBA_BYTES_PER_CELL = 4;

        /// <summary>Midpoint factor used in binary search for volume-to-depth mapping.</summary>
        private const double BINARY_SEARCH_MIDPOINT_FACTOR = 0.5;

        /// <summary>
        /// Runs ponding analysis on a prepared elevation grid and returns a display-ready cache entry.
        /// </summary>
        /// <param name="Grid">Elevation grid for the target basis surface.</param>
        /// <param name="Settings">Ponding parameters.</param>
        /// <param name="BasisSurface">Basis surface kind stored on the cache entry.</param>
        /// <param name="RouteScrubIndex">Route scrub index for haul caches; otherwise null.</param>
        /// <param name="Progress">Optional progress reporter.</param>
        /// <returns>Computed cache entry including depth grid and overlay RGBA.</returns>
        public static WaterLayerCacheEntry Compute
            (
            WaterElevationGridBuilder.ElevationGrid Grid,
            WaterSettings Settings,
            WaterBasisSurfaceKind BasisSurface,
            int? RouteScrubIndex,
            IProgress<int>? Progress = null
            )
        {
            Progress?.Report(WaterConstants.PROGRESS_START_PERCENT);
            RunPondingModel(
                Grid.Dem,
                Grid.Rows,
                Grid.Cols,
                Grid.CellSizeM,
                WaterElevationGridBuilder.NO_DATA_VALUE,
                Settings.PondingRainfallMm,
                Settings.PondingCurveNumber,
                Progress,
                out float[] PondDepthFlat,
                out byte[] OverlayRgba);

            byte[] PayloadBlob = Array.Empty<byte>();
            WaterLayerCacheEntry Entry = new WaterLayerCacheEntry
            {
                BasisSurface = BasisSurface,
                RouteScrubIndex = RouteScrubIndex,
                PondingRainfallMm = Settings.PondingRainfallMm,
                PondingCurveNumber = Settings.PondingCurveNumber,
                GridWidth = Grid.Cols,
                GridHeight = Grid.Rows,
                CellSizeM = Grid.CellSizeM,
                MinLat = Grid.MinLat,
                MaxLat = Grid.MaxLat,
                MinLon = Grid.MinLon,
                MaxLon = Grid.MaxLon,
                PondDepthM = PondDepthFlat,
                OverlayRgba = OverlayRgba,
            };
            Entry.PayloadBlob = WaterLayerBlobCodec.Encode(Entry);
            Progress?.Report(WaterConstants.PROGRESS_COMPLETE_PERCENT);
            return Entry;
        }

        /// <summary>
        /// Builds RGBA overlay bytes from pond depth samples (deeper water is more opaque blue).
        /// </summary>
        /// <param name="PondDepthFlat">Row-major pond depths in metres.</param>
        /// <param name="Cols">Grid column count.</param>
        /// <param name="Rows">Grid row count.</param>
        /// <returns>RGBA byte buffer with four bytes per cell.</returns>
        public static byte[] BuildOverlayRgba
            (
            float[] PondDepthFlat,
            int Cols,
            int Rows
            )
        {
            int CellCount = Cols * Rows;
            byte[] Overlay = new byte[CellCount * RGBA_BYTES_PER_CELL];
            float MaxDepth = 0f;
            for (Int32 Index = 0; Index < CellCount; Index++)
            {
                float Depth = PondDepthFlat[Index];
                if ((!float.IsNaN(Depth)) && (Depth > MaxDepth))
                    MaxDepth = Depth;
            }

            for (Int32 Index = 0; Index < CellCount; Index++)
            {
                int Offset = Index * RGBA_BYTES_PER_CELL;
                float Depth = PondDepthFlat[Index];
                if ((float.IsNaN(Depth)) || (Depth <= 0f) || (MaxDepth <= 0f))
                {
                    Overlay[Offset + 0] = 0;
                    Overlay[Offset + 1] = 0;
                    Overlay[Offset + 2] = 0;
                    Overlay[Offset + 3] = 0;
                    continue;
                }

                double Normalized = Depth / MaxDepth;
                if (Normalized > 1.0)
                    Normalized = 1.0;

                double Scaled = Math.Pow(Normalized, WaterConstants.POND_DEPTH_GRAYSCALE_EXPONENT);
                byte Alpha = (byte)Math.Clamp(Scaled * WaterConstants.WATER_OVERLAY_MAX_ALPHA, 0, WaterConstants.WATER_OVERLAY_MAX_ALPHA);
                Overlay[Offset + 0] = WaterConstants.WATER_OVERLAY_RED;
                Overlay[Offset + 1] = WaterConstants.WATER_OVERLAY_GREEN;
                Overlay[Offset + 2] = WaterConstants.WATER_OVERLAY_BLUE;
                Overlay[Offset + 3] = Alpha;
            }

            return Overlay;
        }

        /// <summary>
        /// Runs the SCS runoff, D8 routing, and depression fill/spill ponding model.
        /// </summary>
        private static void RunPondingModel
            (
            float[,] Dem,
            int Rows,
            int Cols,
            double CellSizeM,
            float NoDataValue,
            uint RainfallMm,
            uint CurveNumber,
            IProgress<int>? Progress,
            out float[] PondDepthFlat,
            out byte[] OverlayRgba
            )
        {
            int Size = Rows * Cols;
            bool[] Valid = new bool[Size];
            double[] DemFlat = new double[Size];
            for (Int32 Row = 0; Row < Rows; Row++)
            {
                for (Int32 Col = 0; Col < Cols; Col++)
                {
                    int Index = (Row * Cols) + Col;
                    float Value = Dem[Row, Col];
                    Valid[Index] = (Value != NoDataValue) && (!float.IsNaN(Value)) && (!float.IsInfinity(Value));
                    DemFlat[Index] = Valid[Index] ? Value : 0.0;
                }
            }

            Progress?.Report(WaterConstants.PROGRESS_DEM_READY_PERCENT);
            double CellArea = CellSizeM * CellSizeM;
            double[] DepthInSink = ComputeDepthInSinkFromDem(DemFlat, Valid, Rows, Cols);
            D8Result D8 = ComputeD8Receiver(DemFlat, Valid, Rows, Cols, CellSizeM, CellSizeM);

            double Curve = Math.Clamp(CurveNumber, WaterConstants.MIN_SCS_CURVE_NUMBER, WaterConstants.MAX_SCS_CURVE_NUMBER);
            double RetentionMm = (WaterConstants.SCS_RETENTION_NUMERATOR_MM / Curve) - WaterConstants.SCS_RETENTION_DENOMINATOR_OFFSET_MM;
            double InitialAbstractionMm = WaterConstants.SCS_INITIAL_ABSTRACTION_FACTOR * RetentionMm;
            double Rainfall = RainfallMm;
            double RunoffMm = Rainfall <= InitialAbstractionMm
                ? 0.0
                : ((Rainfall - InitialAbstractionMm) * (Rainfall - InitialAbstractionMm)) / (Rainfall - InitialAbstractionMm + RetentionMm);
            double RunoffDepthM = RunoffMm / 1000.0;
            double[] RunoffVolumePerCell = new double[Size];
            for (Int32 Index = 0; Index < Size; Index++)
                RunoffVolumePerCell[Index] = Valid[Index] ? RunoffDepthM * CellArea : 0.0;

            RouteRunoffToTerminals(RunoffVolumePerCell, D8, out double[] Accumulated, out long[] TerminalOfCell);
            Progress?.Report(WaterConstants.PROGRESS_ROUTING_COMPLETE_PERCENT);
            int[] Labels = LabelSinkRegions(DepthInSink, Valid, Rows, Cols);

            int[] TerminalSinkId = new int[Size];
            for (Int32 Index = 0; Index < Size; Index++)
            {
                long Terminal = TerminalOfCell[Index];
                TerminalSinkId[Index] = (Terminal >= 0) && (Terminal < Size) ? Labels[Terminal] : 0;
            }

            Dictionary<int, double> IncomingVolume = new Dictionary<int, double>();
            for (Int32 Index = 0; Index < Size; Index++)
            {
                if (!Valid[Index])
                    continue;

                int SinkId = TerminalSinkId[Index];
                if (SinkId > 0)
                {
                    if (!IncomingVolume.ContainsKey(SinkId))
                        IncomingVolume[SinkId] = 0.0;

                    IncomingVolume[SinkId] += RunoffVolumePerCell[Index];
                }
            }

            Dictionary<int, SinkInfo> SinkInfos = BuildSinkInfos(
                DemFlat,
                DepthInSink,
                Labels,
                Valid,
                TerminalOfCell,
                Rows,
                Cols,
                CellArea);
            List<int> OrderedSinkIds = SinkInfos.Keys.OrderBy(SinkId => SinkInfos[SinkId].SpillElev).ToList();
            Dictionary<int, double> StoredVolume = new Dictionary<int, double>();
            Dictionary<int, double> OverflowVolume = new Dictionary<int, double>();
            foreach (int SinkId in SinkInfos.Keys)
            {
                StoredVolume[SinkId] = 0.0;
                OverflowVolume[SinkId] = 0.0;
            }

            foreach (int SinkId in OrderedSinkIds)
            {
                SinkInfo Info = SinkInfos[SinkId];
                double VolumeIn = (IncomingVolume.TryGetValue(SinkId, out double Incoming) ? Incoming : 0.0) + OverflowVolume[SinkId];
                double Capacity = Info.CapacityVolume;
                if (VolumeIn <= Capacity)
                {
                    StoredVolume[SinkId] = VolumeIn;
                }
                else
                {
                    StoredVolume[SinkId] = Capacity;
                    double Excess = VolumeIn - Capacity;
                    int? Downstream = Info.DownstreamSinkId;
                    if (Downstream.HasValue && SinkInfos.ContainsKey(Downstream.Value))
                        OverflowVolume[Downstream.Value] += Excess;
                }
            }

            PondDepthFlat = new float[Size];
            for (Int32 Index = 0; Index < Size; Index++)
                PondDepthFlat[Index] = float.NaN;

            foreach (KeyValuePair<int, SinkInfo> Pair in SinkInfos)
            {
                int SinkId = Pair.Key;
                SinkInfo Info = Pair.Value;
                double Volume = StoredVolume[SinkId];
                double[] CapacityDepth = new double[Info.CellsFlat.Length];
                for (Int32 CellIndex = 0; CellIndex < CapacityDepth.Length; CellIndex++)
                    CapacityDepth[CellIndex] = DepthInSink[Info.CellsFlat[CellIndex]];

                double[] Realized = VolumeToDepths(CapacityDepth, Volume, CellArea);
                for (Int32 CellIndex = 0; CellIndex < Info.CellsFlat.Length; CellIndex++)
                {
                    int Flat = Info.CellsFlat[CellIndex];
                    PondDepthFlat[Flat] = (float)Realized[CellIndex];
                }
            }

            Progress?.Report(WaterConstants.PROGRESS_OVERLAY_START_PERCENT);
            OverlayRgba = BuildOverlayRgba(PondDepthFlat, Cols, Rows);
        }

        /// <summary>
        /// Returns true when a row/column lies inside the grid bounds.
        /// </summary>
        private static bool InBounds
            (
            int Row,
            int Col,
            int Rows,
            int Cols
            )
        {
            return (Row >= 0) && (Row < Rows) && (Col >= 0) && (Col < Cols);
        }

        /// <summary>
        /// Computes depression depth below the priority-flood surface for each valid cell.
        /// </summary>
        private static double[] ComputeDepthInSinkFromDem
            (
            double[] Dem,
            bool[] Valid,
            int Rows,
            int Cols
            )
        {
            double[] Filled = PriorityFloodFill(Dem, Valid, Rows, Cols);
            double[] Depth = new double[Dem.Length];
            for (Int32 Index = 0; Index < Depth.Length; Index++)
                Depth[Index] = Valid[Index] ? Math.Max(Filled[Index] - Dem[Index], 0.0) : 0.0;

            return Depth;
        }

        /// <summary>
        /// Priority-flood fill used to identify sink capacity depths.
        /// </summary>
        private static double[] PriorityFloodFill
            (
            double[] Dem,
            bool[] Valid,
            int Rows,
            int Cols
            )
        {
            double[] Filled = (double[])Dem.Clone();
            bool[] Visited = new bool[Dem.Length];
            PriorityQueue<(int Row, int Col), double> Queue = new PriorityQueue<(int Row, int Col), double>();

            for (Int32 Row = 0; Row < Rows; Row++)
            {
                foreach (int Col in new[] { 0, Cols - 1 })
                {
                    int Index = (Row * Cols) + Col;
                    if (Valid[Index] && !Visited[Index])
                    {
                        Queue.Enqueue((Row, Col), Filled[Index]);
                        Visited[Index] = true;
                    }
                }
            }

            for (Int32 Col = 0; Col < Cols; Col++)
            {
                foreach (int Row in new[] { 0, Rows - 1 })
                {
                    int Index = (Row * Cols) + Col;
                    if (Valid[Index] && !Visited[Index])
                    {
                        Queue.Enqueue((Row, Col), Filled[Index]);
                        Visited[Index] = true;
                    }
                }
            }

            while (Queue.Count > 0)
            {
                (int Row, int Col) = Queue.Dequeue();
                double Elevation = Filled[(Row * Cols) + Col];
                foreach ((int Dr, int Dc) in Neigh8)
                {
                    int NeighbourRow = Row + Dr;
                    int NeighbourCol = Col + Dc;
                    if (!InBounds(NeighbourRow, NeighbourCol, Rows, Cols))
                        continue;

                    int Index = (NeighbourRow * Cols) + NeighbourCol;
                    if ((!Valid[Index]) || Visited[Index])
                        continue;

                    Visited[Index] = true;
                    double NeighbourElevation = Filled[Index];
                    if (NeighbourElevation < Elevation)
                    {
                        Filled[Index] = Elevation;
                        NeighbourElevation = Elevation;
                    }

                    Queue.Enqueue((NeighbourRow, NeighbourCol), NeighbourElevation);
                }
            }

            return Filled;
        }

        /// <summary>D8 flow receiver indices and topological order.</summary>
        private sealed class D8Result
        {
            /// <summary>Downslope receiver flat index per cell, or -1 at sinks.</summary>
            public long[] Receiver = Array.Empty<long>();

            /// <summary>Count of upstream donors per cell.</summary>
            public int[] DonorCount = Array.Empty<int>();

            /// <summary>Cells in upstream-to-downstream processing order.</summary>
            public List<long> Order = new List<long>();
        }

        /// <summary>
        /// Computes D8 steepest-descent receivers for each valid cell.
        /// </summary>
        private static D8Result ComputeD8Receiver
            (
            double[] Dem,
            bool[] Valid,
            int Rows,
            int Cols,
            double Dx,
            double Dy
            )
        {
            int Size = Rows * Cols;
            long[] Receiver = new long[Size];
            int[] DonorCount = new int[Size];
            for (Int32 Index = 0; Index < Size; Index++)
                Receiver[Index] = -1;

            double DiagonalDistance = Math.Sqrt((Dx * Dx) + (Dy * Dy));
            Dictionary<(int, int), double> Distance = new Dictionary<(int, int), double>
            {
                [(-1, -1)] = DiagonalDistance,
                [(-1, 0)] = Dy,
                [(-1, 1)] = DiagonalDistance,
                [(0, -1)] = Dx,
                [(0, 1)] = Dx,
                [(1, -1)] = DiagonalDistance,
                [(1, 0)] = Dy,
                [(1, 1)] = DiagonalDistance,
            };

            for (Int32 Row = 0; Row < Rows; Row++)
            {
                for (Int32 Col = 0; Col < Cols; Col++)
                {
                    int Current = (Row * Cols) + Col;
                    if (!Valid[Current])
                        continue;

                    double Elevation = Dem[Current];
                    double BestSlope = 0.0;
                    long BestIndex = -1;
                    foreach ((int Dr, int Dc) in Neigh8)
                    {
                        int NeighbourRow = Row + Dr;
                        int NeighbourCol = Col + Dc;
                        if ((!InBounds(NeighbourRow, NeighbourCol, Rows, Cols)) || (!Valid[(NeighbourRow * Cols) + NeighbourCol]))
                            continue;

                        long Index = (NeighbourRow * Cols) + NeighbourCol;
                        double Delta = Elevation - Dem[Index];
                        double Slope = Delta / Distance[(Dr, Dc)];
                        if (Slope > BestSlope)
                        {
                            BestSlope = Slope;
                            BestIndex = Index;
                        }
                    }

                    Receiver[Current] = BestIndex;
                    if (BestIndex >= 0)
                        DonorCount[BestIndex]++;
                }
            }

            int[] DonorWork = (int[])DonorCount.Clone();
            List<long> Order = new List<long>();
            Queue<long> Queue = new Queue<long>();
            for (long Cell = 0; Cell < Size; Cell++)
            {
                if (Valid[Cell] && (DonorWork[Cell] == 0))
                    Queue.Enqueue(Cell);
            }

            while (Queue.Count > 0)
            {
                long Cell = Queue.Dequeue();
                Order.Add(Cell);
                long Downstream = Receiver[Cell];
                if ((Downstream >= 0) && (--DonorWork[Downstream] == 0))
                    Queue.Enqueue(Downstream);
            }

            return new D8Result { Receiver = Receiver, DonorCount = DonorCount, Order = Order };
        }

        /// <summary>
        /// Routes per-cell runoff volumes to terminal sink cells along D8 receivers.
        /// </summary>
        private static void RouteRunoffToTerminals
            (
            double[] RunoffVolumePerCell,
            D8Result D8,
            out double[] Accumulated,
            out long[] TerminalOfCell
            )
        {
            int Size = RunoffVolumePerCell.Length;
            Accumulated = (double[])RunoffVolumePerCell.Clone();
            TerminalOfCell = new long[Size];
            for (Int32 Index = 0; Index < Size; Index++)
                TerminalOfCell[Index] = -1;

            foreach (long Cell in D8.Order)
            {
                long Downstream = D8.Receiver[Cell];
                if (Downstream >= 0)
                    Accumulated[Downstream] += Accumulated[Cell];
            }

            for (long Cell = 0; Cell < Size; Cell++)
            {
                if (TerminalOfCell[Cell] >= 0)
                    continue;

                long Current = Cell;
                List<long> Path = new List<long>();
                long Terminal = -1;
                while ((Current >= 0) && (TerminalOfCell[Current] < 0))
                {
                    Path.Add(Current);
                    long Next = D8.Receiver[Current];
                    if (Next < 0)
                    {
                        Terminal = Current;
                        break;
                    }

                    Current = Next;
                }

                if ((Terminal < 0) && (Current >= 0))
                    Terminal = TerminalOfCell[Current];

                foreach (long PathCell in Path)
                    TerminalOfCell[PathCell] = Terminal;
            }
        }

        /// <summary>
        /// Labels eight-connected sink regions where depth-in-sink is positive.
        /// </summary>
        private static int[] LabelSinkRegions
            (
            double[] DepthInSink,
            bool[] Valid,
            int Rows,
            int Cols
            )
        {
            int Size = Rows * Cols;
            int[] Labels = new int[Size];
            int NextLabel = 1;
            Stack<int> Stack = new Stack<int>();
            for (Int32 Start = 0; Start < Size; Start++)
            {
                if ((Labels[Start] != 0) || (!Valid[Start]) || (DepthInSink[Start] <= 0.0))
                    continue;

                int Label = NextLabel++;
                Labels[Start] = Label;
                Stack.Push(Start);
                while (Stack.Count > 0)
                {
                    int Index = Stack.Pop();
                    int Row = Index / Cols;
                    int Col = Index % Cols;
                    foreach ((int Dr, int Dc) in Neigh8)
                    {
                        int NeighbourRow = Row + Dr;
                        int NeighbourCol = Col + Dc;
                        if (!InBounds(NeighbourRow, NeighbourCol, Rows, Cols))
                            continue;

                        int NeighbourIndex = (NeighbourRow * Cols) + NeighbourCol;
                        if ((Labels[NeighbourIndex] != 0) || (!Valid[NeighbourIndex]) || (DepthInSink[NeighbourIndex] <= 0.0))
                            continue;

                        Labels[NeighbourIndex] = Label;
                        Stack.Push(NeighbourIndex);
                    }
                }
            }

            return Labels;
        }

        /// <summary>Metadata for one labelled sink region.</summary>
        private sealed class SinkInfo
        {
            /// <summary>Sink label identifier.</summary>
            public int SinkId;

            /// <summary>Flat indices of cells belonging to this sink.</summary>
            public int[] CellsFlat = Array.Empty<int>();

            /// <summary>Elevation at which this sink spills to neighbours.</summary>
            public double SpillElev;

            /// <summary>Maximum storable volume in cubic metres.</summary>
            public double CapacityVolume;

            /// <summary>Downstream sink label receiving overflow, if any.</summary>
            public int? DownstreamSinkId;
        }

        /// <summary>
        /// Builds sink region metadata used for fill-spill routing.
        /// </summary>
        private static Dictionary<int, SinkInfo> BuildSinkInfos
            (
            double[] Dem,
            double[] DepthInSink,
            int[] Labels,
            bool[] Valid,
            long[] TerminalOfCell,
            int Rows,
            int Cols,
            double CellArea
            )
        {
            Dictionary<int, SinkInfo> Infos = new Dictionary<int, SinkInfo>();
            List<int> UniqueIds = Labels.Where(Label => Label > 0).Distinct().OrderBy(Label => Label).ToList();
            foreach (int SinkId in UniqueIds)
            {
                List<int> CellsFlat = new List<int>();
                double CapacitySum = 0.0;
                for (Int32 Index = 0; Index < Labels.Length; Index++)
                {
                    if (Labels[Index] != SinkId)
                        continue;

                    CellsFlat.Add(Index);
                    CapacitySum += DepthInSink[Index] * CellArea;
                }

                double SpillElev = ComputeSinkSpillElevation(Dem, Labels, SinkId, Valid, Rows, Cols);
                Infos[SinkId] = new SinkInfo
                {
                    SinkId = SinkId,
                    CellsFlat = CellsFlat.ToArray(),
                    SpillElev = SpillElev,
                    CapacityVolume = CapacitySum,
                    DownstreamSinkId = null,
                };
            }

            foreach (int SinkId in UniqueIds)
            {
                double BestEscape = double.PositiveInfinity;
                int? BestPair = null;
                for (Int32 Row = 0; Row < Rows; Row++)
                {
                    for (Int32 Col = 0; Col < Cols; Col++)
                    {
                        int Index = (Row * Cols) + Col;
                        if (Labels[Index] != SinkId)
                            continue;

                        foreach ((int Dr, int Dc) in Neigh8)
                        {
                            int NeighbourRow = Row + Dr;
                            int NeighbourCol = Col + Dc;
                            if (!InBounds(NeighbourRow, NeighbourCol, Rows, Cols))
                            {
                                if (Dem[Index] < BestEscape)
                                {
                                    BestEscape = Dem[Index];
                                    BestPair = null;
                                }

                                continue;
                            }

                            int NeighbourIndex = (NeighbourRow * Cols) + NeighbourCol;
                            if (!Valid[NeighbourIndex])
                            {
                                if (Dem[Index] < BestEscape)
                                {
                                    BestEscape = Dem[Index];
                                    BestPair = null;
                                }

                                continue;
                            }

                            int Other = Labels[NeighbourIndex];
                            if (Other != SinkId)
                            {
                                double Escape = Math.Max(Dem[Index], Dem[NeighbourIndex]);
                                if (Escape < BestEscape)
                                {
                                    BestEscape = Escape;
                                    BestPair = Other > 0 ? Other : (int?)null;
                                }
                            }
                        }
                    }
                }

                Infos[SinkId].DownstreamSinkId = BestPair;
            }

            return Infos;
        }

        /// <summary>
        /// Computes the spill elevation for one sink region.
        /// </summary>
        private static double ComputeSinkSpillElevation
            (
            double[] Dem,
            int[] Labels,
            int SinkId,
            bool[] Valid,
            int Rows,
            int Cols
            )
        {
            double Spill = double.PositiveInfinity;
            for (Int32 Row = 0; Row < Rows; Row++)
            {
                for (Int32 Col = 0; Col < Cols; Col++)
                {
                    int Index = (Row * Cols) + Col;
                    if (Labels[Index] != SinkId)
                        continue;

                    foreach ((int Dr, int Dc) in Neigh8)
                    {
                        int NeighbourRow = Row + Dr;
                        int NeighbourCol = Col + Dc;
                        if (!InBounds(NeighbourRow, NeighbourCol, Rows, Cols))
                        {
                            Spill = Math.Min(Spill, Dem[Index]);
                            continue;
                        }

                        int NeighbourIndex = (NeighbourRow * Cols) + NeighbourCol;
                        if (!Valid[NeighbourIndex])
                        {
                            Spill = Math.Min(Spill, Dem[Index]);
                            continue;
                        }

                        if (Labels[NeighbourIndex] != SinkId)
                            Spill = Math.Min(Spill, Math.Max(Dem[Index], Dem[NeighbourIndex]));
                    }
                }
            }

            return double.IsInfinity(Spill) ? 0.0 : Spill;
        }

        /// <summary>
        /// Maps a target runoff volume to realized pond depths across sink cells.
        /// </summary>
        private static double[] VolumeToDepths
            (
            double[] DepthCapacity,
            double TargetVolume,
            double CellArea
            )
        {
            if (TargetVolume <= 0.0)
                return new double[DepthCapacity.Length];

            double TotalCapacity = DepthCapacity.Sum() * CellArea;
            if (TargetVolume >= TotalCapacity)
                return (double[])DepthCapacity.Clone();

            double Low = 0.0;
            double High = DepthCapacity.Max();
            for (Int32 Iteration = 0; Iteration < WaterConstants.VOLUME_TO_DEPTH_MAX_ITERATIONS; Iteration++)
            {
                double Mid = (Low + High) * BINARY_SEARCH_MIDPOINT_FACTOR;
                double Volume = DepthCapacity.Sum(Depth => Math.Max(Depth - Mid, 0.0)) * CellArea;
                if (Volume > TargetVolume)
                    Low = Mid;
                else
                    High = Mid;
            }

            double Level = High;
            return DepthCapacity.Select(Depth => Math.Max(Depth - Level, 0.0)).ToArray();
        }
    }
}

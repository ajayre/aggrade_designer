using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Infers a closed field boundary ring from observed survey or design-grid samples when 2PER is absent.
    /// </summary>
    public static class ObservedFieldBoundaryInferrer
    {
        /// <summary>Minimum observed samples required before inference is attempted.</summary>
        private const int MIN_OBSERVED_POINT_COUNT = 4;

        /// <summary>Morphological close passes applied to the observed-cell mask.</summary>
        private const int MORPHOLOGY_CLOSE_PASSES = 2;

        /// <summary>Padding around observed UTM extent when rasterizing for inference.</summary>
        private const double EXTENT_PAD_M = 2.0;

        /// <summary>Eight-connected neighbour count used by morphology helpers.</summary>
        private const int EIGHT_CONNECTED_NEIGHBOUR_COUNT = 8;

        /// <summary>
        /// Attempts to infer an ordered lat/lon boundary ring from survey and optional design-grid samples.
        /// </summary>
        /// <param name="Survey">Survey model with interior and optional boundary points.</param>
        /// <param name="Grid">Optional design grid whose valid bin centroids supplement observed samples.</param>
        /// <returns>Ordered boundary ring, or null when inference fails.</returns>
        public static List<(double Lat, double Lon)>? TryInferFromProject
            (
            SurveyModel? Survey,
            DesignGrid? Grid
            )
        {
            List<(double Lat, double Lon)> Points = CollectObservedLatLonPoints(Survey, Grid);
            return TryInferFromPoints(Points);
        }

        /// <summary>
        /// Attempts to infer an ordered lat/lon boundary ring from observed sample locations.
        /// </summary>
        /// <param name="Points">Observed sample locations in decimal degrees.</param>
        /// <returns>Ordered boundary ring, or null when inference fails.</returns>
        public static List<(double Lat, double Lon)>? TryInferFromPoints
            (
            IReadOnlyList<(double Lat, double Lon)> Points
            )
        {
            if (Points.Count < MIN_OBSERVED_POINT_COUNT)
                return null;

            List<UTM.UTMCoordinate> UtmPoints = new List<UTM.UTMCoordinate>(Points.Count);
            foreach ((double Lat, double Lon) in Points)
                UtmPoints.Add(UTM.FromLatLon(Lat, Lon));

            UTM.UTMCoordinate ZoneRef = UtmPoints[0];
            double MinE = double.MaxValue;
            double MaxE = double.MinValue;
            double MinN = double.MaxValue;
            double MaxN = double.MinValue;
            foreach (UTM.UTMCoordinate Point in UtmPoints)
            {
                MinE = Math.Min(MinE, Point.Easting);
                MaxE = Math.Max(MaxE, Point.Easting);
                MinN = Math.Min(MinN, Point.Northing);
                MaxN = Math.Max(MaxN, Point.Northing);
            }

            MinE -= EXTENT_PAD_M;
            MaxE += EXTENT_PAD_M;
            MinN -= EXTENT_PAD_M;
            MaxN += EXTENT_PAD_M;

            int GridWidth = Math.Max(1, (int)Math.Ceiling((MaxE - MinE) / FieldConstants.BIN_SIZE_M));
            int GridHeight = Math.Max(1, (int)Math.Ceiling((MaxN - MinN) / FieldConstants.BIN_SIZE_M));
            bool[] Observed = new bool[GridWidth * GridHeight];
            foreach (UTM.UTMCoordinate Point in UtmPoints)
            {
                int CellX = (int)Math.Floor((Point.Easting - MinE) / FieldConstants.BIN_SIZE_M);
                int CellY = (int)Math.Floor((Point.Northing - MinN) / FieldConstants.BIN_SIZE_M);
                if (CellX < 0 || CellY < 0 || CellX >= GridWidth || CellY >= GridHeight)
                    continue;

                Observed[(CellY * GridWidth) + CellX] = true;
            }

            bool AnyObserved = false;
            for (int Index = 0; Index < Observed.Length; Index++)
            {
                if (!Observed[Index])
                    continue;

                AnyObserved = true;
                break;
            }

            if (!AnyObserved)
                return null;

            // intent: close small gaps between observed bins without the row/col envelope fill that swells rectangular edges
            bool[] Closed = MorphErode(
                MorphDilate(Observed, GridWidth, GridHeight, MORPHOLOGY_CLOSE_PASSES),
                GridWidth,
                GridHeight,
                MORPHOLOGY_CLOSE_PASSES);
            bool[] Filled = FillHoles(Closed, GridWidth, GridHeight);
            bool[] Main = LargestComponent(Filled, GridWidth, GridHeight);
            for (int Index = 0; Index < Main.Length; Index++)
                Main[Index] = Main[Index] || Observed[Index];

            bool[,] Valid = ToMatrix(Main, GridWidth, GridHeight);
            bool[,] ExteriorInvalid = MarkExteriorInvalid(Valid, GridWidth, GridHeight);
            List<(double Lat, double Lon)> EdgePoints = CollectExteriorEdgeLatLonPoints(
                Valid,
                ExteriorInvalid,
                GridWidth,
                GridHeight,
                MinE,
                MinN,
                ZoneRef.Zone,
                ZoneRef.IsNorthernHemisphere);

            if (EdgePoints.Count < GeoConstants.MIN_POLYGON_VERTICES)
                return null;

            return BoundaryMask.OrderBoundaryLatLon(EdgePoints);
        }

        /// <summary>
        /// Collects observed sample locations from survey and design-grid sources.
        /// </summary>
        /// <param name="Survey">Survey model with topology points.</param>
        /// <param name="Grid">Optional design grid with valid bin centroids.</param>
        /// <returns>Deduplicated lat/lon sample locations.</returns>
        private static List<(double Lat, double Lon)> CollectObservedLatLonPoints
            (
            SurveyModel? Survey,
            DesignGrid? Grid
            )
        {
            HashSet<(long LatKey, long LonKey)> Seen = new HashSet<(long LatKey, long LonKey)>();
            List<(double Lat, double Lon)> Points = new List<(double Lat, double Lon)>();

            void AddPoint
                (
                double Lat,
                double Lon
                )
            {
                (long LatKey, long LonKey) Key = (Quantize(Lat), Quantize(Lon));
                if (!Seen.Add(Key))
                    return;

                Points.Add((Lat, Lon));
            }

            if (Survey != null)
            {
                foreach (TopologyPoint Point in Survey.InteriorPoints)
                    AddPoint(Point.Latitude, Point.Longitude);

                foreach (TopologyPoint Point in Survey.BoundaryPoints)
                    AddPoint(Point.Latitude, Point.Longitude);
            }

            if (Grid != null)
            {
                Grid.EnsureCentroidsFromUtm();
                foreach (GridBinData Bin in Grid.Bins)
                {
                    if (!Bin.Valid)
                        continue;

                    AddPoint(Bin.CentroidLat, Bin.CentroidLon);
                }
            }

            return Points;
        }

        /// <summary>
        /// Collects lat/lon vertices for valid cells that border exterior empty space.
        /// </summary>
        /// <param name="Valid">Valid-cell mask.</param>
        /// <param name="ExteriorInvalid">Exterior invalid mask.</param>
        /// <param name="GridWidth">Mask width in cells.</param>
        /// <param name="GridHeight">Mask height in cells.</param>
        /// <param name="MinE">Western grid origin easting in metres.</param>
        /// <param name="MinN">Southern grid origin northing in metres.</param>
        /// <param name="Zone">UTM zone used for lat/lon conversion.</param>
        /// <param name="IsNorthern">True when the UTM zone is in the northern hemisphere.</param>
        /// <returns>Boundary vertex candidates in geographic coordinates.</returns>
        private static List<(double Lat, double Lon)> CollectExteriorEdgeLatLonPoints
            (
            bool[,] Valid,
            bool[,] ExteriorInvalid,
            int GridWidth,
            int GridHeight,
            double MinE,
            double MinN,
            int Zone,
            bool IsNorthern
            )
        {
            List<(double Lat, double Lon)> EdgePoints = new List<(double Lat, double Lon)>();
            for (int CellY = 0; CellY < GridHeight; CellY++)
            {
                for (int CellX = 0; CellX < GridWidth; CellX++)
                {
                    if (!Valid[CellY, CellX])
                        continue;

                    if (!BordersExteriorInvalid(ExteriorInvalid, CellX - 1, CellY, GridWidth, GridHeight) &&
                        !BordersExteriorInvalid(ExteriorInvalid, CellX + 1, CellY, GridWidth, GridHeight) &&
                        !BordersExteriorInvalid(ExteriorInvalid, CellX, CellY - 1, GridWidth, GridHeight) &&
                        !BordersExteriorInvalid(ExteriorInvalid, CellX, CellY + 1, GridWidth, GridHeight))
                    {
                        continue;
                    }

                    double Easting = MinE + ((CellX + FieldConstants.BIN_CENTER_OFFSET) * FieldConstants.BIN_SIZE_M);
                    double Northing = MinN + ((CellY + FieldConstants.BIN_CENTER_OFFSET) * FieldConstants.BIN_SIZE_M);
                    UTM.ToLatLon(Zone, IsNorthern, Easting, Northing, out double Lat, out double Lon);
                    EdgePoints.Add((Lat, Lon));
                }
            }

            return EdgePoints;
        }

        /// <summary>
        /// Flood-fills invalid cells reachable from the grid border.
        /// </summary>
        /// <param name="Valid">Valid-cell mask.</param>
        /// <param name="GridWidth">Mask width in cells.</param>
        /// <param name="GridHeight">Mask height in cells.</param>
        /// <returns>True where invalid cells are exterior to the observed region.</returns>
        private static bool[,] MarkExteriorInvalid
            (
            bool[,] Valid,
            int GridWidth,
            int GridHeight
            )
        {
            bool[,] Exterior = new bool[GridHeight, GridWidth];
            Queue<(int X, int Y)> Queue = new Queue<(int X, int Y)>();
            for (int CellY = 0; CellY < GridHeight; CellY++)
            {
                for (int CellX = 0; CellX < GridWidth; CellX++)
                {
                    if (Valid[CellY, CellX] || Exterior[CellY, CellX])
                        continue;

                    Exterior[CellY, CellX] = true;
                    Queue.Enqueue((CellX, CellY));
                }
            }

            while (Queue.Count > 0)
            {
                (int CellX, int CellY) = Queue.Dequeue();
                TryEnqueueExteriorInvalid(Queue, Exterior, Valid, CellX - 1, CellY, GridWidth, GridHeight);
                TryEnqueueExteriorInvalid(Queue, Exterior, Valid, CellX + 1, CellY, GridWidth, GridHeight);
                TryEnqueueExteriorInvalid(Queue, Exterior, Valid, CellX, CellY - 1, GridWidth, GridHeight);
                TryEnqueueExteriorInvalid(Queue, Exterior, Valid, CellX, CellY + 1, GridWidth, GridHeight);
            }

            return Exterior;
        }

        /// <summary>
        /// Enqueues one neighbouring invalid cell when it is not already tagged exterior.
        /// </summary>
        /// <param name="Queue">Active flood-fill queue.</param>
        /// <param name="Exterior">Exterior invalid mask being built.</param>
        /// <param name="Valid">Valid-cell mask.</param>
        /// <param name="CellX">Neighbour column index.</param>
        /// <param name="CellY">Neighbour row index.</param>
        /// <param name="GridWidth">Mask width in cells.</param>
        /// <param name="GridHeight">Mask height in cells.</param>
        private static void TryEnqueueExteriorInvalid
            (
            Queue<(int X, int Y)> Queue,
            bool[,] Exterior,
            bool[,] Valid,
            int CellX,
            int CellY,
            int GridWidth,
            int GridHeight
            )
        {
            if (CellX < 0 || CellY < 0 || CellX >= GridWidth || CellY >= GridHeight)
                return;

            if (Valid[CellY, CellX] || Exterior[CellY, CellX])
                return;

            Exterior[CellY, CellX] = true;
            Queue.Enqueue((CellX, CellY));
        }

        /// <summary>
        /// Returns true when a neighbour lies in exterior invalid space.
        /// </summary>
        /// <param name="ExteriorInvalid">Exterior invalid mask.</param>
        /// <param name="CellX">Neighbour column index.</param>
        /// <param name="CellY">Neighbour row index.</param>
        /// <param name="GridWidth">Mask width in cells.</param>
        /// <param name="GridHeight">Mask height in cells.</param>
        /// <returns>True when the neighbour is exterior invalid.</returns>
        private static bool BordersExteriorInvalid
            (
            bool[,] ExteriorInvalid,
            int CellX,
            int CellY,
            int GridWidth,
            int GridHeight
            )
        {
            if (CellX < 0 || CellY < 0 || CellX >= GridWidth || CellY >= GridHeight)
                return true;

            return ExteriorInvalid[CellY, CellX];
        }

        /// <summary>
        /// Converts a flat mask to a row-major boolean matrix.
        /// </summary>
        /// <param name="Mask">Flat mask indexed by row then column.</param>
        /// <param name="GridWidth">Mask width in cells.</param>
        /// <param name="GridHeight">Mask height in cells.</param>
        /// <returns>Two-dimensional mask.</returns>
        private static bool[,] ToMatrix
            (
            bool[] Mask,
            int GridWidth,
            int GridHeight
            )
        {
            bool[,] Matrix = new bool[GridHeight, GridWidth];
            for (int CellY = 0; CellY < GridHeight; CellY++)
            {
                for (int CellX = 0; CellX < GridWidth; CellX++)
                    Matrix[CellY, CellX] = Mask[(CellY * GridWidth) + CellX];
            }

            return Matrix;
        }

        /// <summary>
        /// Expands true cells in a boolean mask using eight-connected dilation.
        /// </summary>
        /// <param name="Mask">Source validity mask.</param>
        /// <param name="GridWidth">Mask width in cells.</param>
        /// <param name="GridHeight">Mask height in cells.</param>
        /// <param name="Passes">Number of dilation iterations to apply.</param>
        /// <returns>Dilated mask copy.</returns>
        private static bool[] MorphDilate
            (
            bool[] Mask,
            int GridWidth,
            int GridHeight,
            int Passes
            )
        {
            bool[] Out = (bool[])Mask.Clone();
            for (int Pass = 0; Pass < Passes; Pass++)
            {
                bool[] Next = new bool[Out.Length];
                for (int CellY = 0; CellY < GridHeight; CellY++)
                {
                    for (int CellX = 0; CellX < GridWidth; CellX++)
                    {
                        bool Any = false;
                        for (int DeltaY = -1; DeltaY <= 1 && !Any; DeltaY++)
                        {
                            int NeighbourY = CellY + DeltaY;
                            if (NeighbourY < 0 || NeighbourY >= GridHeight)
                                continue;

                            for (int DeltaX = -1; DeltaX <= 1; DeltaX++)
                            {
                                int NeighbourX = CellX + DeltaX;
                                if (NeighbourX < 0 || NeighbourX >= GridWidth)
                                    continue;

                                if (Out[(NeighbourY * GridWidth) + NeighbourX])
                                {
                                    Any = true;
                                    break;
                                }
                            }
                        }

                        Next[(CellY * GridWidth) + CellX] = Any;
                    }
                }

                Out = Next;
            }

            return Out;
        }

        /// <summary>
        /// Shrinks true cells in a boolean mask using eight-connected erosion.
        /// </summary>
        /// <param name="Mask">Source validity mask.</param>
        /// <param name="GridWidth">Mask width in cells.</param>
        /// <param name="GridHeight">Mask height in cells.</param>
        /// <param name="Passes">Number of erosion iterations to apply.</param>
        /// <returns>Eroded mask copy.</returns>
        private static bool[] MorphErode
            (
            bool[] Mask,
            int GridWidth,
            int GridHeight,
            int Passes
            )
        {
            bool[] Out = (bool[])Mask.Clone();
            for (int Pass = 0; Pass < Passes; Pass++)
            {
                bool[] Next = new bool[Out.Length];
                for (int CellY = 0; CellY < GridHeight; CellY++)
                {
                    for (int CellX = 0; CellX < GridWidth; CellX++)
                    {
                        bool All = true;
                        for (int DeltaY = -1; DeltaY <= 1 && All; DeltaY++)
                        {
                            int NeighbourY = CellY + DeltaY;
                            if (NeighbourY < 0 || NeighbourY >= GridHeight)
                            {
                                All = false;
                                break;
                            }

                            for (int DeltaX = -1; DeltaX <= 1; DeltaX++)
                            {
                                int NeighbourX = CellX + DeltaX;
                                if (NeighbourX < 0 || NeighbourX >= GridWidth ||
                                    !Out[(NeighbourY * GridWidth) + NeighbourX])
                                {
                                    All = false;
                                    break;
                                }
                            }
                        }

                        Next[(CellY * GridWidth) + CellX] = All;
                    }
                }

                Out = Next;
            }

            return Out;
        }

        /// <summary>
        /// Fills interior holes in a closed mask.
        /// </summary>
        /// <param name="Mask">Source mask.</param>
        /// <param name="GridWidth">Mask width in cells.</param>
        /// <param name="GridHeight">Mask height in cells.</param>
        /// <returns>Mask with interior holes filled.</returns>
        private static bool[] FillHoles
            (
            bool[] Mask,
            int GridWidth,
            int GridHeight
            )
        {
            bool[] Exterior = new bool[Mask.Length];
            Queue<int> Queue = new Queue<int>();
            int[] DeltaX = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] DeltaY = { -1, -1, -1, 0, 0, 1, 1, 1 };

            for (int CellY = 0; CellY < GridHeight; CellY++)
            {
                for (int CellX = 0; CellX < GridWidth; CellX++)
                {
                    bool IsBorder = CellX == 0 || CellY == 0 || CellX == GridWidth - 1 || CellY == GridHeight - 1;
                    int Index = (CellY * GridWidth) + CellX;
                    if (!IsBorder || Mask[Index] || Exterior[Index])
                        continue;

                    Exterior[Index] = true;
                    Queue.Enqueue(Index);
                }
            }

            while (Queue.Count > 0)
            {
                int Index = Queue.Dequeue();
                int CellY = Index / GridWidth;
                int CellX = Index % GridWidth;
                for (int Direction = 0; Direction < EIGHT_CONNECTED_NEIGHBOUR_COUNT; Direction++)
                {
                    int NeighbourX = CellX + DeltaX[Direction];
                    int NeighbourY = CellY + DeltaY[Direction];
                    if (NeighbourX < 0 || NeighbourY < 0 || NeighbourX >= GridWidth || NeighbourY >= GridHeight)
                        continue;

                    int NeighbourIndex = (NeighbourY * GridWidth) + NeighbourX;
                    if (Mask[NeighbourIndex] || Exterior[NeighbourIndex])
                        continue;

                    Exterior[NeighbourIndex] = true;
                    Queue.Enqueue(NeighbourIndex);
                }
            }

            bool[] Filled = (bool[])Mask.Clone();
            for (int Index = 0; Index < Filled.Length; Index++)
            {
                if (!Filled[Index] && !Exterior[Index])
                    Filled[Index] = true;
            }

            return Filled;
        }

        /// <summary>
        /// Keeps only the largest connected component in a mask.
        /// </summary>
        /// <param name="Mask">Source mask.</param>
        /// <param name="GridWidth">Mask width in cells.</param>
        /// <param name="GridHeight">Mask height in cells.</param>
        /// <returns>Mask containing only the largest connected component.</returns>
        private static bool[] LargestComponent
            (
            bool[] Mask,
            int GridWidth,
            int GridHeight
            )
        {
            bool[] Best = Array.Empty<bool>();
            int BestCount = 0;
            bool[] Visited = new bool[Mask.Length];
            int[] DeltaX = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] DeltaY = { -1, -1, -1, 0, 0, 1, 1, 1 };

            for (int Start = 0; Start < Mask.Length; Start++)
            {
                if (!Mask[Start] || Visited[Start])
                    continue;

                bool[] Component = new bool[Mask.Length];
                Queue<int> Queue = new Queue<int>();
                Queue.Enqueue(Start);
                Visited[Start] = true;
                Component[Start] = true;
                int Count = 1;

                while (Queue.Count > 0)
                {
                    int Index = Queue.Dequeue();
                    int CellY = Index / GridWidth;
                    int CellX = Index % GridWidth;
                    for (int Direction = 0; Direction < EIGHT_CONNECTED_NEIGHBOUR_COUNT; Direction++)
                    {
                        int NeighbourX = CellX + DeltaX[Direction];
                        int NeighbourY = CellY + DeltaY[Direction];
                        if (NeighbourX < 0 || NeighbourY < 0 || NeighbourX >= GridWidth || NeighbourY >= GridHeight)
                            continue;

                        int NeighbourIndex = (NeighbourY * GridWidth) + NeighbourX;
                        if (!Mask[NeighbourIndex] || Visited[NeighbourIndex])
                            continue;

                        Visited[NeighbourIndex] = true;
                        Component[NeighbourIndex] = true;
                        Count++;
                        Queue.Enqueue(NeighbourIndex);
                    }
                }

                if (Count <= BestCount)
                    continue;

                BestCount = Count;
                Best = Component;
            }

            return Best.Length == 0 ? (bool[])Mask.Clone() : Best;
        }

        /// <summary>
        /// Quantizes a coordinate to a fixed-point deduplication key.
        /// </summary>
        /// <param name="Value">Coordinate in decimal degrees.</param>
        /// <returns>Fixed-point key.</returns>
        private static long Quantize
            (
            double Value
            )
        {
            return (long)Math.Round(Value * 100_000_000.0);
        }
    }
}

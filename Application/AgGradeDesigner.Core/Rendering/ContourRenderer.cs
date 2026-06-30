using AgGradeDesigner.Core.Models;



namespace AgGradeDesigner.Core.Rendering

{

    /// <summary>

    /// Evenly spaced contour extraction (AgGrade cold-open marching squares).

    /// </summary>

    static public class ContourRenderer

    {

        private const int RESOLUTION_MULTIPLIER = 4;

        private const double MERGE_EPSILON_METRES = 2.0;

        private const int CONTOUR_COUNT = 32;

        private const int BAND_COUNT = CONTOUR_COUNT + 1;



        static private readonly (int A, int B)[][] SegmentTable =

        {

            Array.Empty<(int, int)>(),

            new[] { (3, 0) },

            new[] { (0, 1) },

            new[] { (3, 1) },

            new[] { (1, 2) },

            new[] { (3, 0), (1, 2) },

            new[] { (0, 2) },

            new[] { (3, 2) },

            new[] { (2, 3) },

            new[] { (0, 3) },

            new[] { (0, 1), (2, 3) },

            new[] { (1, 3) },

            new[] { (1, 3) },

            new[] { (0, 1) },

            new[] { (0, 2) },

            Array.Empty<(int, int)>()

        };



        /// <summary>

        /// Extracts smoothed contour polylines grouped by elevation level.

        /// </summary>

        /// <param name="Surface">Sampled scalar field.</param>

        /// <param name="ContentView">Scalar channel to contour.</param>

        /// <param name="VMin">Minimum value of the display range.</param>

        /// <param name="VMax">Maximum value of the display range.</param>

        /// <returns>Level and polyline pairs for each contour elevation.</returns>

        static public IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> ExtractPolylinesByLevel

            (

            FieldSurfaceSampler Surface,

            FieldContentView ContentView,

            double VMin,

            double VMax

            )

        {

            double[] Levels = GetContourLevels(VMin, VMax);

            if (Levels.Length == 0)

                return Array.Empty<(double, IReadOnlyList<(double Lat, double Lon)>)>();



            // Supersample the scalar field before marching squares for smoother contours.

            int FineWidth = (Surface.SampleWidth - 1) * RESOLUTION_MULTIPLIER + 1;

            int FineHeight = (Surface.SampleHeight - 1) * RESOLUTION_MULTIPLIER + 1;

            double[,] HeightGrid = new double[FineHeight, FineWidth];

            bool[,] ValidGrid = new bool[FineHeight, FineWidth];



            for (int Y = 0; Y < FineHeight; Y++)

            {

                for (int X = 0; X < FineWidth; X++)

                {

                    double Lon = Surface.MinLon + (Surface.MaxLon - Surface.MinLon) * X / (FineWidth - 1);

                    double Lat = Surface.MaxLat - (Surface.MaxLat - Surface.MinLat) * Y / (FineHeight - 1);

                    double Value = Surface.SampleScalar(ContentView, Lat, Lon);

                    ValidGrid[Y, X] = !double.IsNaN(Value);

                    HeightGrid[Y, X] = Value;

                }

            }



            double CentreLat = (Surface.MinLat + Surface.MaxLat) * 0.5;

            List<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Result = new List<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)>();



            foreach (double Level in Levels)

            {

                List<(double LatA, double LonA, double LatB, double LonB)> Segments = new List<(double LatA, double LonA, double LatB, double LonB)>();

                for (int Y = 0; Y < FineHeight - 1; Y++)

                {

                    for (int X = 0; X < FineWidth - 1; X++)

                    {

                        CollectCellSegments(HeightGrid, ValidGrid, FineWidth, FineHeight, Surface, X, Y, Level, Segments);

                    }

                }



                foreach (IReadOnlyList<(double Lat, double Lon)> Polyline in TracePolylines(Segments, CentreLat))

                {

                    if (Polyline.Count >= 2)

                        Result.Add((Level, ContourSmoother.Smooth(Polyline)));

                }

            }



            // Join open fragments whose endpoints land within merge tolerance (fixes dashed contours).

            return MergeNearbyPolylinesAtLevels(Result, CentreLat);

        }



        /// <summary>

        /// Extracts all contour polylines flattened across elevation levels.

        /// </summary>

        /// <param name="Surface">Sampled scalar field.</param>

        /// <param name="ContentView">Scalar channel to contour.</param>

        /// <param name="VMin">Minimum value of the display range.</param>

        /// <param name="VMax">Maximum value of the display range.</param>

        /// <returns>Flat list of contour polylines.</returns>

        static public IReadOnlyList<IReadOnlyList<(double Lat, double Lon)>> ExtractPolylines

            (

            FieldSurfaceSampler Surface,

            FieldContentView ContentView,

            double VMin,

            double VMax

            )

        {

            List<IReadOnlyList<(double Lat, double Lon)>> Flat = new List<IReadOnlyList<(double Lat, double Lon)>>();

            foreach ((double _, IReadOnlyList<(double Lat, double Lon)> Points) in ExtractPolylinesByLevel(Surface, ContentView, VMin, VMax))

                Flat.Add(Points);

            return Flat;

        }



        /// <summary>

        /// Computes evenly spaced contour levels between display range endpoints.

        /// </summary>

        /// <param name="VMin">Minimum value of the display range.</param>

        /// <param name="VMax">Maximum value of the display range.</param>

        /// <returns>Contour elevations, or empty when the range is degenerate.</returns>

        static public double[] GetContourLevels

            (

            double VMin,

            double VMax

            )

        {

            double Span = VMax - VMin;

            if (Span <= 0)

                return Array.Empty<double>();



            double[] Levels = new double[CONTOUR_COUNT];

            for (int Band = 1; Band <= CONTOUR_COUNT; Band++)

                Levels[Band - 1] = VMin + Span * Band / BAND_COUNT;

            return Levels;

        }



        /// <summary>

        /// Emits line segments crossing one grid cell at a contour level via marching squares.

        /// </summary>

        /// <param name="Heights">Supersampled scalar height grid.</param>

        /// <param name="Valid">Validity mask parallel to the height grid.</param>

        /// <param name="Width">Grid width in cells.</param>

        /// <param name="Height">Grid height in cells.</param>

        /// <param name="Surface">Surface sampler for lat/lon corner mapping.</param>

        /// <param name="X">Cell column index.</param>

        /// <param name="Y">Cell row index.</param>

        /// <param name="Level">Contour elevation to extract.</param>

        /// <param name="Segments">Output segment list to append to.</param>

        static private void CollectCellSegments

            (

            double[,] Heights,

            bool[,] Valid,

            int Width,

            int Height,

            FieldSurfaceSampler Surface,

            int X,

            int Y,

            double Level,

            List<(double LatA, double LonA, double LatB, double LonB)> Segments

            )

        {

            if (!Valid[Y, X] || !Valid[Y, X + 1] || !Valid[Y + 1, X + 1] || !Valid[Y + 1, X])

                return;



            // Classify the cell corner heights against the contour level for marching squares.
            double VBl = Heights[Y + 1, X];

            double VBr = Heights[Y + 1, X + 1];

            double VTr = Heights[Y, X + 1];

            double VTl = Heights[Y, X];

            double[] Values = { VBl, VBr, VTr, VTl };



            int CaseIndex = 0;

            for (int I = 0; I < 4; I++)

            {

                if (Values[I] >= Level)

                    CaseIndex |= 1 << I;

            }



            if (CaseIndex is 0 or 15)

                return;



            (double Lat, double Lon) Bl = CellLatLon(Surface, Width, Height, X, Y + 1);

            (double Lat, double Lon) Br = CellLatLon(Surface, Width, Height, X + 1, Y + 1);

            (double Lat, double Lon) Tr = CellLatLon(Surface, Width, Height, X + 1, Y);

            (double Lat, double Lon) Tl = CellLatLon(Surface, Width, Height, X, Y);

            (double Lat, double Lon)[] Corners = { Bl, Br, Tr, Tl };



            (double Lat, double Lon)[] EdgePoints =

            {

                InterpolateEdge(Corners[0], Corners[1], Values[0], Values[1], Level),

                InterpolateEdge(Corners[1], Corners[2], Values[1], Values[2], Level),

                InterpolateEdge(Corners[3], Corners[2], Values[3], Values[2], Level),

                InterpolateEdge(Corners[0], Corners[3], Values[0], Values[3], Level)

            };



            (int A, int B)[] Edges;

            if (CaseIndex is 5 or 10)

            {

                // Resolve saddle ambiguity using the centre decider value.

                double Decider = (Values[0] - Level) * (Values[2] - Level) - (Values[1] - Level) * (Values[3] - Level);

                if (CaseIndex == 5)

                    Edges = Decider < 0 ? SegmentTable[5] : SegmentTable[10];

                else

                    Edges = Decider < 0 ? SegmentTable[10] : SegmentTable[5];

            }

            else

            {

                Edges = SegmentTable[CaseIndex];

            }



            foreach ((int A, int B) in Edges)

            {

                (double LatA, double LonA) = EdgePoints[A];

                (double LatB, double LonB) = EdgePoints[B];

                Segments.Add((LatA, LonA, LatB, LonB));

            }

        }



        /// <summary>

        /// Maps a fine-grid cell corner to geographic coordinates.

        /// </summary>

        /// <param name="Surface">Surface sampler defining the geographic extent.</param>

        /// <param name="Width">Fine grid width.</param>

        /// <param name="Height">Fine grid height.</param>

        /// <param name="X">Corner column index.</param>

        /// <param name="Y">Corner row index.</param>

        /// <returns>Latitude and longitude of the corner.</returns>

        static private (double Lat, double Lon) CellLatLon

            (

            FieldSurfaceSampler Surface,

            int Width,

            int Height,

            int X,

            int Y

            )

        {
            // Map fine-grid indices back to geographic coordinates along the surface extent.
            double Lon = Surface.MinLon + (Surface.MaxLon - Surface.MinLon) * X / (Width - 1);

            double Lat = Surface.MaxLat - (Surface.MaxLat - Surface.MinLat) * Y / (Height - 1);

            return (Lat, Lon);

        }



        /// <summary>

        /// Linearly interpolates a contour crossing along an edge between two corners.

        /// </summary>

        /// <param name="A">First corner location.</param>

        /// <param name="B">Second corner location.</param>

        /// <param name="Va">Scalar value at corner A.</param>

        /// <param name="Vb">Scalar value at corner B.</param>

        /// <param name="Level">Contour elevation.</param>

        /// <returns>Geographic location of the edge crossing.</returns>

        static private (double Lat, double Lon) InterpolateEdge

            (

            (double Lat, double Lon) A,

            (double Lat, double Lon) B,

            double Va,

            double Vb,

            double Level

            )

        {
            if (Math.Abs(Vb - Va) < 1e-12)
                // Degenerate edge: average corner positions when heights are equal.
                return ((A.Lat + B.Lat) * 0.5, (A.Lon + B.Lon) * 0.5);



            double T = Math.Clamp((Level - Va) / (Vb - Va), 0.0, 1.0);

            return (A.Lat + (B.Lat - A.Lat) * T, A.Lon + (B.Lon - A.Lon) * T);

        }



        /// <summary>

        /// Chains segment endpoints into polylines with metre-scale vertex merging.

        /// </summary>

        /// <param name="Segments">Raw contour segments in geographic coordinates.</param>

        /// <param name="CentreLat">Map centre latitude for distance scaling.</param>

        /// <returns>Traced polylines ready for smoothing.</returns>

        static private List<IReadOnlyList<(double Lat, double Lon)>> TracePolylines

            (

            List<(double LatA, double LonA, double LatB, double LonB)> Segments,

            double CentreLat

            )

        {

            if (Segments.Count == 0)

                return new List<IReadOnlyList<(double Lat, double Lon)>>();



            List<(double Lat, double Lon)> Points = new List<(double Lat, double Lon)>();



            /// <summary>
            /// Returns the index of an existing vertex within merge tolerance, or appends a new one.
            /// </summary>
            /// <param name="Lat">Vertex latitude in decimal degrees.</param>
            /// <param name="Lon">Vertex longitude in decimal degrees.</param>
            /// <returns>Index of the merged or newly appended vertex.</returns>
            int Register
                (
                double Lat,
                double Lon
                )
            {
                // Reuse a prior endpoint when segment joins land within merge epsilon.
                for (int I = 0; I < Points.Count; I++)

                {

                    if (DistanceMetres(Points[I].Lat, Points[I].Lon, Lat, Lon, CentreLat) <= MERGE_EPSILON_METRES)

                        return I;

                }



                Points.Add((Lat, Lon));

                return Points.Count - 1;

            }



            Dictionary<int, List<int>> Adjacency = new Dictionary<int, List<int>>();

            HashSet<(int, int)> Edges = new HashSet<(int, int)>();



            foreach ((double LatA, double LonA, double LatB, double LonB) Segment in Segments)

            {

                int Start = Register(Segment.LatA, Segment.LonA);

                int End = Register(Segment.LatB, Segment.LonB);

                if (Start == End)

                    continue;



                AddEdge(Adjacency, Start, End);

                Edges.Add(Start < End ? (Start, End) : (End, Start));

            }



            HashSet<(int, int)> VisitedEdges = new HashSet<(int, int)>();

            List<IReadOnlyList<(double Lat, double Lon)>> Polylines = new List<IReadOnlyList<(double Lat, double Lon)>>();



            foreach ((int A, int B) in Edges)

            {

                if (VisitedEdges.Contains(A < B ? (A, B) : (B, A)))

                    continue;



                List<int> Chain = WalkChain(A, B, Adjacency, VisitedEdges);

                if (Chain.Count >= 2)

                {

                    List<(double Lat, double Lon)> Polyline = new List<(double Lat, double Lon)>(Chain.Count);

                    foreach (int Index in Chain)

                        Polyline.Add(Points[Index]);

                    Polylines.Add(Polyline);

                }

            }



            return Polylines;

        }



        /// <summary>

        /// Computes approximate ground distance between two lat/lon points.

        /// </summary>

        /// <param name="LatA">First latitude in decimal degrees.</param>

        /// <param name="LonA">First longitude in decimal degrees.</param>

        /// <param name="LatB">Second latitude in decimal degrees.</param>

        /// <param name="LonB">Second longitude in decimal degrees.</param>

        /// <param name="CentreLat">Reference latitude for longitude scale correction.</param>

        /// <returns>Approximate distance in metres.</returns>

        static private double DistanceMetres

            (

            double LatA,

            double LonA,

            double LatB,

            double LonB,

            double CentreLat

            )

        {
            // Approximate local metres per degree for fast vertex merging near the map centre.
            double MetresPerDegreeLat = 111320.0;

            double MetresPerDegreeLon = MetresPerDegreeLat * Math.Cos(CentreLat * Math.PI / 180.0);

            double Dy = (LatA - LatB) * MetresPerDegreeLat;

            double Dx = (LonA - LonB) * MetresPerDegreeLon;

            return Math.Sqrt(Dx * Dx + Dy * Dy);

        }



        /// <summary>

        /// Adds an undirected edge to the segment adjacency graph.

        /// </summary>

        /// <param name="Adjacency">Vertex adjacency lists.</param>

        /// <param name="A">First vertex index.</param>

        /// <param name="B">Second vertex index.</param>

        static private void AddEdge

            (

            Dictionary<int, List<int>> Adjacency,

            int A,

            int B

            )

        {
            // Maintain undirected adjacency lists for polyline tracing.
            if (!Adjacency.TryGetValue(A, out List<int>? ListA))

            {

                ListA = new List<int>();

                Adjacency[A] = ListA;

            }



            if (!Adjacency.TryGetValue(B, out List<int>? ListB))

            {

                ListB = new List<int>();

                Adjacency[B] = ListB;

            }



            ListA.Add(B);

            ListB.Add(A);

        }



        /// <summary>

        /// Walks a degree-two chain from a seed edge to form one polyline.

        /// </summary>

        /// <param name="Start">Seed chain start vertex.</param>

        /// <param name="Next">Seed chain next vertex.</param>

        /// <param name="Adjacency">Segment adjacency graph.</param>

        /// <param name="VisitedEdges">Edges already consumed by prior chains.</param>

        /// <returns>Ordered vertex indices along the chain.</returns>

        static private List<int> WalkChain

            (

            int Start,

            int Next,

            Dictionary<int, List<int>> Adjacency,

            HashSet<(int, int)> VisitedEdges

            )

        {

            List<int> Chain = new List<int> { Start, Next };

            MarkVisited(VisitedEdges, Start, Next);



            // Extend forward from the seed edge while the graph remains a simple path.

            while (true)

            {

                int Tip = Chain[^1];

                int Prev = Chain[^2];

                List<int>? Neighbours = Adjacency.GetValueOrDefault(Tip);

                if (Neighbours == null)

                    break;



                List<int> Candidates = Neighbours.Where(N => N != Prev).ToList();

                if (Candidates.Count != 1)

                    break;



                int Candidate = Candidates[0];

                if (Candidate == Chain[0])

                {

                    MarkVisited(VisitedEdges, Tip, Candidate);

                    break;

                }



                if (IsVisited(VisitedEdges, Tip, Candidate))

                    break;



                MarkVisited(VisitedEdges, Tip, Candidate);

                Chain.Add(Candidate);

            }



            // Extend backward when the chain is open at the start.

            if (Chain[0] != Chain[^1])

            {

                while (true)

                {

                    int Tip = Chain[0];

                    int Prev = Chain[1];

                    List<int>? Neighbours = Adjacency.GetValueOrDefault(Tip);

                    if (Neighbours == null)

                        break;



                    List<int> Candidates = Neighbours.Where(N => N != Prev).ToList();

                    if (Candidates.Count != 1)

                        break;



                    int Candidate = Candidates[0];

                    if (Candidate == Chain[^1])

                    {

                        MarkVisited(VisitedEdges, Tip, Candidate);

                        break;

                    }



                    if (IsVisited(VisitedEdges, Tip, Candidate))

                        break;



                    MarkVisited(VisitedEdges, Tip, Candidate);

                    Chain.Insert(0, Candidate);

                }

            }



            return Chain;

        }



        /// <summary>

        /// Records an undirected edge as visited in canonical order.

        /// </summary>

        /// <param name="Visited">Visited edge set.</param>

        /// <param name="A">First vertex index.</param>

        /// <param name="B">Second vertex index.</param>

        static private void MarkVisited

            (

            HashSet<(int, int)> Visited,

            int A,

            int B

            )

        {
            // Store edges in canonical (low, high) order for set lookups.
            Visited.Add(A < B ? (A, B) : (B, A));

        }



        /// <summary>

        /// Returns whether an undirected edge was already visited.

        /// </summary>

        /// <param name="Visited">Visited edge set.</param>

        /// <param name="A">First vertex index.</param>

        /// <param name="B">Second vertex index.</param>

        /// <returns>True when the edge is in the visited set.</returns>

        static private bool IsVisited

            (

            HashSet<(int, int)> Visited,

            int A,

            int B

            )

        {
            return Visited.Contains(A < B ? (A, B) : (B, A));

        }



        /// <summary>

        /// Merges contour polylines at the same level when open ends meet within merge tolerance.

        /// </summary>

        /// <param name="PolylinesByLevel">Extracted level/point pairs.</param>

        /// <param name="CentreLat">Map centre latitude for distance scaling.</param>

        /// <returns>Polylines with nearby fragments joined into longer chains.</returns>

        static private List<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> MergeNearbyPolylinesAtLevels

            (

            List<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> PolylinesByLevel,

            double CentreLat

            )

        {

            Dictionary<double, List<List<(double Lat, double Lon)>>> ByLevel = new Dictionary<double, List<List<(double Lat, double Lon)>>>();

            foreach ((double Level, IReadOnlyList<(double Lat, double Lon)> Points) Entry in PolylinesByLevel)

            {

                if (!ByLevel.TryGetValue(Entry.Level, out List<List<(double Lat, double Lon)>>? Chains))

                {

                    Chains = new List<List<(double Lat, double Lon)>>();

                    ByLevel[Entry.Level] = Chains;

                }



                Chains.Add(new List<(double Lat, double Lon)>(Entry.Points));

            }



            List<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Merged = new List<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)>();

            foreach (KeyValuePair<double, List<List<(double Lat, double Lon)>>> Group in ByLevel)

            {

                List<List<(double Lat, double Lon)>> Chains = Group.Value;

                bool Joined = true;

                while (Joined)

                {

                    Joined = false;

                    for (int A = 0; A < Chains.Count; A++)

                    {

                        for (int B = A + 1; B < Chains.Count; B++)

                        {

                            if (TryJoinPolylines(Chains[A], Chains[B], CentreLat, out List<(double Lat, double Lon)>? Combined))

                            {

                                Chains[A] = Combined;

                                Chains.RemoveAt(B);

                                Joined = true;

                                break;

                            }

                        }



                        if (Joined)

                            break;

                    }

                }



                foreach (List<(double Lat, double Lon)> Chain in Chains)

                    Merged.Add((Group.Key, Chain));

            }



            return Merged;

        }



        /// <summary>

        /// Attempts to join two polylines at any endpoint pair within merge tolerance.

        /// </summary>

        static private bool TryJoinPolylines

            (

            List<(double Lat, double Lon)> A,

            List<(double Lat, double Lon)> B,

            double CentreLat,

            out List<(double Lat, double Lon)> Combined

            )

        {

            Combined = new List<(double Lat, double Lon)>();

            if ((A.Count == 0) || (B.Count == 0))

                return false;



            (double Lat, double Lon) AStart = A[0];

            (double Lat, double Lon) AEnd = A[^1];

            (double Lat, double Lon) BStart = B[0];

            (double Lat, double Lon) BEnd = B[^1];



            if (DistanceMetres(AEnd.Lat, AEnd.Lon, BStart.Lat, BStart.Lon, CentreLat) <= MERGE_EPSILON_METRES)

            {

                Combined.AddRange(A);

                Combined.AddRange(B);

                return true;

            }



            if (DistanceMetres(AEnd.Lat, AEnd.Lon, BEnd.Lat, BEnd.Lon, CentreLat) <= MERGE_EPSILON_METRES)

            {

                Combined.AddRange(A);

                B.Reverse();

                Combined.AddRange(B);

                return true;

            }



            if (DistanceMetres(AStart.Lat, AStart.Lon, BEnd.Lat, BEnd.Lon, CentreLat) <= MERGE_EPSILON_METRES)

            {

                A.Reverse();

                Combined.AddRange(A);

                Combined.AddRange(B);

                return true;

            }



            if (DistanceMetres(AStart.Lat, AStart.Lon, BStart.Lat, BStart.Lon, CentreLat) <= MERGE_EPSILON_METRES)

            {

                A.Reverse();

                Combined.AddRange(A);

                B.Reverse();

                Combined.AddRange(B);

                return true;

            }



            return false;

        }

    }

}



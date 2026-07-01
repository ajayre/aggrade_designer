using System.Collections.Concurrent;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Evenly spaced contour extraction (AgGrade cold-open marching squares).
    /// </summary>
    static public class ContourRenderer
    {
        private const int RESOLUTION_MULTIPLIER = 4;

        private const double MERGE_EPSILON_CELL_FACTOR = 0.38;

        private const int CONTOUR_COUNT = 32;

        private const int BAND_COUNT = CONTOUR_COUNT + 1;

        private const double CLIP_JOIN_EPSILON_METRES = 0.05;

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
            double VMax,
            IProgress<int>? Progress = null
            )
        {
            double[] Levels = GetContourLevels(VMin, VMax);
            if (Levels.Length == 0)
            {
                Progress?.Report(100);
                return Array.Empty<(double, IReadOnlyList<(double Lat, double Lon)>)>();
            }

            if (Surface.SampleWidth <= 0 || Surface.SampleHeight <= 0)
            {
                Progress?.Report(100);
                return Array.Empty<(double, IReadOnlyList<(double Lat, double Lon)>)>();
            }

            Progress?.Report(0);
            int FineWidth = (Surface.SampleWidth - 1) * RESOLUTION_MULTIPLIER + 1;
            int FineHeight = (Surface.SampleHeight - 1) * RESOLUTION_MULTIPLIER + 1;
            double[,] HeightGrid = new double[FineHeight, FineWidth];
            bool[,] InsideField = new bool[FineHeight, FineWidth];
            Surface.BuildContourFineGrid(ContentView, RESOLUTION_MULTIPLIER, HeightGrid, InsideField, ProgressMapper.Subrange(Progress, 0, 35));

            double CentreLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
            double MergeEpsilonMetres = ComputeMergeEpsilonMetres(Surface, FineWidth, FineHeight, CentreLat);
            var LevelResults = new ConcurrentBag<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)>();
            int CompletedLevels = 0;

            Parallel.For(0, Levels.Length, LevelIndex =>
            {
                double Level = Levels[LevelIndex];
                List<(double LatA, double LonA, double LatB, double LonB)> Segments =
                    new List<(double LatA, double LonA, double LatB, double LonB)>();

                for (int Y = 0; Y < FineHeight - 1; Y++)
                {
                    for (int X = 0; X < FineWidth - 1; X++)
                    {
                        CollectCellSegments(
                            HeightGrid,
                            FineWidth,
                            FineHeight,
                            Surface,
                            X,
                            Y,
                            Level,
                            Segments);
                    }
                }

                foreach (IReadOnlyList<(double Lat, double Lon)> Polyline in TracePolylines(Segments, CentreLat, MergeEpsilonMetres))
                {
                    List<(double Lat, double Lon)> Smoothed = ContourSmoother.Smooth(Polyline);
                    foreach (List<(double Lat, double Lon)> Clipped in ClipPolylineToBoundary(Smoothed, Surface, CentreLat))
                    {
                        if (Clipped.Count >= 2)
                            LevelResults.Add((Level, Clipped));
                    }
                }

                int Done = Interlocked.Increment(ref CompletedLevels);
                ProgressMapper.ReportSegment(Progress, 35, 100, Done * 100 / Levels.Length);
            });

            List<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Result =
                LevelResults.OrderBy(Entry => Entry.Level).ToList();

            Progress?.Report(100);
            return Result;
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
        /// Computes endpoint merge tolerance from the fine-grid cell size in metres.
        /// </summary>
        /// <param name="Surface">Surface sampler defining geographic extent.</param>
        /// <param name="FineWidth">Fine grid width in samples.</param>
        /// <param name="FineHeight">Fine grid height in samples.</param>
        /// <param name="CentreLat">Map centre latitude for distance scaling.</param>
        /// <returns>Merge tolerance in metres.</returns>
        static private double ComputeMergeEpsilonMetres
            (
            FieldSurfaceSampler Surface,
            int FineWidth,
            int FineHeight,
            double CentreLat
            )
        {
            double MetresPerDegreeLat = 111320.0;
            double MetresPerDegreeLon = MetresPerDegreeLat * Math.Cos(CentreLat * Math.PI / 180.0);
            double CellLat = (Surface.MaxLat - Surface.MinLat) / Math.Max(1, FineHeight - 1);
            double CellLon = (Surface.MaxLon - Surface.MinLon) / Math.Max(1, FineWidth - 1);
            double CellSizeM = Math.Sqrt(
                (CellLat * MetresPerDegreeLat) * (CellLat * MetresPerDegreeLat)
                + (CellLon * MetresPerDegreeLon) * (CellLon * MetresPerDegreeLon));
            return Math.Max(0.05, CellSizeM * MERGE_EPSILON_CELL_FACTOR);
        }

        /// <summary>
        /// Emits line segments crossing one grid cell at a contour level via marching squares.
        /// </summary>
        /// <param name="Heights">Supersampled scalar height grid.</param>
        /// <param name="Width">Fine grid width in samples.</param>
        /// <param name="Height">Fine grid height in samples.</param>
        /// <param name="Surface">Surface sampler for lat/lon corner mapping.</param>
        /// <param name="X">Cell column index.</param>
        /// <param name="Y">Cell row index.</param>
        /// <param name="Level">Contour elevation to extract.</param>
        /// <param name="Segments">Output segment list to append to.</param>
        static private void CollectCellSegments
            (
            double[,] Heights,
            int Width,
            int Height,
            FieldSurfaceSampler Surface,
            int X,
            int Y,
            double Level,
            List<(double LatA, double LonA, double LatB, double LonB)> Segments
            )
        {
            double VBl = Heights[Y + 1, X];
            double VBr = Heights[Y + 1, X + 1];
            double VTr = Heights[Y, X + 1];
            double VTl = Heights[Y, X];
            if (!double.IsFinite(VBl) || !double.IsFinite(VBr) || !double.IsFinite(VTr) || !double.IsFinite(VTl))
                return;

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
                return ((A.Lat + B.Lat) * 0.5, (A.Lon + B.Lon) * 0.5);

            double T = Math.Clamp((Level - Va) / (Vb - Va), 0.0, 1.0);
            return (A.Lat + (B.Lat - A.Lat) * T, A.Lon + (B.Lon - A.Lon) * T);
        }

        /// <summary>
        /// Chains segment endpoints into polylines with sub-cell vertex merging.
        /// </summary>
        /// <param name="Segments">Raw contour segments in geographic coordinates.</param>
        /// <param name="CentreLat">Map centre latitude for distance scaling.</param>
        /// <param name="MergeEpsilonMetres">Endpoint merge tolerance in metres.</param>
        /// <returns>Traced polylines ready for smoothing.</returns>
        static private List<IReadOnlyList<(double Lat, double Lon)>> TracePolylines
            (
            List<(double LatA, double LonA, double LatB, double LonB)> Segments,
            double CentreLat,
            double MergeEpsilonMetres
            )
        {
            if (Segments.Count == 0)
                return new List<IReadOnlyList<(double Lat, double Lon)>>();

            List<(double Lat, double Lon)> Points = new List<(double Lat, double Lon)>();

            int Register
                (
                double Lat,
                double Lon
                )
            {
                for (int I = 0; I < Points.Count; I++)
                {
                    if (DistanceMetres(Points[I].Lat, Points[I].Lon, Lat, Lon, CentreLat) <= MergeEpsilonMetres)
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
        /// Clips a polyline to the field boundary, returning one or more inside chains.
        /// </summary>
        /// <param name="Polyline">Input polyline in geographic coordinates.</param>
        /// <param name="BoundaryLatLon">Field boundary ring, or null to skip clipping.</param>
        /// <param name="CentreLat">Map centre latitude for distance scaling.</param>
        /// <returns>Boundary-clipped polylines.</returns>
        static private List<List<(double Lat, double Lon)>> ClipPolylineToBoundary
            (
            IReadOnlyList<(double Lat, double Lon)> Polyline,
            FieldSurfaceSampler Surface,
            double CentreLat
            )
        {
            if (Polyline.Count < 2)
                return new List<List<(double Lat, double Lon)>>();

            IReadOnlyList<(double Lat, double Lon)>? BoundaryLatLon = Surface.BoundaryLatLon;
            if (BoundaryLatLon != null && BoundaryLatLon.Count >= 3)
                return ClipPolylineToPolygon(Polyline, BoundaryLatLon, CentreLat);

            return ClipPolylineToValidRegion(Polyline, Surface, CentreLat);
        }

        /// <summary>
        /// Clips a polyline to an explicit boundary polygon, returning one or more inside chains.
        /// </summary>
        static private List<List<(double Lat, double Lon)>> ClipPolylineToPolygon
            (
            IReadOnlyList<(double Lat, double Lon)> Polyline,
            IReadOnlyList<(double Lat, double Lon)> BoundaryLatLon,
            double CentreLat
            )
        {
            if (Polyline.Count < 2)
                return new List<List<(double Lat, double Lon)>>();

            List<(double X, double Y)> Ring = BoundaryMask.BuildUtmRing(BoundaryLatLon);
            List<List<(double Lat, double Lon)>> Chains = new List<List<(double Lat, double Lon)>>();
            List<(double Lat, double Lon)>? Current = null;

            for (int I = 0; I < Polyline.Count - 1; I++)
            {
                foreach (( (double Lat, double Lon) Start, (double Lat, double Lon) End) Segment in ClipSegmentToPolygon(Polyline[I], Polyline[I + 1], Ring))
                {
                    if (Current == null)
                    {
                        Current = new List<(double Lat, double Lon)> { Segment.Start, Segment.End };
                        continue;
                    }

                    (double Lat, double Lon) Last = Current[^1];
                    if (DistanceMetres(Last.Lat, Last.Lon, Segment.Start.Lat, Segment.Start.Lon, CentreLat) > CLIP_JOIN_EPSILON_METRES)
                    {
                        if (Current.Count >= 2)
                            Chains.Add(Current);
                        Current = new List<(double Lat, double Lon)> { Segment.Start, Segment.End };
                        continue;
                    }

                    if (DistanceMetres(Last.Lat, Last.Lon, Segment.Start.Lat, Segment.Start.Lon, CentreLat) > 1e-9)
                        Current.Add(Segment.Start);
                    Current.Add(Segment.End);
                }
            }

            if (Current != null && Current.Count >= 2)
                Chains.Add(Current);

            return Chains;
        }

        /// <summary>
        /// Clips a polyline to the valid mesh region when no explicit boundary exists.
        /// </summary>
        static private List<List<(double Lat, double Lon)>> ClipPolylineToValidRegion
            (
            IReadOnlyList<(double Lat, double Lon)> Polyline,
            FieldSurfaceSampler Surface,
            double CentreLat
            )
        {
            List<List<(double Lat, double Lon)>> Chains = new List<List<(double Lat, double Lon)>>();
            List<(double Lat, double Lon)>? Current = null;

            for (int I = 0; I < Polyline.Count - 1; I++)
            {
                foreach (((double Lat, double Lon) Start, (double Lat, double Lon) End) Segment in
                         ClipSegmentToValidRegion(Polyline[I], Polyline[I + 1], Surface))
                {
                    if (Current == null)
                    {
                        Current = new List<(double Lat, double Lon)> { Segment.Start, Segment.End };
                        continue;
                    }

                    (double Lat, double Lon) Last = Current[^1];
                    if (DistanceMetres(Last.Lat, Last.Lon, Segment.Start.Lat, Segment.Start.Lon, CentreLat) > CLIP_JOIN_EPSILON_METRES)
                    {
                        if (Current.Count >= 2)
                            Chains.Add(Current);
                        Current = new List<(double Lat, double Lon)> { Segment.Start, Segment.End };
                        continue;
                    }

                    if (DistanceMetres(Last.Lat, Last.Lon, Segment.Start.Lat, Segment.Start.Lon, CentreLat) > 1e-9)
                        Current.Add(Segment.Start);
                    Current.Add(Segment.End);
                }
            }

            if (Current != null && Current.Count >= 2)
                Chains.Add(Current);

            return Chains;
        }

        /// <summary>
        /// Returns the inside portions of a segment clipped to the valid mesh region.
        /// </summary>
        static private List<((double Lat, double Lon) Start, (double Lat, double Lon) End)> ClipSegmentToValidRegion
            (
            (double Lat, double Lon) Start,
            (double Lat, double Lon) End,
            FieldSurfaceSampler Surface
            )
        {
            const int Samples = 32;
            List<double> Params = new List<double> { 0.0, 1.0 };
            bool PrevInside = Surface.IsInsideValidRegion(Start.Lat, Start.Lon);
            for (int I = 1; I <= Samples; I++)
            {
                double T = (double)I / Samples;
                (double Lat, double Lon) Point = InterpolateLatLon(Start, End, T);
                bool Inside = Surface.IsInsideValidRegion(Point.Lat, Point.Lon);
                if (Inside != PrevInside)
                {
                    double CrossT = RefineValidRegionCrossing(Start, End, T - 1.0 / Samples, T, Surface, Inside);
                    Params.Add(CrossT);
                    PrevInside = Inside;
                }
            }

            Params.Sort();
            List<double> Unique = new List<double>();
            foreach (double T in Params)
            {
                if (Unique.Count == 0 || Math.Abs(T - Unique[^1]) > 1e-9)
                    Unique.Add(T);
            }

            List<((double Lat, double Lon) Start, (double Lat, double Lon) End)> Segments =
                new List<((double Lat, double Lon), (double Lat, double Lon))>();

            for (int I = 0; I < Unique.Count - 1; I++)
            {
                double T0 = Unique[I];
                double T1 = Unique[I + 1];
                if (T1 - T0 < 1e-10)
                    continue;

                (double Lat, double Lon) Mid = InterpolateLatLon(Start, End, (T0 + T1) * 0.5);
                if (!Surface.IsInsideValidRegion(Mid.Lat, Mid.Lon))
                    continue;

                Segments.Add((InterpolateLatLon(Start, End, T0), InterpolateLatLon(Start, End, T1)));
            }

            return Segments;
        }

        static private (double Lat, double Lon) InterpolateLatLon
            (
            (double Lat, double Lon) Start,
            (double Lat, double Lon) End,
            double T
            )
        {
            return (
                Start.Lat + (End.Lat - Start.Lat) * T,
                Start.Lon + (End.Lon - Start.Lon) * T);
        }

        static private double RefineValidRegionCrossing
            (
            (double Lat, double Lon) Start,
            (double Lat, double Lon) End,
            double T0,
            double T1,
            FieldSurfaceSampler Surface,
            bool TargetInside
            )
        {
            for (int I = 0; I < 20; I++)
            {
                double MidT = (T0 + T1) * 0.5;
                (double Lat, double Lon) Mid = InterpolateLatLon(Start, End, MidT);
                if (Surface.IsInsideValidRegion(Mid.Lat, Mid.Lon) == TargetInside)
                    T1 = MidT;
                else
                    T0 = MidT;
            }

            return (T0 + T1) * 0.5;
        }

        /// <summary>
        /// Returns the inside portions of a segment clipped to a closed polygon.
        /// </summary>
        /// <param name="Start">Segment start in geographic coordinates.</param>
        /// <param name="End">Segment end in geographic coordinates.</param>
        /// <param name="Ring">Closed polygon in UTM coordinates.</param>
        /// <returns>Inside sub-segments.</returns>
        static private List<((double Lat, double Lon) Start, (double Lat, double Lon) End)> ClipSegmentToPolygon
            (
            (double Lat, double Lon) Start,
            (double Lat, double Lon) End,
            IReadOnlyList<(double X, double Y)> Ring
            )
        {
            UTM.UTMCoordinate UtmStart = UTM.FromLatLon(Start.Lat, Start.Lon);
            UTM.UTMCoordinate UtmEnd = UTM.FromLatLon(End.Lat, End.Lon);
            double Ax = UtmStart.Easting;
            double Ay = UtmStart.Northing;
            double Bx = UtmEnd.Easting;
            double By = UtmEnd.Northing;
            double Dx = Bx - Ax;
            double Dy = By - Ay;

            List<double> Params = new List<double> { 0.0, 1.0 };
            int Count = Ring.Count;
            for (int I = 0; I < Count; I++)
            {
                int J = (I + 1) % Count;
                if (TrySegmentEdgeIntersection(Ax, Ay, Dx, Dy, Ring[I].X, Ring[I].Y, Ring[J].X, Ring[J].Y, out double T)
                    && T > 1e-9 && T < 1.0 - 1e-9)
                {
                    Params.Add(T);
                }
            }

            Params.Sort();
            List<double> Unique = new List<double>();
            foreach (double T in Params)
            {
                if (Unique.Count == 0 || Math.Abs(T - Unique[^1]) > 1e-9)
                    Unique.Add(T);
            }

            List<((double Lat, double Lon), (double Lat, double Lon))> Segments =
                new List<((double Lat, double Lon), (double Lat, double Lon))>();

            for (int I = 0; I < Unique.Count - 1; I++)
            {
                double T0 = Unique[I];
                double T1 = Unique[I + 1];
                if (T1 - T0 < 1e-10)
                    continue;

                double MidX = Ax + Dx * (T0 + T1) * 0.5;
                double MidY = Ay + Dy * (T0 + T1) * 0.5;
                if (!BoundaryMask.IsInsideBoundary(MidX, MidY, Ring))
                    continue;

                (double Lat, double Lon) Pa = (
                    Start.Lat + (End.Lat - Start.Lat) * T0,
                    Start.Lon + (End.Lon - Start.Lon) * T0);
                (double Lat, double Lon) Pb = (
                    Start.Lat + (End.Lat - Start.Lat) * T1,
                    Start.Lon + (End.Lon - Start.Lon) * T1);
                Segments.Add((Pa, Pb));
            }

            return Segments;
        }

        /// <summary>
        /// Computes the parametric intersection of a segment with a polygon edge.
        /// </summary>
        /// <param name="Ax">Segment start easting.</param>
        /// <param name="Ay">Segment start northing.</param>
        /// <param name="Dx">Segment delta easting.</param>
        /// <param name="Dy">Segment delta northing.</param>
        /// <param name="Ex0">Edge start easting.</param>
        /// <param name="Ey0">Edge start northing.</param>
        /// <param name="Ex1">Edge end easting.</param>
        /// <param name="Ey1">Edge end northing.</param>
        /// <param name="T">Output intersection parameter along the segment.</param>
        /// <returns>True when the edges intersect within both segments.</returns>
        static private bool TrySegmentEdgeIntersection
            (
            double Ax,
            double Ay,
            double Dx,
            double Dy,
            double Ex0,
            double Ey0,
            double Ex1,
            double Ey1,
            out double T
            )
        {
            T = 0;
            double EdgeDx = Ex1 - Ex0;
            double EdgeDy = Ey1 - Ey0;
            double Denominator = (Dx * EdgeDy) - (Dy * EdgeDx);
            if (Math.Abs(Denominator) < 1e-12)
                return false;

            double Qpx = Ex0 - Ax;
            double Qpy = Ey0 - Ay;
            T = ((Qpx * EdgeDy) - (Qpy * EdgeDx)) / Denominator;
            double S = ((Qpx * Dy) - (Qpy * Dx)) / Denominator;
            return T >= 0.0 && T <= 1.0 && S >= 0.0 && S <= 1.0;
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
    }
}

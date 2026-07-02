using AgGradeDesigner.Core;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Identifies which view-cube region was hit.
    /// </summary>
    internal enum ViewCubePartKind
    {
        None,
        Face,
        Edge,
        Vertex
    }

    /// <summary>
    /// World-oriented faces on the navigation cube (+X east, +Y north, +Z up).
    /// </summary>
    internal enum ViewCubeFace
    {
        Top,
        Bottom,
        Front,
        Back,
        Right,
        Left
    }

    /// <summary>
    /// Result of a view-cube hit test.
    /// </summary>
    internal readonly struct ViewCubeHit
    {
        /// <summary>
        /// Creates a hit result for the given view-cube part.
        /// </summary>
        /// <param name="Kind">Region kind (face, edge, or vertex).</param>
        /// <param name="Index">Index into the corresponding topology table.</param>
        public ViewCubeHit
            (
            ViewCubePartKind Kind,
            int Index
            )
        {
            this.Kind = Kind;
            this.Index = Index;
        }

        /// <summary>Active view-cube region kind (none, face, edge, or vertex).</summary>
        public ViewCubePartKind Kind { get; }
        /// <summary>Index into the face, edge, or vertex table for the hit region.</summary>
        public int Index { get; }
        /// <summary>True when a face, edge, or vertex was hit.</summary>
        public bool IsHit => Kind != ViewCubePartKind.None;

        /// <summary>Sentinel index returned by <see cref="None"/>.</summary>
        private const int HIT_NONE_INDEX = -1;

        /// <summary>Hit result indicating no view-cube region was under the pointer.</summary>
        static public ViewCubeHit None => new ViewCubeHit(ViewCubePartKind.None, HIT_NONE_INDEX);
    }

    /// <summary>
    /// Draws and hit-tests the 3D navigation cube shown in the top-left of the 3D viewer.
    /// </summary>
    static internal class ViewCubeRenderer
    {
        /// <summary>
        /// Screen position plus depth for view-cube painter ordering.
        /// </summary>
        private readonly struct ProjectedPoint
        {
            /// <summary>
            /// Creates a screen-space point with depth for painter ordering.
            /// </summary>
            /// <param name="X">Screen X in widget-local pixels.</param>
            /// <param name="Y">Screen Y in widget-local pixels.</param>
            /// <param name="Z">Camera-space depth for back-to-front sort.</param>
            public ProjectedPoint
                (
                float X,
                float Y,
                float Z
                )
            {
                this.X = X;
                this.Y = Y;
                this.Z = Z;
            }

            /// <summary>Screen X coordinate in widget-local pixels.</summary>
            public float X { get; }
            /// <summary>Screen Y coordinate in widget-local pixels.</summary>
            public float Y { get; }
            /// <summary>Camera-space depth used for face sorting.</summary>
            public float Z { get; }
        }

        /// <summary>Radius of the visible cube content within the widget.</summary>
        private const float CUBE_CONTENT_RADIUS = 26f;
        /// <summary>Extra padding around the cube for easier hit testing.</summary>
        private const float CUBE_INTERACTION_PAD = 10f;
        /// <summary>Multiplier applied when computing total widget size from content radius.</summary>
        private const float CUBE_WIDGET_HALF_SIZE_MULTIPLIER = 2f;
        /// <summary>Total widget width and height including interaction padding.</summary>
        private const float CUBE_WIDGET_SIZE = (CUBE_CONTENT_RADIUS + CUBE_INTERACTION_PAD) * CUBE_WIDGET_HALF_SIZE_MULTIPLIER;
        /// <summary>Maximum radius of a unit cube corner from its centre.</summary>
        private const float UNIT_CUBE_MAX_RADIUS = 0.866f;
        /// <summary>Scale factor mapping unit cube coordinates to widget pixels.</summary>
        private const float CUBE_SCALE_FACTOR = CUBE_CONTENT_RADIUS / (UNIT_CUBE_MAX_RADIUS * CUBE_WIDGET_SIZE);
        /// <summary>Minimum camera-space Z for a face to be considered front-facing.</summary>
        private const double FACE_CULL_THRESHOLD = -0.05;
        /// <summary>Fraction of corner Z values averaged for face depth sort key.</summary>
        private const float FACE_DEPTH_AVERAGE_FRACTION = 0.25f;
        /// <summary>Fraction toward face centre used when insetting hover highlights.</summary>
        private const float HIGHLIGHT_INSET = 0.62f;
        /// <summary>Hit-test radius in pixels for cube edges.</summary>
        private const float EDGE_HIT_RADIUS = 9f;
        /// <summary>Hit-test radius in pixels for cube vertices.</summary>
        private const float VERTEX_HIT_RADIUS = 10f;
        /// <summary>Stroke width for face outline edges.</summary>
        private const float FACE_EDGE_STROKE_WIDTH = 1.25f;
        /// <summary>Default pitch in degrees when resolving orientation from a degenerate normal.</summary>
        private const double DEFAULT_PITCH_DEG = 30.0;
        /// <summary>Minimum normal vector length before orientation fallback.</summary>
        private const double MIN_NORMAL_LENGTH = 1e-9;
        /// <summary>Minimum horizontal normal component before heading is computed.</summary>
        private const double MIN_HORIZONTAL_NORMAL = 1e-6;
        /// <summary>Minimum projected edge length before edge highlight is drawn.</summary>
        private const float MIN_EDGE_LENGTH = 1e-3f;
        /// <summary>Half-width of edge hover highlight patch.</summary>
        private const float EDGE_HIGHLIGHT_HALF_WIDTH = 7f;
        /// <summary>Fraction of edge length used for highlight patch half-length.</summary>
        private const float EDGE_HIGHLIGHT_LENGTH_FRACTION = 0.34f;
        /// <summary>Maximum half-length of edge hover highlight in pixels.</summary>
        private const float EDGE_HIGHLIGHT_MAX_HALF_LENGTH = 16f;
        /// <summary>Midpoint fraction (0.5) for edge-centred highlights.</summary>
        private const float EDGE_MIDPOINT_FRACTION = 0.5f;
        /// <summary>Side length of vertex hover highlight square.</summary>
        private const float VERTEX_HIGHLIGHT_SIZE = 11f;
        /// <summary>Half-extent of the unit cube along each axis.</summary>
        private const float UNIT_CUBE_HALF_EXTENT = 0.5f;
        /// <summary>Local origin for widget-local coordinate space.</summary>
        private const float WIDGET_LOCAL_ORIGIN = 0f;
        /// <summary>Heading threshold below which 360° is added to normalize negative angles.</summary>
        private const double NEGATIVE_HEADING_THRESHOLD = 0.0;
        /// <summary>Red channel of default face fill colour.</summary>
        private const byte FACE_FILL_RED = 232;
        /// <summary>Green channel of default face fill colour.</summary>
        private const byte FACE_FILL_GREEN = 242;
        /// <summary>Blue channel of default face fill colour.</summary>
        private const byte FACE_FILL_BLUE = 252;
        /// <summary>Alpha channel of default face fill colour.</summary>
        private const byte FACE_FILL_ALPHA = 210;
        /// <summary>Red channel of hover highlight fill.</summary>
        private const byte HIGHLIGHT_FILL_RED = 0;
        /// <summary>Green channel of hover highlight fill.</summary>
        private const byte HIGHLIGHT_FILL_GREEN = 140;
        /// <summary>Blue channel of hover highlight fill.</summary>
        private const byte HIGHLIGHT_FILL_BLUE = 230;
        /// <summary>Alpha channel of hover highlight fill.</summary>
        private const byte HIGHLIGHT_FILL_ALPHA = 235;
        /// <summary>Red channel of face edge stroke colour.</summary>
        private const byte EDGE_COLOR_RED = 0;
        /// <summary>Green channel of face edge stroke colour.</summary>
        private const byte EDGE_COLOR_GREEN = 114;
        /// <summary>Blue channel of face edge stroke colour.</summary>
        private const byte EDGE_COLOR_BLUE = 198;
        /// <summary>Unit normal X component (zero) for axis-aligned normals.</summary>
        private const double UNIT_NORMAL_X = 0.0;
        /// <summary>Unit normal +Z component.</summary>
        private const double UNIT_NORMAL_Z = 1.0;
        /// <summary>Unit normal −Z component.</summary>
        private const double UNIT_NORMAL_NEG_Z = -1.0;
        /// <summary>Unit normal −Y component.</summary>
        private const double UNIT_NORMAL_NEG_Y = -1.0;
        /// <summary>Unit normal +Y component.</summary>
        private const double UNIT_NORMAL_Y = 1.0;
        /// <summary>Unit normal −X component.</summary>
        private const double UNIT_NORMAL_NEG_X = -1.0;
        /// <summary>Minimum barycentric weight for point-in-triangle test.</summary>
        private const float BARYCENTRIC_MIN = 0f;

        /// <summary>Minimum triangle area denominator to reject degenerate hit tests.</summary>
        private const float MIN_TRIANGLE_DENOMINATOR = 1e-6f;
        /// <summary>Minimum squared segment length before point-to-segment distance falls back to endpoint.</summary>
        private const float MIN_SEGMENT_LENGTH_SQ = 1e-6f;
        /// <summary>Default forward normal Y when normalizing a zero vector.</summary>
        private const float DEFAULT_FORWARD_NORMAL_Y = -1.0f;
        /// <summary>Minimum segment parameter for closest-point clamp.</summary>
        private const float SEGMENT_PARAMETER_MIN = 0f;
        /// <summary>Maximum segment parameter for closest-point clamp.</summary>
        private const float SEGMENT_PARAMETER_MAX = 1f;
        /// <summary>Expected sum of barycentric weights inside a triangle.</summary>
        private const float BARYCENTRIC_WEIGHT_SUM = 1f;

        /// <summary>Default fill colour for visible cube faces.</summary>
        private static readonly SKColor FACE_FILL = new SKColor(FACE_FILL_RED, FACE_FILL_GREEN, FACE_FILL_BLUE, FACE_FILL_ALPHA);
        /// <summary>Fill colour for hovered face, edge, or vertex regions.</summary>
        private static readonly SKColor HIGHLIGHT_FILL = new SKColor(HIGHLIGHT_FILL_RED, HIGHLIGHT_FILL_GREEN, HIGHLIGHT_FILL_BLUE, HIGHLIGHT_FILL_ALPHA);
        /// <summary>Stroke colour for cube face outlines.</summary>
        private static readonly SKColor EDGE_COLOR = new SKColor(EDGE_COLOR_RED, EDGE_COLOR_GREEN, EDGE_COLOR_BLUE);

        /// <summary>Corner indices for each cube face quad in unit-cube vertex order.</summary>
        private static readonly (int A, int B, int C, int D)[] FaceVertexIndices =
        {
            (4, 5, 6, 7),
            (0, 3, 2, 1),
            (0, 4, 5, 1),
            (3, 7, 6, 2),
            (1, 5, 6, 2),
            (4, 0, 3, 7)
        };

        /// <summary>Outward unit normals for each cube face (+X east, +Y north, +Z up).</summary>
        private static readonly (double X, double Y, double Z)[] FaceNormals =
        {
            (UNIT_NORMAL_X, UNIT_NORMAL_X, UNIT_NORMAL_Z),
            (UNIT_NORMAL_X, UNIT_NORMAL_X, UNIT_NORMAL_NEG_Z),
            (UNIT_NORMAL_X, UNIT_NORMAL_NEG_Y, UNIT_NORMAL_X),
            (UNIT_NORMAL_X, UNIT_NORMAL_Y, UNIT_NORMAL_X),
            (UNIT_NORMAL_Z, UNIT_NORMAL_X, UNIT_NORMAL_X),
            (UNIT_NORMAL_NEG_X, UNIT_NORMAL_X, UNIT_NORMAL_X)
        };

        /// <summary>Vertex index pairs for each cube edge.</summary>
        private static readonly (int A, int B)[] EdgeVertexPairs =
        {
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7)
        };

        /// <summary>Adjacent face indices for each cube edge.</summary>
        private static readonly (int FaceA, int FaceB)[] EdgeFacePairs =
        {
            ((int)ViewCubeFace.Bottom, (int)ViewCubeFace.Front),
            ((int)ViewCubeFace.Bottom, (int)ViewCubeFace.Right),
            ((int)ViewCubeFace.Bottom, (int)ViewCubeFace.Back),
            ((int)ViewCubeFace.Bottom, (int)ViewCubeFace.Left),
            ((int)ViewCubeFace.Top, (int)ViewCubeFace.Front),
            ((int)ViewCubeFace.Top, (int)ViewCubeFace.Right),
            ((int)ViewCubeFace.Top, (int)ViewCubeFace.Back),
            ((int)ViewCubeFace.Top, (int)ViewCubeFace.Left),
            ((int)ViewCubeFace.Front, (int)ViewCubeFace.Left),
            ((int)ViewCubeFace.Front, (int)ViewCubeFace.Right),
            ((int)ViewCubeFace.Back, (int)ViewCubeFace.Right),
            ((int)ViewCubeFace.Back, (int)ViewCubeFace.Left)
        };

        /// <summary>Face indices meeting at each cube vertex.</summary>
        private static readonly int[][] VertexFaceIndices =
        {
            new[] { (int)ViewCubeFace.Bottom, (int)ViewCubeFace.Front, (int)ViewCubeFace.Left },
            new[] { (int)ViewCubeFace.Bottom, (int)ViewCubeFace.Front, (int)ViewCubeFace.Right },
            new[] { (int)ViewCubeFace.Bottom, (int)ViewCubeFace.Back, (int)ViewCubeFace.Right },
            new[] { (int)ViewCubeFace.Bottom, (int)ViewCubeFace.Back, (int)ViewCubeFace.Left },
            new[] { (int)ViewCubeFace.Top, (int)ViewCubeFace.Front, (int)ViewCubeFace.Left },
            new[] { (int)ViewCubeFace.Top, (int)ViewCubeFace.Front, (int)ViewCubeFace.Right },
            new[] { (int)ViewCubeFace.Top, (int)ViewCubeFace.Back, (int)ViewCubeFace.Right },
            new[] { (int)ViewCubeFace.Top, (int)ViewCubeFace.Back, (int)ViewCubeFace.Left }
        };

        /// <summary>Unit cube vertex coordinates centred at the origin.</summary>
        private static readonly (double X, double Y, double Z)[] UnitCubeVertices =
        {
            (-UNIT_CUBE_HALF_EXTENT, -UNIT_CUBE_HALF_EXTENT, -UNIT_CUBE_HALF_EXTENT),
            (UNIT_CUBE_HALF_EXTENT, -UNIT_CUBE_HALF_EXTENT, -UNIT_CUBE_HALF_EXTENT),
            (UNIT_CUBE_HALF_EXTENT, UNIT_CUBE_HALF_EXTENT, -UNIT_CUBE_HALF_EXTENT),
            (-UNIT_CUBE_HALF_EXTENT, UNIT_CUBE_HALF_EXTENT, -UNIT_CUBE_HALF_EXTENT),
            (-UNIT_CUBE_HALF_EXTENT, -UNIT_CUBE_HALF_EXTENT, UNIT_CUBE_HALF_EXTENT),
            (UNIT_CUBE_HALF_EXTENT, -UNIT_CUBE_HALF_EXTENT, UNIT_CUBE_HALF_EXTENT),
            (UNIT_CUBE_HALF_EXTENT, UNIT_CUBE_HALF_EXTENT, UNIT_CUBE_HALF_EXTENT),
            (-UNIT_CUBE_HALF_EXTENT, UNIT_CUBE_HALF_EXTENT, UNIT_CUBE_HALF_EXTENT)
        };

        /// <summary>
        /// Returns the screen bounds occupied by the view cube widget.
        /// </summary>
        static internal SKRect GetBounds
            (
            SKRect Viewport
            )
        {
            float CentreX = Viewport.Left + RenderingConstants.OVERLAY_WIDGET_MARGIN + CUBE_CONTENT_RADIUS;
            float CentreY = Viewport.Top + RenderingConstants.OVERLAY_WIDGET_MARGIN + CUBE_CONTENT_RADIUS;
            float HalfWidget = CUBE_WIDGET_SIZE * EDGE_MIDPOINT_FRACTION;
            return SKRect.Create(CentreX - HalfWidget, CentreY - HalfWidget, CUBE_WIDGET_SIZE, CUBE_WIDGET_SIZE);
        }

        /// <summary>
        /// Draws the view cube in the top-left widget; bottom face stays parallel to the ground plane.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="WidgetRect">Widget-local rectangle (0,0,width,height) with no extra margin.</param>
        /// <param name="HeadingDeg">Current orbit heading in degrees.</param>
        /// <param name="PitchDeg">Unused for display; kept for call-site compatibility.</param>
        /// <param name="Hover">Active hover target, if any.</param>
        static internal void Draw
            (
            SKCanvas Canvas,
            SKRect WidgetRect,
            double HeadingDeg,
            double PitchDeg,
            ViewCubeHit Hover
            )
        {
            float CentreX = WidgetRect.MidX;
            float CentreY = WidgetRect.MidY;
            float Scale = Math.Min(WidgetRect.Width, WidgetRect.Height) * CUBE_SCALE_FACTOR;

            ProjectedPoint[] Projected = ProjectVertices(HeadingDeg, PitchDeg, CentreX, CentreY, Scale);
            List<(int FaceIndex, double Depth)> VisibleFaces = new List<(int, double)>();
            for (int FaceIndex = 0; FaceIndex < FaceVertexIndices.Length; FaceIndex++)
            {
                (int A, int B, int C, int D) Face = FaceVertexIndices[FaceIndex];
                double Depth = (Projected[Face.A].Z + Projected[Face.B].Z + Projected[Face.C].Z + Projected[Face.D].Z) * FACE_DEPTH_AVERAGE_FRACTION;
                if (FaceDepthSign(FaceIndex, HeadingDeg, PitchDeg) >= FACE_CULL_THRESHOLD)
                    VisibleFaces.Add((FaceIndex, Depth));
            }

            VisibleFaces.Sort((A, B) => A.Depth.CompareTo(B.Depth));

            using SKPaint FacePaint = new SKPaint
            {
                Color = FACE_FILL,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            using SKPaint EdgePaint = new SKPaint
            {
                Color = EDGE_COLOR,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = FACE_EDGE_STROKE_WIDTH,
                IsAntialias = true
            };
            using SKPaint HighlightPaint = new SKPaint
            {
                Color = HIGHLIGHT_FILL,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            foreach ((int FaceIndex, double _) in VisibleFaces)
            {
                (int A, int B, int C, int D) Face = FaceVertexIndices[FaceIndex];
                using SKPath FacePath = BuildQuadPath(Projected, Face.A, Face.B, Face.C, Face.D);
                Canvas.DrawPath(FacePath, FacePaint);
                Canvas.DrawPath(FacePath, EdgePaint);

                if ((Hover.Kind == ViewCubePartKind.Face) && (Hover.Index == FaceIndex))
                    DrawFaceHighlight(Canvas, Projected, Face, HighlightPaint);
            }

            if ((Hover.Kind == ViewCubePartKind.Edge) && IsPartVisible(Hover, HeadingDeg, PitchDeg))
                DrawEdgeHighlight(Canvas, Projected, Hover.Index, HighlightPaint);

            if ((Hover.Kind == ViewCubePartKind.Vertex) && IsPartVisible(Hover, HeadingDeg, PitchDeg))
                DrawVertexHighlight(Canvas, Projected, Hover.Index, HighlightPaint, EdgePaint);
        }

        /// <summary>
        /// Hit-tests a screen point against the view cube.
        /// </summary>
        static internal ViewCubeHit HitTest
            (
            SKRect Viewport,
            double HeadingDeg,
            double PitchDeg,
            float ScreenX,
            float ScreenY
            )
        {
            SKRect WidgetBounds = GetBounds(Viewport);
            if (!WidgetBounds.Contains(ScreenX, ScreenY))
                return ViewCubeHit.None;

            SKRect LocalWidget = SKRect.Create(WIDGET_LOCAL_ORIGIN, WIDGET_LOCAL_ORIGIN, WidgetBounds.Width, WidgetBounds.Height);
            float LocalX = ScreenX - WidgetBounds.Left;
            float LocalY = ScreenY - WidgetBounds.Top;

            float CentreX = LocalWidget.MidX;
            float CentreY = LocalWidget.MidY;
            float Scale = Math.Min(LocalWidget.Width, LocalWidget.Height) * CUBE_SCALE_FACTOR;
            ProjectedPoint[] Projected = ProjectVertices(HeadingDeg, PitchDeg, CentreX, CentreY, Scale);

            for (int VertexIndex = 0; VertexIndex < Projected.Length; VertexIndex++)
            {
                ViewCubeHit Candidate = new ViewCubeHit(ViewCubePartKind.Vertex, VertexIndex);
                if (!IsPartVisible(Candidate, HeadingDeg, PitchDeg))
                    continue;

                if (Distance(Projected[VertexIndex], LocalX, LocalY) <= VERTEX_HIT_RADIUS)
                    return Candidate;
            }

            for (int EdgeIndex = 0; EdgeIndex < EdgeVertexPairs.Length; EdgeIndex++)
            {
                ViewCubeHit Candidate = new ViewCubeHit(ViewCubePartKind.Edge, EdgeIndex);
                if (!IsPartVisible(Candidate, HeadingDeg, PitchDeg))
                    continue;

                (int A, int B) Edge = EdgeVertexPairs[EdgeIndex];
                if (DistanceToSegment(Projected[Edge.A], Projected[Edge.B], LocalX, LocalY) <= EDGE_HIT_RADIUS)
                    return Candidate;
            }

            List<(int FaceIndex, double Depth)> VisibleFaces = new List<(int, double)>();
            for (int FaceIndex = 0; FaceIndex < FaceVertexIndices.Length; FaceIndex++)
            {
                if (FaceDepthSign(FaceIndex, HeadingDeg, PitchDeg) < FACE_CULL_THRESHOLD)
                    continue;

                (int A, int B, int C, int D) Face = FaceVertexIndices[FaceIndex];
                double Depth = (Projected[Face.A].Z + Projected[Face.B].Z + Projected[Face.C].Z + Projected[Face.D].Z) * FACE_DEPTH_AVERAGE_FRACTION;
                VisibleFaces.Add((FaceIndex, Depth));
            }

            VisibleFaces.Sort((A, B) => B.Depth.CompareTo(A.Depth));

            foreach ((int FaceIndex, double _) in VisibleFaces)
            {
                (int A, int B, int C, int D) Face = FaceVertexIndices[FaceIndex];
                if (PointInQuad(Projected, Face.A, Face.B, Face.C, Face.D, LocalX, LocalY))
                    return new ViewCubeHit(ViewCubePartKind.Face, FaceIndex);
            }

            return ViewCubeHit.None;
        }

        /// <summary>
        /// Resolves the orbit heading and pitch that look at the hit region.
        /// </summary>
        static internal (double HeadingDeg, double PitchDeg) ResolveTargetOrientation
            (
            ViewCubeHit Hit,
            double CurrentHeadingDeg
            )
        {
            if (!Hit.IsHit)
                return (CurrentHeadingDeg, DEFAULT_PITCH_DEG);

            (double X, double Y, double Z) Normal = Hit.Kind switch
            {
                ViewCubePartKind.Face => FaceNormals[Hit.Index],
                ViewCubePartKind.Edge => AverageNormalsForEdge(Hit.Index),
                ViewCubePartKind.Vertex => AverageNormalsForVertex(Hit.Index),
                _ => (UNIT_NORMAL_X, DEFAULT_FORWARD_NORMAL_Y, UNIT_NORMAL_X)
            };

            return OrientationFromNormal(Normal, CurrentHeadingDeg);
        }

        /// <summary>
        /// Converts a world-space outward normal into orbit heading and pitch.
        /// </summary>
        static internal (double HeadingDeg, double PitchDeg) OrientationFromNormal
            (
            (double X, double Y, double Z) Normal,
            double CurrentHeadingDeg
            )
        {
            double Length = Math.Sqrt((Normal.X * Normal.X) + (Normal.Y * Normal.Y) + (Normal.Z * Normal.Z));
            if (Length <= MIN_NORMAL_LENGTH)
                return (CurrentHeadingDeg, DEFAULT_PITCH_DEG);

            double Nx = Normal.X / Length;
            double Ny = Normal.Y / Length;
            double Nz = Normal.Z / Length;

            double Horizontal = Math.Sqrt((Nx * Nx) + (Ny * Ny));
            double PitchDeg = Math.Atan2(Nz, Horizontal) * FieldConstants.DEGREES_PER_RADIAN / Math.PI;
            PitchDeg = Math.Clamp(PitchDeg, FieldCamera3D.MinimumOrbitPitchDeg, RenderingConstants.MAX_ORBIT_PITCH_DEG);

            if (Horizontal <= MIN_HORIZONTAL_NORMAL)
                return (CurrentHeadingDeg, PitchDeg);

            // Negate Nx because FieldCamera3D.Project negates the horizontal (Rx) axis,
            // so the cube's visual X is reflected relative to the world X.
            double HeadingDeg = Math.Atan2(-Nx, Ny) * FieldConstants.DEGREES_PER_RADIAN / Math.PI;
            if (HeadingDeg < NEGATIVE_HEADING_THRESHOLD)
                HeadingDeg += FieldConstants.DEGREES_FULL_CIRCLE;

            return (HeadingDeg, PitchDeg);
        }

        /// <summary>
        /// Projects unit-cube vertices to widget-local screen coordinates.
        /// </summary>
        /// <param name="HeadingDeg">Current orbit heading in degrees.</param>
        /// <param name="PitchDeg">Current orbit pitch in degrees.</param>
        /// <param name="CentreX">Widget centre X in local pixels.</param>
        /// <param name="CentreY">Widget centre Y in local pixels.</param>
        /// <param name="Scale">Pixels per unit cube coordinate.</param>
        /// <returns>Projected vertices with screen position and depth.</returns>
        static private ProjectedPoint[] ProjectVertices
            (
            double HeadingDeg,
            double PitchDeg,
            float CentreX,
            float CentreY,
            float Scale
            )
        {
            ProjectedPoint[] Projected = new ProjectedPoint[UnitCubeVertices.Length];
            for (int I = 0; I < UnitCubeVertices.Length; I++)
            {
                (double X, double Y, double Z) Vertex = UnitCubeVertices[I];
                ApplyCameraTransform(HeadingDeg, PitchDeg, Vertex.X, Vertex.Y, Vertex.Z,
                    out double ViewX, out double ViewY, out double ViewZ);
                Projected[I] = new ProjectedPoint(
                    CentreX - ((float)ViewX * Scale),   // negate X to match FieldCamera3D.Project sign convention
                    CentreY - ((float)ViewY * Scale),
                    (float)ViewZ);
            }

            return Projected;
        }

        /// <summary>
        /// Applies the same heading+pitch camera transform as FieldCamera3D.Project so the
        /// cube appears exactly as it would sitting in the 3-D scene with one face on the ground
        /// and one face pointing north.
        /// ViewX = camera right, ViewY = camera up (screen up), ViewZ = depth into screen.
        /// A face whose normal has ViewZ > 0 is front-facing and visible.
        /// </summary>
        static private void ApplyCameraTransform
            (
            double HeadingDeg,
            double PitchDeg,
            double Vx,
            double Vy,
            double Vz,
            out double ViewX,
            out double ViewY,
            out double ViewZ
            )
        {
            double HeadingRad = HeadingDeg * Math.PI / FieldConstants.DEGREES_PER_RADIAN;
            double PitchRad = PitchDeg * Math.PI / FieldConstants.DEGREES_PER_RADIAN;
            double CosH = Math.Cos(HeadingRad);
            double SinH = Math.Sin(HeadingRad);
            double CosP = Math.Cos(PitchRad);
            double SinP = Math.Sin(PitchRad);

            double Rx = (Vx * CosH) - (Vy * SinH);
            double Fy = (Vx * SinH) + (Vy * CosH);

            ViewX = Rx;
            ViewY = (Vz * CosP) - (Fy * SinP);
            ViewZ = (Fy * CosP) + (Vz * SinP);
        }

        /// <summary>
        /// Returns camera-space Z sign for face culling (positive = front-facing).
        /// </summary>
        /// <param name="FaceIndex">Index into <see cref="FaceVertexIndices"/>.</param>
        /// <param name="HeadingDeg">Current orbit heading in degrees.</param>
        /// <param name="PitchDeg">Current orbit pitch in degrees.</param>
        /// <returns>Camera-space Z of the face normal after transform.</returns>
        static private double FaceDepthSign
            (
            int FaceIndex,
            double HeadingDeg,
            double PitchDeg
            )
        {
            (double X, double Y, double Z) Normal = FaceNormals[FaceIndex];
            ApplyCameraTransform(HeadingDeg, PitchDeg, Normal.X, Normal.Y, Normal.Z,
                out _, out _, out double ViewZ);
            return ViewZ;
        }

        /// <summary>
        /// Returns true when the hit target faces the camera and is drawn on the visible shell.
        /// </summary>
        static private bool IsPartVisible
            (
            ViewCubeHit Hit,
            double HeadingDeg,
            double PitchDeg
            )
        {
            if (!Hit.IsHit)
                return false;

            (double X, double Y, double Z) Normal = Hit.Kind switch
            {
                ViewCubePartKind.Face => FaceNormals[Hit.Index],
                ViewCubePartKind.Edge => AverageNormalsForEdge(Hit.Index),
                ViewCubePartKind.Vertex => AverageNormalsForVertex(Hit.Index),
                _ => (UNIT_NORMAL_X, UNIT_NORMAL_X, UNIT_NORMAL_X)
            };

            ApplyCameraTransform(HeadingDeg, PitchDeg, Normal.X, Normal.Y, Normal.Z,
                out _, out _, out double ViewZ);
            return ViewZ >= FACE_CULL_THRESHOLD;
        }

        /// <summary>
        /// Builds a closed SKPath for one projected quad face.
        /// </summary>
        /// <param name="Projected">Projected cube vertices.</param>
        /// <param name="A">First corner index.</param>
        /// <param name="B">Second corner index.</param>
        /// <param name="C">Third corner index.</param>
        /// <param name="D">Fourth corner index.</param>
        /// <returns>Closed path tracing the quad.</returns>
        static private SKPath BuildQuadPath
            (
            ProjectedPoint[] Projected,
            int A,
            int B,
            int C,
            int D
            )
        {
            SKPath Path = new SKPath();
            Path.MoveTo(Projected[A].X, Projected[A].Y);
            Path.LineTo(Projected[B].X, Projected[B].Y);
            Path.LineTo(Projected[C].X, Projected[C].Y);
            Path.LineTo(Projected[D].X, Projected[D].Y);
            Path.Close();
            return Path;
        }

        /// <summary>
        /// Draws an inset highlight quad on a hovered cube face.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Projected">Projected cube vertices.</param>
        /// <param name="Face">Corner indices for the hovered face.</param>
        /// <param name="HighlightPaint">Fill paint for the highlight.</param>
        static private void DrawFaceHighlight
            (
            SKCanvas Canvas,
            ProjectedPoint[] Projected,
            (int A, int B, int C, int D) Face,
            SKPaint HighlightPaint
            )
        {
            ProjectedPoint Centre = AveragePoint(Projected, Face.A, Face.B, Face.C, Face.D);
            using SKPath Highlight = new SKPath();
            foreach (int Index in new[] { Face.A, Face.B, Face.C, Face.D })
            {
                ProjectedPoint Corner = Projected[Index];
                float X = Centre.X + ((Corner.X - Centre.X) * HIGHLIGHT_INSET);
                float Y = Centre.Y + ((Corner.Y - Centre.Y) * HIGHLIGHT_INSET);
                if (Highlight.PointCount == 0)
                    Highlight.MoveTo(X, Y);
                else
                    Highlight.LineTo(X, Y);
            }

            Highlight.Close();
            Canvas.DrawPath(Highlight, HighlightPaint);
        }

        /// <summary>
        /// Draws a pill-shaped highlight along a hovered cube edge.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Projected">Projected cube vertices.</param>
        /// <param name="EdgeIndex">Index into <see cref="EdgeVertexPairs"/>.</param>
        /// <param name="HighlightPaint">Fill paint for the highlight.</param>
        static private void DrawEdgeHighlight
            (
            SKCanvas Canvas,
            ProjectedPoint[] Projected,
            int EdgeIndex,
            SKPaint HighlightPaint
            )
        {
            (int A, int B) Edge = EdgeVertexPairs[EdgeIndex];
            ProjectedPoint P0 = Projected[Edge.A];
            ProjectedPoint P1 = Projected[Edge.B];
            float Dx = P1.X - P0.X;
            float Dy = P1.Y - P0.Y;
            float Length = (float)Math.Sqrt((Dx * Dx) + (Dy * Dy));
            if (Length <= MIN_EDGE_LENGTH)
                return;

            float Nx = -Dy / Length;
            float Ny = Dx / Length;
            float HalfWidth = EDGE_HIGHLIGHT_HALF_WIDTH;
            float HalfLength = Math.Min(Length * EDGE_HIGHLIGHT_LENGTH_FRACTION, EDGE_HIGHLIGHT_MAX_HALF_LENGTH);
            float MidX = (P0.X + P1.X) * EDGE_MIDPOINT_FRACTION;
            float MidY = (P0.Y + P1.Y) * EDGE_MIDPOINT_FRACTION;
            float Tx = (Dx / Length) * HalfLength;
            float Ty = (Dy / Length) * HalfLength;

            using SKPath Patch = new SKPath();
            Patch.MoveTo(MidX - Tx + (Nx * HalfWidth), MidY - Ty + (Ny * HalfWidth));
            Patch.LineTo(MidX + Tx + (Nx * HalfWidth), MidY + Ty + (Ny * HalfWidth));
            Patch.LineTo(MidX + Tx - (Nx * HalfWidth), MidY + Ty - (Ny * HalfWidth));
            Patch.LineTo(MidX - Tx - (Nx * HalfWidth), MidY - Ty - (Ny * HalfWidth));
            Patch.Close();
            Canvas.DrawPath(Patch, HighlightPaint);
        }

        /// <summary>
        /// Draws a square highlight on a hovered cube vertex.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Projected">Projected cube vertices.</param>
        /// <param name="VertexIndex">Index into <see cref="UnitCubeVertices"/>.</param>
        /// <param name="HighlightPaint">Fill paint for the highlight.</param>
        /// <param name="EdgePaint">Stroke paint for the highlight outline.</param>
        static private void DrawVertexHighlight
            (
            SKCanvas Canvas,
            ProjectedPoint[] Projected,
            int VertexIndex,
            SKPaint HighlightPaint,
            SKPaint EdgePaint
            )
        {
            ProjectedPoint Corner = Projected[VertexIndex];
            float Size = VERTEX_HIGHLIGHT_SIZE;
            SKRect Rect = SKRect.Create(Corner.X - (Size * EDGE_MIDPOINT_FRACTION), Corner.Y - (Size * EDGE_MIDPOINT_FRACTION), Size, Size);
            Canvas.DrawRect(Rect, HighlightPaint);
            Canvas.DrawRect(Rect, EdgePaint);
        }

        /// <summary>
        /// Averages outward normals of the two faces sharing an edge.
        /// </summary>
        /// <param name="EdgeIndex">Index into <see cref="EdgeFacePairs"/>.</param>
        /// <returns>Unit normal pointing toward the edge midpoint.</returns>
        static private (double X, double Y, double Z) AverageNormalsForEdge
            (
            int EdgeIndex
            )
        {
            (int FaceA, int FaceB) = EdgeFacePairs[EdgeIndex];
            return Normalize(
                FaceNormals[FaceA].X + FaceNormals[FaceB].X,
                FaceNormals[FaceA].Y + FaceNormals[FaceB].Y,
                FaceNormals[FaceA].Z + FaceNormals[FaceB].Z);
        }

        /// <summary>
        /// Averages outward normals of all faces meeting at a vertex.
        /// </summary>
        /// <param name="VertexIndex">Index into <see cref="VertexFaceIndices"/>.</param>
        /// <returns>Unit normal pointing toward the vertex corner.</returns>
        static private (double X, double Y, double Z) AverageNormalsForVertex
            (
            int VertexIndex
            )
        {
            int[] FacesForVertex = VertexFaceIndices[VertexIndex];

            double X = 0;
            double Y = 0;
            double Z = 0;
            foreach (int FaceIndex in FacesForVertex)
            {
                X += FaceNormals[FaceIndex].X;
                Y += FaceNormals[FaceIndex].Y;
                Z += FaceNormals[FaceIndex].Z;
            }

            return Normalize(X, Y, Z);
        }

        /// <summary>
        /// Normalizes a 3D vector, falling back to a default forward direction when length is zero.
        /// </summary>
        /// <param name="X">X component.</param>
        /// <param name="Y">Y component.</param>
        /// <param name="Z">Z component.</param>
        /// <returns>Unit normal, or default when input is degenerate.</returns>
        static private (double X, double Y, double Z) Normalize
            (
            double X,
            double Y,
            double Z
            )
        {
            double Length = Math.Sqrt((X * X) + (Y * Y) + (Z * Z));
            if (Length <= MIN_NORMAL_LENGTH)
                return (UNIT_NORMAL_X, DEFAULT_FORWARD_NORMAL_Y, UNIT_NORMAL_X);
            return (X / Length, Y / Length, Z / Length);
        }

        /// <summary>
        /// Computes the centroid of four projected points (quarter-sum average).
        /// </summary>
        /// <param name="Points">Projected vertex array.</param>
        /// <param name="A">First corner index.</param>
        /// <param name="B">Second corner index.</param>
        /// <param name="C">Third corner index.</param>
        /// <param name="D">Fourth corner index.</param>
        /// <returns>Average screen position and depth of the four corners.</returns>
        static private ProjectedPoint AveragePoint
            (
            ProjectedPoint[] Points,
            int A,
            int B,
            int C,
            int D
            )
        {
            return new ProjectedPoint(
                (Points[A].X + Points[B].X + Points[C].X + Points[D].X) * FACE_DEPTH_AVERAGE_FRACTION,
                (Points[A].Y + Points[B].Y + Points[C].Y + Points[D].Y) * FACE_DEPTH_AVERAGE_FRACTION,
                (Points[A].Z + Points[B].Z + Points[C].Z + Points[D].Z) * FACE_DEPTH_AVERAGE_FRACTION);
        }

        /// <summary>
        /// Tests whether a screen point lies inside a projected quad (two triangle test).
        /// </summary>
        /// <param name="Projected">Projected cube vertices.</param>
        /// <param name="A">First corner index.</param>
        /// <param name="B">Second corner index.</param>
        /// <param name="C">Third corner index.</param>
        /// <param name="D">Fourth corner index.</param>
        /// <param name="X">Screen X to test.</param>
        /// <param name="Y">Screen Y to test.</param>
        /// <returns>True when the point is inside either triangle of the quad.</returns>
        static private bool PointInQuad
            (
            ProjectedPoint[] Projected,
            int A,
            int B,
            int C,
            int D,
            float X,
            float Y
            )
        {
            return PointInTriangle(Projected[A], Projected[B], Projected[C], X, Y)
                || PointInTriangle(Projected[A], Projected[C], Projected[D], X, Y);
        }

        /// <summary>
        /// Tests whether a screen point lies inside a projected triangle using barycentric coordinates.
        /// </summary>
        /// <param name="A">First triangle corner.</param>
        /// <param name="B">Second triangle corner.</param>
        /// <param name="C">Third triangle corner.</param>
        /// <param name="X">Screen X to test.</param>
        /// <param name="Y">Screen Y to test.</param>
        /// <returns>True when barycentric weights are all non-negative.</returns>
        static private bool PointInTriangle
            (
            ProjectedPoint A,
            ProjectedPoint B,
            ProjectedPoint C,
            float X,
            float Y
            )
        {
            float Denom = ((B.Y - C.Y) * (A.X - C.X)) + ((C.X - B.X) * (A.Y - C.Y));
            if (Math.Abs(Denom) <= MIN_TRIANGLE_DENOMINATOR)
                return false;

            float Alpha = (((B.Y - C.Y) * (X - C.X)) + ((C.X - B.X) * (Y - C.Y))) / Denom;
            float Beta = (((C.Y - A.Y) * (X - C.X)) + ((A.X - C.X) * (Y - C.Y))) / Denom;
            float Gamma = BARYCENTRIC_WEIGHT_SUM - Alpha - Beta;
            return (Alpha >= BARYCENTRIC_MIN) && (Beta >= BARYCENTRIC_MIN) && (Gamma >= BARYCENTRIC_MIN);
        }

        /// <summary>
        /// Euclidean distance from a projected point to a screen coordinate.
        /// </summary>
        /// <param name="Point">Projected point.</param>
        /// <param name="X">Screen X.</param>
        /// <param name="Y">Screen Y.</param>
        /// <returns>Distance in pixels.</returns>
        static private float Distance
            (
            ProjectedPoint Point,
            float X,
            float Y
            )
        {
            float Dx = Point.X - X;
            float Dy = Point.Y - Y;
            return (float)Math.Sqrt((Dx * Dx) + (Dy * Dy));
        }

        /// <summary>
        /// Shortest distance from a screen point to a projected line segment.
        /// </summary>
        /// <param name="A">Segment start.</param>
        /// <param name="B">Segment end.</param>
        /// <param name="X">Screen X.</param>
        /// <param name="Y">Screen Y.</param>
        /// <returns>Distance in pixels to the closest point on the segment.</returns>
        static private float DistanceToSegment
            (
            ProjectedPoint A,
            ProjectedPoint B,
            float X,
            float Y
            )
        {
            float Dx = B.X - A.X;
            float Dy = B.Y - A.Y;
            float LengthSq = (Dx * Dx) + (Dy * Dy);
            if (LengthSq <= MIN_SEGMENT_LENGTH_SQ)
                return Distance(A, X, Y);

            float T = Math.Clamp((((X - A.X) * Dx) + ((Y - A.Y) * Dy)) / LengthSq, SEGMENT_PARAMETER_MIN, SEGMENT_PARAMETER_MAX);
            float Px = A.X + (T * Dx);
            float Py = A.Y + (T * Dy);
            float DistX = X - Px;
            float DistY = Y - Py;
            return (float)Math.Sqrt((DistX * DistX) + (DistY * DistY));
        }
    }
}

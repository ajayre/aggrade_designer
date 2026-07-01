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
        public ViewCubeHit
            (
            ViewCubePartKind Kind,
            int Index
            )
        {
            this.Kind = Kind;
            this.Index = Index;
        }

        public ViewCubePartKind Kind { get; }
        public int Index { get; }
        public bool IsHit => Kind != ViewCubePartKind.None;

        static public ViewCubeHit None => new ViewCubeHit(ViewCubePartKind.None, -1);
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

            public float X { get; }
            public float Y { get; }
            public float Z { get; }
        }

        // Match ViewCompassRenderer overlay inset from the viewport edge.
        private const float CUBE_MARGIN = 14f;
        // Radius of the drawn cube; compass dial uses 52 px diameter (26 px radius).
        private const float CUBE_CONTENT_RADIUS = 26f;
        private const float CUBE_INTERACTION_PAD = 10f;
        private const float CUBE_WIDGET_SIZE = (CUBE_CONTENT_RADIUS + CUBE_INTERACTION_PAD) * 2f;
        // Unit-cube vertices are at most sqrt(3)/2 from the origin.
        private const float CUBE_SCALE_FACTOR = CUBE_CONTENT_RADIUS / (0.866f * CUBE_WIDGET_SIZE);
        // Faces with normal ViewZ >= this threshold are drawn; tolerance handles edge-on faces.
        private const double FACE_CULL_THRESHOLD = -0.05;
        private const float HIGHLIGHT_INSET = 0.62f;
        private const float EDGE_HIT_RADIUS = 9f;
        private const float VERTEX_HIT_RADIUS = 10f;

        private static readonly SKColor FACE_FILL = new SKColor(232, 242, 252, 210);
        private static readonly SKColor HIGHLIGHT_FILL = new SKColor(0, 140, 230, 235);
        private static readonly SKColor EDGE_COLOR = new SKColor(0, 114, 198);

        private static readonly (int A, int B, int C, int D)[] FaceVertexIndices =
        {
            (4, 5, 6, 7),
            (0, 3, 2, 1),
            (0, 4, 5, 1),
            (3, 7, 6, 2),
            (1, 5, 6, 2),
            (4, 0, 3, 7)
        };

        private static readonly (double X, double Y, double Z)[] FaceNormals =
        {
            (0, 0, 1),
            (0, 0, -1),
            (0, -1, 0),
            (0, 1, 0),
            (1, 0, 0),
            (-1, 0, 0)
        };

        private static readonly (int A, int B)[] EdgeVertexPairs =
        {
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7)
        };

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

        private static readonly (double X, double Y, double Z)[] UnitCubeVertices =
        {
            (-0.5, -0.5, -0.5),
            (0.5, -0.5, -0.5),
            (0.5, 0.5, -0.5),
            (-0.5, 0.5, -0.5),
            (-0.5, -0.5, 0.5),
            (0.5, -0.5, 0.5),
            (0.5, 0.5, 0.5),
            (-0.5, 0.5, 0.5)
        };

        /// <summary>
        /// Returns the screen bounds occupied by the view cube widget.
        /// </summary>
        static internal SKRect GetBounds
            (
            SKRect Viewport
            )
        {
            float CentreX = Viewport.Left + CUBE_MARGIN + CUBE_CONTENT_RADIUS;
            float CentreY = Viewport.Top + CUBE_MARGIN + CUBE_CONTENT_RADIUS;
            float HalfWidget = CUBE_WIDGET_SIZE * 0.5f;
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
                double Depth = (Projected[Face.A].Z + Projected[Face.B].Z + Projected[Face.C].Z + Projected[Face.D].Z) * 0.25;
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
                StrokeWidth = 1.25f,
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

            SKRect LocalWidget = SKRect.Create(0, 0, WidgetBounds.Width, WidgetBounds.Height);
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
                double Depth = (Projected[Face.A].Z + Projected[Face.B].Z + Projected[Face.C].Z + Projected[Face.D].Z) * 0.25;
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
                return (CurrentHeadingDeg, 30.0);

            (double X, double Y, double Z) Normal = Hit.Kind switch
            {
                ViewCubePartKind.Face => FaceNormals[Hit.Index],
                ViewCubePartKind.Edge => AverageNormalsForEdge(Hit.Index),
                ViewCubePartKind.Vertex => AverageNormalsForVertex(Hit.Index),
                _ => (0, -1, 0)
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
            if (Length <= 1e-9)
                return (CurrentHeadingDeg, 30.0);

            double Nx = Normal.X / Length;
            double Ny = Normal.Y / Length;
            double Nz = Normal.Z / Length;

            double Horizontal = Math.Sqrt((Nx * Nx) + (Ny * Ny));
            double PitchDeg = Math.Atan2(Nz, Horizontal) * 180.0 / Math.PI;
            PitchDeg = Math.Clamp(PitchDeg, FieldCamera3D.MinimumOrbitPitchDeg, 89.0);

            if (Horizontal <= 1e-6)
                return (CurrentHeadingDeg, PitchDeg);

            // Negate Nx because FieldCamera3D.Project negates the horizontal (Rx) axis,
            // so the cube's visual X is reflected relative to the world X.
            double HeadingDeg = Math.Atan2(-Nx, Ny) * 180.0 / Math.PI;
            if (HeadingDeg < 0)
                HeadingDeg += 360.0;

            return (HeadingDeg, PitchDeg);
        }

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
            double HeadingRad = HeadingDeg * Math.PI / 180.0;
            double PitchRad = PitchDeg * Math.PI / 180.0;
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
                _ => (0, 0, 0)
            };

            ApplyCameraTransform(HeadingDeg, PitchDeg, Normal.X, Normal.Y, Normal.Z,
                out _, out _, out double ViewZ);
            return ViewZ >= FACE_CULL_THRESHOLD;
        }

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
            if (Length <= 1e-3f)
                return;

            float Nx = -Dy / Length;
            float Ny = Dx / Length;
            float HalfWidth = 7f;
            float HalfLength = Math.Min(Length * 0.34f, 16f);
            float MidX = (P0.X + P1.X) * 0.5f;
            float MidY = (P0.Y + P1.Y) * 0.5f;
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
            float Size = 11f;
            SKRect Rect = SKRect.Create(Corner.X - (Size * 0.5f), Corner.Y - (Size * 0.5f), Size, Size);
            Canvas.DrawRect(Rect, HighlightPaint);
            Canvas.DrawRect(Rect, EdgePaint);
        }

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

        static private (double X, double Y, double Z) Normalize
            (
            double X,
            double Y,
            double Z
            )
        {
            double Length = Math.Sqrt((X * X) + (Y * Y) + (Z * Z));
            if (Length <= 1e-9)
                return (0, -1, 0);
            return (X / Length, Y / Length, Z / Length);
        }

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
                (Points[A].X + Points[B].X + Points[C].X + Points[D].X) * 0.25f,
                (Points[A].Y + Points[B].Y + Points[C].Y + Points[D].Y) * 0.25f,
                (Points[A].Z + Points[B].Z + Points[C].Z + Points[D].Z) * 0.25f);
        }

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
            if (Math.Abs(Denom) <= 1e-6f)
                return false;

            float Alpha = (((B.Y - C.Y) * (X - C.X)) + ((C.X - B.X) * (Y - C.Y))) / Denom;
            float Beta = (((C.Y - A.Y) * (X - C.X)) + ((A.X - C.X) * (Y - C.Y))) / Denom;
            float Gamma = 1f - Alpha - Beta;
            return (Alpha >= 0f) && (Beta >= 0f) && (Gamma >= 0f);
        }

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
            if (LengthSq <= 1e-6f)
                return Distance(A, X, Y);

            float T = Math.Clamp((((X - A.X) * Dx) + ((Y - A.Y) * Dy)) / LengthSq, 0f, 1f);
            float Px = A.X + (T * Dx);
            float Py = A.Y + (T * Dy);
            float DistX = X - Px;
            float DistY = Y - Py;
            return (float)Math.Sqrt((DistX * DistX) + (DistY * DistY));
        }
    }
}

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Sortable triangle record for painter-ordered 3D mesh rendering and depth-buffer builds.
    /// </summary>
    internal sealed class MeshTriangle
    {
        /// <summary>First vertex screen position.</summary>
        public required SKPoint P0 { get; init; }

        /// <summary>Second vertex screen position.</summary>
        public required SKPoint P1 { get; init; }

        /// <summary>Third vertex screen position.</summary>
        public required SKPoint P2 { get; init; }

        /// <summary>Fill colour at the first vertex.</summary>
        public required SKColor C0 { get; init; }

        /// <summary>Fill colour at the second vertex.</summary>
        public required SKColor C1 { get; init; }

        /// <summary>Fill colour at the third vertex.</summary>
        public required SKColor C2 { get; init; }

        /// <summary>Camera depth at the first vertex.</summary>
        public required double D0 { get; init; }

        /// <summary>Camera depth at the second vertex.</summary>
        public required double D1 { get; init; }

        /// <summary>Camera depth at the third vertex.</summary>
        public required double D2 { get; init; }

        /// <summary>Camera-space depth used for painter sorting.</summary>
        public required double Depth { get; init; }
    }

    /// <summary>
    /// Per-pixel camera depth of the rendered topo mesh; larger values are closer to the camera.
    /// Stored at reduced resolution to keep orbit interaction responsive on large meshes.
    /// </summary>
    internal sealed class MeshDepthBuffer
    {
        /// <summary>Screen-space divisor for reduced-resolution depth buffer storage.</summary>
        private const int DEPTH_BUFFER_RESOLUTION_SCALE = 2;

        /// <summary>Depth tolerance when comparing overlay samples against stored mesh depth.</summary>
        private const float DEPTH_VISIBLE_EPSILON = 0.05f;

        /// <summary>Minimum depth buffer width or height in reduced-resolution pixels.</summary>
        private const int DEPTH_BUFFER_MIN_DIMENSION = 1;

        /// <summary>Minimum triangle area for depth-buffer rasterization.</summary>
        private const double TRIANGLE_RASTERIZE_AREA_EPSILON = 1e-6;

        /// <summary>Singleton passthrough buffer that skips depth testing.</summary>
        private static readonly MeshDepthBuffer AlwaysVisibleInstance = new MeshDepthBuffer();

        /// <summary>Depth lookup that treats every overlay sample as visible.</summary>
        public static MeshDepthBuffer AlwaysVisible => AlwaysVisibleInstance;

        /// <summary>True when depth testing is disabled and all overlay samples are visible.</summary>
        public bool IsPassthrough => _Depth == null;

        /// <summary>Reduced-resolution depth values; null for passthrough instance.</summary>
        private readonly float[]? _Depth;

        /// <summary>Depth buffer width in reduced-resolution pixels.</summary>
        private readonly int _Width;

        /// <summary>Depth buffer height in reduced-resolution pixels.</summary>
        private readonly int _Height;

        /// <summary>Creates a passthrough depth buffer with no stored depths.</summary>
        private MeshDepthBuffer()
        {
        }

        /// <summary>
        /// Builds a depth buffer by rasterizing mesh triangles at reduced resolution.
        /// </summary>
        /// <param name="Viewport">Target viewport rectangle.</param>
        /// <param name="Triangles">Mesh triangles with per-vertex depths.</param>
        /// <returns>Depth buffer for overlay visibility testing.</returns>
        public static MeshDepthBuffer Build(SKRect Viewport, IReadOnlyList<MeshTriangle> Triangles)
        {
            MeshDepthBuffer Buffer = new MeshDepthBuffer(Viewport);
            if (Triangles.Count == 0)
                return Buffer;

            float[] Depth = Buffer._Depth!;
            foreach (MeshTriangle Triangle in Triangles)
            {
                Buffer.RasterizeTriangleInto(
                    Depth,
                    Triangle.P0.X,
                    Triangle.P0.Y,
                    Triangle.D0,
                    Triangle.P1.X,
                    Triangle.P1.Y,
                    Triangle.D1,
                    Triangle.P2.X,
                    Triangle.P2.Y,
                    Triangle.D2);
            }

            return Buffer;
        }

        /// <summary>
        /// Allocates a reduced-resolution depth buffer for the given viewport.
        /// </summary>
        /// <param name="Viewport">Target viewport rectangle.</param>
        private MeshDepthBuffer(SKRect Viewport)
        {
            _Width = Math.Max(DEPTH_BUFFER_MIN_DIMENSION, (int)Math.Ceiling(Viewport.Width / DEPTH_BUFFER_RESOLUTION_SCALE));
            _Height = Math.Max(DEPTH_BUFFER_MIN_DIMENSION, (int)Math.Ceiling(Viewport.Height / DEPTH_BUFFER_RESOLUTION_SCALE));
            _Depth = new float[_Width * _Height];
            Array.Fill(_Depth, float.NegativeInfinity);
        }

        /// <summary>
        /// Returns whether a screen point is visible at the given depth against the topo mesh.
        /// </summary>
        /// <param name="ScreenX">Screen X in pixels.</param>
        /// <param name="ScreenY">Screen Y in pixels.</param>
        /// <param name="Depth">Camera depth of the overlay sample.</param>
        /// <returns>True when the sample is not occluded by the mesh.</returns>
        public bool IsVisible(float ScreenX, float ScreenY, double Depth)
        {
            if (_Depth == null)
                return true;

            int Px = (int)Math.Round(ScreenX / DEPTH_BUFFER_RESOLUTION_SCALE);
            int Py = (int)Math.Round(ScreenY / DEPTH_BUFFER_RESOLUTION_SCALE);
            if (Px < 0 || Py < 0 || Px >= _Width || Py >= _Height)
                return false;

            float Stored = _Depth[(Py * _Width) + Px];
            if (float.IsNegativeInfinity(Stored))
                return true;

            return Depth >= Stored - DEPTH_VISIBLE_EPSILON;
        }

        /// <summary>
        /// Rasterizes one triangle into the depth buffer using barycentric interpolation.
        /// </summary>
        private void RasterizeTriangleInto
            (
            float[] Depth,
            float X0,
            float Y0,
            double D0,
            float X1,
            float Y1,
            double D1,
            float X2,
            float Y2,
            double D2
            )
        {
            X0 /= DEPTH_BUFFER_RESOLUTION_SCALE;
            Y0 /= DEPTH_BUFFER_RESOLUTION_SCALE;
            X1 /= DEPTH_BUFFER_RESOLUTION_SCALE;
            Y1 /= DEPTH_BUFFER_RESOLUTION_SCALE;
            X2 /= DEPTH_BUFFER_RESOLUTION_SCALE;
            Y2 /= DEPTH_BUFFER_RESOLUTION_SCALE;

            double Area2 = TriangleEdge(X0, Y0, X1, Y1, X2, Y2);
            if (Math.Abs(Area2) < TRIANGLE_RASTERIZE_AREA_EPSILON)
                return;

            int MinX = (int)Math.Floor(Math.Min(X0, Math.Min(X1, X2)));
            int MaxX = (int)Math.Ceiling(Math.Max(X0, Math.Max(X1, X2)));
            int MinY = (int)Math.Floor(Math.Min(Y0, Math.Min(Y1, Y2)));
            int MaxY = (int)Math.Ceiling(Math.Max(Y0, Math.Max(Y1, Y2)));
            if (MaxX < 0 || MinX >= _Width || MaxY < 0 || MinY >= _Height)
                return;

            MinX = Math.Clamp(MinX, 0, _Width - 1);
            MaxX = Math.Clamp(MaxX, 0, _Width - 1);
            MinY = Math.Clamp(MinY, 0, _Height - 1);
            MaxY = Math.Clamp(MaxY, 0, _Height - 1);

            for (int Py = MinY; Py <= MaxY; Py++)
            {
                int RowBase = Py * _Width;
                for (int Px = MinX; Px <= MaxX; Px++)
                {
                    double W0 = TriangleEdge(X1, Y1, X2, Y2, Px, Py) / Area2;
                    if (W0 < 0)
                        continue;

                    double W1 = TriangleEdge(X2, Y2, X0, Y0, Px, Py) / Area2;
                    if (W1 < 0)
                        continue;

                    double W2 = TriangleEdge(X0, Y0, X1, Y1, Px, Py) / Area2;
                    if (W2 < 0)
                        continue;

                    float TriangleDepth = (float)((W0 * D0) + (W1 * D1) + (W2 * D2));
                    int Idx = RowBase + Px;
                    float Stored = Depth[Idx];
                    if (TriangleDepth > Stored)
                        Depth[Idx] = TriangleDepth;
                }
            }
        }

        /// <summary>
        /// Signed doubled area of triangle ABC (positive = counter-clockwise).
        /// </summary>
        private static double TriangleEdge
            (
            double Ax,
            double Ay,
            double Bx,
            double By,
            double Cx,
            double Cy
            )
        {
            return ((Cx - Ax) * (By - Ay)) - ((Cy - Ay) * (Bx - Ax));
        }
    }
}

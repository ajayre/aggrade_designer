using AgGradeDesigner.Core.Geo;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Draws 3D overlay strokes with topo mesh depth testing.
    /// </summary>
    internal static class TopoDepthOverlayDrawer
    {
        /// <summary>Pixel spacing between depth samples along a 3D line segment.</summary>
        private const double DEPTH_SEGMENT_SAMPLE_SPACING = 3.0;

        /// <summary>Minimum depth sample steps along a segment.</summary>
        private const int DEPTH_SEGMENT_MIN_STEPS = 2;

        /// <summary>Maximum depth sample steps along a segment.</summary>
        private const int DEPTH_SEGMENT_MAX_STEPS = 64;

        /// <summary>Distance below which consecutive depth samples are merged.</summary>
        private const float DEPTH_SAMPLE_JOIN_EPSILON = 1e-3f;

        /// <summary>
        /// Draws a 3D line only where it is not hidden behind closer mesh surface.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="X0">First endpoint screen X.</param>
        /// <param name="Y0">First endpoint screen Y.</param>
        /// <param name="D0">First endpoint camera depth.</param>
        /// <param name="X1">Second endpoint screen X.</param>
        /// <param name="Y1">Second endpoint screen Y.</param>
        /// <param name="D1">Second endpoint camera depth.</param>
        /// <param name="TopoDepth">Topo mesh depth buffer.</param>
        /// <param name="Paint">Stroke paint.</param>
        public static void DrawDepthTestedLine3D
            (
            SKCanvas Canvas,
            float X0,
            float Y0,
            double D0,
            float X1,
            float Y1,
            double D1,
            MeshDepthBuffer TopoDepth,
            SKPaint Paint
            )
        {
            DrawDepthTestedPolyline3D(
                Canvas,
                new List<(float X, float Y, double Depth)> { (X0, Y0, D0), (X1, Y1, D1) },
                TopoDepth,
                Paint,
                Closed: false);
        }

        /// <summary>
        /// Draws screen-space polyline segments that pass the topo depth test.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Vertices">Screen vertices with camera depth.</param>
        /// <param name="TopoDepth">Topo mesh depth buffer.</param>
        /// <param name="Paint">Stroke paint.</param>
        /// <param name="Closed">True when the polyline closes on the first vertex.</param>
        public static void DrawDepthTestedPolyline3D
            (
            SKCanvas Canvas,
            IReadOnlyList<(float X, float Y, double Depth)> Vertices,
            MeshDepthBuffer TopoDepth,
            SKPaint Paint,
            bool Closed
            )
        {
            if (Vertices.Count < GeoConstants.MIN_POLYLINE_VERTICES)
                return;

            if (TopoDepth.IsPassthrough)
            {
                using SKPath Path = new SKPath();
                Path.MoveTo(Vertices[0].X, Vertices[0].Y);
                for (int I = 1; I < Vertices.Count; I++)
                    Path.LineTo(Vertices[I].X, Vertices[I].Y);
                if (Closed)
                    Path.Close();
                Canvas.DrawPath(Path, Paint);
                return;
            }

            List<(float X, float Y, double Depth)> Samples = new List<(float X, float Y, double Depth)>();
            int SegmentCount = Closed ? Vertices.Count : Vertices.Count - 1;
            for (int I = 0; I < SegmentCount; I++)
            {
                (float X0, float Y0, double D0) = Vertices[I];
                (float X1, float Y1, double D1) = Vertices[(I + 1) % Vertices.Count];
                AppendDepthTestedSegmentSamples(Samples, X0, Y0, D0, X1, Y1, D1);
            }

            foreach (IReadOnlyList<(float X, float Y)> Run in BuildVisibleScreenRuns(Samples, TopoDepth))
            {
                if (Run.Count < GeoConstants.MIN_POLYLINE_VERTICES)
                    continue;

                using SKPath Path = new SKPath();
                Path.MoveTo(Run[0].X, Run[0].Y);
                for (int I = 1; I < Run.Count; I++)
                    Path.LineTo(Run[I].X, Run[I].Y);
                Canvas.DrawPath(Path, Paint);
            }
        }

        /// <summary>
        /// Subdivides a 3D line segment into depth samples for visibility testing.
        /// </summary>
        private static void AppendDepthTestedSegmentSamples
            (
            List<(float X, float Y, double Depth)> Samples,
            float X0,
            float Y0,
            double D0,
            float X1,
            float Y1,
            double D1
            )
        {
            double Length = Math.Sqrt(((X1 - X0) * (X1 - X0)) + ((Y1 - Y0) * (Y1 - Y0)));
            int Steps = Math.Clamp((int)Math.Ceiling(Length / DEPTH_SEGMENT_SAMPLE_SPACING), DEPTH_SEGMENT_MIN_STEPS, DEPTH_SEGMENT_MAX_STEPS);
            if (Samples.Count > 0)
            {
                (float LastX, float LastY, double LastD) = Samples[^1];
                if (Math.Abs(LastX - X0) < DEPTH_SAMPLE_JOIN_EPSILON && Math.Abs(LastY - Y0) < DEPTH_SAMPLE_JOIN_EPSILON)
                    Samples[^1] = (X0, Y0, D0);
                else
                    Samples.Add((X0, Y0, D0));
            }
            else
            {
                Samples.Add((X0, Y0, D0));
            }

            for (int Step = 1; Step < Steps; Step++)
            {
                double T = (double)Step / Steps;
                Samples.Add((
                    (float)(X0 + ((X1 - X0) * T)),
                    (float)(Y0 + ((Y1 - Y0) * T)),
                    D0 + ((D1 - D0) * T)));
            }

            Samples.Add((X1, Y1, D1));
        }

        /// <summary>
        /// Splits depth-tested screen samples into contiguous visible polyline runs.
        /// </summary>
        private static List<List<(float X, float Y)>> BuildVisibleScreenRuns
            (
            IReadOnlyList<(float X, float Y, double Depth)> Samples,
            MeshDepthBuffer TopoDepth
            )
        {
            List<List<(float X, float Y)>> Runs = new List<List<(float X, float Y)>>();
            List<(float X, float Y)>? Current = null;

            foreach ((float X, float Y, double Depth) in Samples)
            {
                if (TopoDepth.IsVisible(X, Y, Depth))
                {
                    Current ??= new List<(float X, float Y)>();
                    Current.Add((X, Y));
                    continue;
                }

                if (Current != null && Current.Count >= GeoConstants.MIN_POLYLINE_VERTICES)
                    Runs.Add(Current);
                Current = null;
            }

            if (Current != null && Current.Count >= GeoConstants.MIN_POLYLINE_VERTICES)
                Runs.Add(Current);

            return Runs;
        }
    }
}

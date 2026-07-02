using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Rendering.Vulkan;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Ensures GPU camera uniforms match the CPU orbit projection.
    /// </summary>

    public sealed class FieldCameraProjectionTests
    {
        /// <summary>
        /// Sample heading deg used by this component.
        /// </summary>

        private const double SAMPLE_HEADING_DEG = 45;
        /// <summary>
        /// Screen height px used by this component.
        /// </summary>

        private const int SCREEN_HEIGHT_PX = 720;
        /// <summary>
        /// Ndc top used by this component.
        /// </summary>

        private const float NDC_TOP = -1.0f;
        /// <summary>
        /// Ndc bottom used by this component.
        /// </summary>

        private const float NDC_BOTTOM = 1.0f;
        /// <summary>
        /// Ndc precision used by this component.
        /// </summary>

        private const int NDC_PRECISION = 3;
        /// <summary>
        /// Near camera depth used by this component.
        /// </summary>

        private const double NEAR_CAMERA_DEPTH = 900;
        /// <summary>
        /// Far camera depth used by this component.
        /// </summary>

        private const double FAR_CAMERA_DEPTH = 200;
        /// <summary>
        /// Depth range used by this component.
        /// </summary>

        private const double DEPTH_RANGE = 1000;
        /// <summary>
        /// Depth margin used by this component.
        /// </summary>

        private const double DEPTH_MARGIN = 100;
        /// <summary>
        /// Render width px used by this component.
        /// </summary>

        private const int RENDER_WIDTH_PX = 640;
        /// <summary>
        /// Render height px used by this component.
        /// </summary>

        private const int RENDER_HEIGHT_PX = 480;
        /// <summary>
        /// Render scale used by this component.
        /// </summary>

        private const float RENDER_SCALE = 1.5f;
        /// <summary>
        /// Trig cos used by this component.
        /// </summary>

        private const float TRIG_COS = 0.8660254f;
        /// <summary>
        /// Trig sin used by this component.
        /// </summary>

        private const float TRIG_SIN = 0.5f;
        /// <summary>
        /// Triangle east min used by this component.
        /// </summary>

        private const float TRIANGLE_EAST_MIN = -100;
        /// <summary>
        /// Triangle east max used by this component.
        /// </summary>

        private const float TRIANGLE_EAST_MAX = 100;
        /// <summary>
        /// Triangle north min used by this component.
        /// </summary>

        private const float TRIANGLE_NORTH_MIN = -100;
        /// <summary>
        /// Triangle north max used by this component.
        /// </summary>

        private const float TRIANGLE_NORTH_MAX = 100;
        /// <summary>
        /// Viewport width px used by this component.
        /// </summary>

        private const int VIEWPORT_WIDTH_PX = 1280;
        /// <summary>
        /// Viewport height px used by this component.
        /// </summary>

        private const int VIEWPORT_HEIGHT_PX = 720;
        /// <summary>
        /// Viewport mid x used by this component.
        /// </summary>

        private const double VIEWPORT_MID_X = 640;
        /// <summary>
        /// Viewport mid y used by this component.
        /// </summary>

        private const double VIEWPORT_MID_Y = 360;
        /// <summary>
        /// Projection scale used by this component.
        /// </summary>

        private const double PROJECTION_SCALE = 0.9;
        /// <summary>
        /// Pitch deg used by this component.
        /// </summary>

        private const double PITCH_DEG = 25;
        /// <summary>
        /// Centroid tolerance px used by this component.
        /// </summary>

        private const int CENTROID_TOLERANCE_PX = 12;
        /// <summary>
        /// Min colored pixel count used by this component.
        /// </summary>

        private const int MIN_COLORED_PIXEL_COUNT = 100;
        /// <summary>
        /// Background channel value used by this component.
        /// </summary>

        private const byte BACKGROUND_CHANNEL_VALUE = 255;
        /// <summary>
        /// Rgba channels used by this component.
        /// </summary>

        private const int RGBA_CHANNELS = 4;

        /// <summary>
        /// Sample points must project deterministically for a fixed camera pose.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(120, -80, 12)]
        [InlineData(-200, 150, 45)]
        public void ProjectModelOffset_IsDeterministic
            (
            double EastM,
            double NorthM,
            double ModelZ
            )
        {
            ProjectSample(EastM, NorthM, ModelZ, SAMPLE_HEADING_DEG, out float X0, out float Y0, out double D0);
            ProjectSample(EastM, NorthM, ModelZ, SAMPLE_HEADING_DEG, out float X1, out float Y1, out double D1);

            Assert.Equal(X0, X1);
            Assert.Equal(Y0, Y1);
            Assert.Equal(D0, D1);
        }

        /// <summary>
        /// Vulkan NDC Y must increase downward like screen pixels.
        /// </summary>
        [Fact]
        public void ScreenYToVulkanNdcY_IncreasesDownward
            (
            )
        {
            float Top = FieldCameraProjection.ScreenYToVulkanNdcY(0, SCREEN_HEIGHT_PX);
            float Bottom = FieldCameraProjection.ScreenYToVulkanNdcY(SCREEN_HEIGHT_PX, SCREEN_HEIGHT_PX);
            Assert.True(Top < Bottom);
            Assert.Equal(NDC_TOP, Top, NDC_PRECISION);
            Assert.Equal(NDC_BOTTOM, Bottom, NDC_PRECISION);
        }

        /// <summary>
        /// Closer camera depth must map to smaller Vulkan NDC Z.
        /// </summary>
        [Fact]
        public void CameraDepthToVulkanNdcZ_MapsCloserPointsNearer
            (
            )
        {
            float NearZ = FieldCameraProjection.CameraDepthToVulkanNdcZ(NEAR_CAMERA_DEPTH, DEPTH_MARGIN, DEPTH_RANGE);
            float FarZ = FieldCameraProjection.CameraDepthToVulkanNdcZ(FAR_CAMERA_DEPTH, DEPTH_MARGIN, DEPTH_RANGE);
            Assert.True(NearZ < FarZ);
        }

        /// <summary>
        /// GPU triangle rasterization must land near the CPU-projected screen centroid.
        /// </summary>
        [Fact]
        public void GpuRenderer_TriangleCentroid_MatchesCpuProjection
            (
            )
        {
            using VulkanGraphicsSession? Session = VulkanGraphicsBackend.TryCreateSession(out string? SessionFailure);
            Assert.NotNull(Session);

            using VulkanOffscreenRenderer? Renderer = VulkanOffscreenRenderer.TryCreate(Session!, out string? RendererFailure);
            Assert.NotNull(Renderer);

            (double East, double North, double Z)[] ModelPoints =
            {
                (TRIANGLE_EAST_MIN, TRIANGLE_NORTH_MIN, 0),
                (TRIANGLE_EAST_MAX, TRIANGLE_NORTH_MIN, 0),
                (0, TRIANGLE_NORTH_MAX, 0)
            };

            float SumX = 0;
            float SumY = 0;
            double DepthMin = double.PositiveInfinity;
            double DepthMax = double.NegativeInfinity;
            foreach ((double East, double North, double Z) Point in ModelPoints)
            {
                FieldCameraProjection.ProjectModelOffset(
                    Point.East,
                    Point.North,
                    Point.Z,
                    RENDER_WIDTH_PX,
                    RENDER_HEIGHT_PX,
                    RENDER_WIDTH_PX * 0.5,
                    RENDER_HEIGHT_PX * 0.5,
                    RENDER_SCALE,
                    TRIG_COS,
                    TRIG_SIN,
                    TRIG_COS,
                    TRIG_SIN,
                    out float ScreenX,
                    out float ScreenY,
                    out double Depth);
                SumX += ScreenX;
                SumY += ScreenY;
                if (Depth < DepthMin)
                    DepthMin = Depth;
                if (Depth > DepthMax)
                    DepthMax = Depth;
            }

            float ExpectedCentroidX = SumX / ModelPoints.Length;
            float ExpectedCentroidY = SumY / ModelPoints.Length;

            GpuFieldFrame Frame = new GpuFieldFrame
            {
                MeshVertices = new[]
                {
                    new GpuVertex { EastM = TRIANGLE_EAST_MIN, NorthM = TRIANGLE_NORTH_MIN, ModelZ = 0, R = 1, G = 0, B = 0, A = 1 },
                    new GpuVertex { EastM = TRIANGLE_EAST_MAX, NorthM = TRIANGLE_NORTH_MIN, ModelZ = 0, R = 0, G = 1, B = 0, A = 1 },
                    new GpuVertex { EastM = 0, NorthM = TRIANGLE_NORTH_MAX, ModelZ = 0, R = 0, G = 0, B = 1, A = 1 }
                },
                Camera = new CameraGpuUniforms
                {
                    ViewportWidth = RENDER_WIDTH_PX,
                    ViewportHeight = RENDER_HEIGHT_PX,
                    ViewportMidX = RENDER_WIDTH_PX * 0.5f,
                    ViewportMidY = RENDER_HEIGHT_PX * 0.5f,
                    Scale = RENDER_SCALE,
                    CosH = TRIG_COS,
                    SinH = TRIG_SIN,
                    CosP = TRIG_COS,
                    SinP = TRIG_SIN,
                    DepthMin = (float)(DepthMin - 1),
                    DepthMax = (float)(DepthMax + 1)
                }
            };

            byte[]? Pixels = Renderer!.TryRender(Frame, RENDER_WIDTH_PX, RENDER_HEIGHT_PX, out string? RenderFailure);
            Assert.NotNull(Pixels);

            (float ActualCentroidX, float ActualCentroidY) = FindColoredCentroid(Pixels, RENDER_WIDTH_PX, RENDER_HEIGHT_PX);
            Assert.True(Math.Abs(ActualCentroidX - ExpectedCentroidX) < CENTROID_TOLERANCE_PX,
                RenderFailure ?? $"Centroid X expected {ExpectedCentroidX:F1}, got {ActualCentroidX:F1}.");
            Assert.True(Math.Abs(ActualCentroidY - ExpectedCentroidY) < CENTROID_TOLERANCE_PX,
                RenderFailure ?? $"Centroid Y expected {ExpectedCentroidY:F1}, got {ActualCentroidY:F1}.");
        }

        static private (float CentroidX, float CentroidY) FindColoredCentroid
            (
            byte[] Pixels,
            int Width,
            int Height
            )
        {
            double SumX = 0;
            double SumY = 0;
            int Count = 0;
            for (int Y = 0; Y < Height; Y++)
            {
                for (int X = 0; X < Width; X++)
                {
                    int Index = (Y * Width + X) * RGBA_CHANNELS;
                    if (Pixels[Index] == BACKGROUND_CHANNEL_VALUE &&
                        Pixels[Index + 1] == BACKGROUND_CHANNEL_VALUE &&
                        Pixels[Index + 2] == BACKGROUND_CHANNEL_VALUE)
                        continue;

                    SumX += X;
                    SumY += Y;
                    Count++;
                }
            }

            Assert.True(Count > MIN_COLORED_PIXEL_COUNT);
            return ((float)(SumX / Count), (float)(SumY / Count));
        }

        static private void ProjectSample
            (
            double EastM,
            double NorthM,
            double ModelZ,
            double HeadingDeg,
            out float ScreenX,
            out float ScreenY,
            out double Depth
            )
        {
            double HeadingRad = HeadingDeg * Math.PI / 180.0;
            FieldCameraProjection.ProjectModelOffset(
                EastM,
                NorthM,
                ModelZ,
                VIEWPORT_WIDTH_PX,
                VIEWPORT_HEIGHT_PX,
                VIEWPORT_MID_X,
                VIEWPORT_MID_Y,
                PROJECTION_SCALE,
                Math.Cos(HeadingRad),
                Math.Sin(HeadingRad),
                Math.Cos(PITCH_DEG * Math.PI / 180.0),
                Math.Sin(PITCH_DEG * Math.PI / 180.0),
                out ScreenX,
                out ScreenY,
                out Depth);
        }
    }
}

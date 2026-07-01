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
            ProjectSample(EastM, NorthM, ModelZ, 45, out float X0, out float Y0, out double D0);
            ProjectSample(EastM, NorthM, ModelZ, 45, out float X1, out float Y1, out double D1);

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
            float Top = FieldCameraProjection.ScreenYToVulkanNdcY(0, 720);
            float Bottom = FieldCameraProjection.ScreenYToVulkanNdcY(720, 720);
            Assert.True(Top < Bottom);
            Assert.Equal(-1.0f, Top, 3);
            Assert.Equal(1.0f, Bottom, 3);
        }

        /// <summary>
        /// Closer camera depth must map to smaller Vulkan NDC Z.
        /// </summary>
        [Fact]
        public void CameraDepthToVulkanNdcZ_MapsCloserPointsNearer
            (
            )
        {
            float NearZ = FieldCameraProjection.CameraDepthToVulkanNdcZ(900, 100, 1000);
            float FarZ = FieldCameraProjection.CameraDepthToVulkanNdcZ(200, 100, 1000);
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

            const int Width = 640;
            const int Height = 480;
            const float Scale = 1.5f;
            const float CosH = 0.8660254f;
            const float SinH = 0.5f;
            const float CosP = 0.8660254f;
            const float SinP = 0.5f;

            (double East, double North, double Z)[] ModelPoints =
            {
                (-100, -100, 0),
                (100, -100, 0),
                (0, 100, 0)
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
                    Width,
                    Height,
                    Width * 0.5,
                    Height * 0.5,
                    Scale,
                    CosH,
                    SinH,
                    CosP,
                    SinP,
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
                    new GpuVertex { EastM = -100, NorthM = -100, ModelZ = 0, R = 1, G = 0, B = 0, A = 1 },
                    new GpuVertex { EastM = 100, NorthM = -100, ModelZ = 0, R = 0, G = 1, B = 0, A = 1 },
                    new GpuVertex { EastM = 0, NorthM = 100, ModelZ = 0, R = 0, G = 0, B = 1, A = 1 }
                },
                Camera = new CameraGpuUniforms
                {
                    ViewportWidth = Width,
                    ViewportHeight = Height,
                    ViewportMidX = Width * 0.5f,
                    ViewportMidY = Height * 0.5f,
                    Scale = Scale,
                    CosH = CosH,
                    SinH = SinH,
                    CosP = CosP,
                    SinP = SinP,
                    DepthMin = (float)(DepthMin - 1),
                    DepthMax = (float)(DepthMax + 1)
                }
            };

            byte[]? Pixels = Renderer!.TryRender(Frame, Width, Height, out string? RenderFailure);
            Assert.NotNull(Pixels);

            (float ActualCentroidX, float ActualCentroidY) = FindColoredCentroid(Pixels, Width, Height);
            Assert.True(Math.Abs(ActualCentroidX - ExpectedCentroidX) < 12,
                RenderFailure ?? $"Centroid X expected {ExpectedCentroidX:F1}, got {ActualCentroidX:F1}.");
            Assert.True(Math.Abs(ActualCentroidY - ExpectedCentroidY) < 12,
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
                    int Index = (Y * Width + X) * 4;
                    if (Pixels[Index] == 255 && Pixels[Index + 1] == 255 && Pixels[Index + 2] == 255)
                        continue;

                    SumX += X;
                    SumY += Y;
                    Count++;
                }
            }

            Assert.True(Count > 100);
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
                1280,
                720,
                640,
                360,
                0.9,
                Math.Cos(HeadingRad),
                Math.Sin(HeadingRad),
                Math.Cos(25 * Math.PI / 180.0),
                Math.Sin(25 * Math.PI / 180.0),
                out ScreenX,
                out ScreenY,
                out Depth);
        }
    }
}

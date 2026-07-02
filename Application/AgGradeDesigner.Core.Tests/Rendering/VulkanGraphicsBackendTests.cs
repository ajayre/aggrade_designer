using AgGradeDesigner.Core.Rendering.Vulkan;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Validates cross-platform Vulkan bootstrap (Windows native drivers, macOS MoltenVK portability).
    /// </summary>

    public sealed class VulkanGraphicsBackendTests
    {
        /// <summary>
        /// Render width px used by this component.
        /// </summary>

        private const int RENDER_WIDTH_PX = 640;
        /// <summary>
        /// Render height px used by this component.
        /// </summary>

        private const int RENDER_HEIGHT_PX = 480;
        /// <summary>
        /// Viewport mid x used by this component.
        /// </summary>

        private const double VIEWPORT_MID_X = 320;
        /// <summary>
        /// Viewport mid y used by this component.
        /// </summary>

        private const double VIEWPORT_MID_Y = 240;
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
        /// Depth min used by this component.
        /// </summary>

        private const float DEPTH_MIN = -500;
        /// <summary>
        /// Depth max used by this component.
        /// </summary>

        private const float DEPTH_MAX = 500;
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
        /// Rgba channels used by this component.
        /// </summary>

        private const int RGBA_CHANNELS = 4;
        /// <summary>
        /// Background channel value used by this component.
        /// </summary>

        private const byte BACKGROUND_CHANNEL_VALUE = 255;
        /// <summary>
        /// Min non background pixels used by this component.
        /// </summary>

        private const int MIN_NON_BACKGROUND_PIXELS = 1000;
        /// <summary>
        /// Probe cycle count used by this component.
        /// </summary>

        private const int PROBE_CYCLE_COUNT = 3;

        /// <summary>
        /// A Vulkan loader, GPU, and graphics queue must be available on developer/CI machines with drivers installed.
        /// </summary>
        [Fact]
        public void Probe_ReturnsAvailableDevice
            (
            )
        {
            VulkanProbeResult Result = VulkanGraphicsBackend.Probe();
            Assert.True(Result.IsAvailable, DescribeFailure(Result));
            Assert.False(string.IsNullOrWhiteSpace(Result.DeviceName));
            Assert.False(string.IsNullOrWhiteSpace(Result.ApiVersion));
            Assert.False(string.IsNullOrWhiteSpace(Result.DeviceType));
        }

        /// <summary>
        /// Session creation must succeed and accept empty command-buffer work on the graphics queue.
        /// </summary>
        [Fact]
        public void TryCreateSession_SubmitsEmptyGraphicsWork
            (
            )
        {
            using VulkanGraphicsSession? Session = VulkanGraphicsBackend.TryCreateSession(out string? FailureReason);
            Assert.NotNull(Session);
            Assert.False(string.IsNullOrWhiteSpace(Session!.DeviceName), FailureReason);

            bool Submitted = Session.TrySubmitEmptyGraphicsWork(out string? SubmitFailure);
            Assert.True(Submitted, SubmitFailure ?? "Queue submission failed.");
        }

        /// <summary>
        /// Offscreen renderer must draw a simple coloured triangle and return pixels.
        /// </summary>
        [Fact]
        public void OffscreenRenderer_DrawsTrianglePixels
            (
            )
        {
            using VulkanGraphicsSession? Session = VulkanGraphicsBackend.TryCreateSession(out string? SessionFailure);
            Assert.NotNull(Session);

            using VulkanOffscreenRenderer? Renderer = VulkanOffscreenRenderer.TryCreate(Session!, out string? RendererFailure);
            Assert.NotNull(Renderer);

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
                    ViewportMidX = (float)VIEWPORT_MID_X,
                    ViewportMidY = (float)VIEWPORT_MID_Y,
                    Scale = RENDER_SCALE,
                    CosH = 1,
                    SinH = 0,
                    CosP = TRIG_COS,
                    SinP = TRIG_SIN,
                    DepthMin = DEPTH_MIN,
                    DepthMax = DEPTH_MAX
                }
            };

            byte[]? Pixels = Renderer!.TryRender(Frame, RENDER_WIDTH_PX, RENDER_HEIGHT_PX, out string? RenderFailure);
            Assert.NotNull(Pixels);
            Assert.Equal(RENDER_WIDTH_PX * RENDER_HEIGHT_PX * RGBA_CHANNELS, Pixels!.Length);

            int NonBackground = 0;
            for (int I = 0; I < Pixels.Length; I += RGBA_CHANNELS)
            {
                if (Pixels[I] != BACKGROUND_CHANNEL_VALUE ||
                    Pixels[I + 1] != BACKGROUND_CHANNEL_VALUE ||
                    Pixels[I + 2] != BACKGROUND_CHANNEL_VALUE)
                    NonBackground++;
            }

            Assert.True(NonBackground > MIN_NON_BACKGROUND_PIXELS, RenderFailure ?? "Triangle render produced no visible pixels.");
        }

        /// <summary>
        /// Repeated probe/dispose cycles must remain stable (no loader leaks or stale handles).
        /// </summary>
        [Fact]
        public void Probe_RepeatedCycles_RemainStable
            (
            )
        {
            for (int Cycle = 0; Cycle < PROBE_CYCLE_COUNT; Cycle++)
            {
                VulkanProbeResult Result = VulkanGraphicsBackend.Probe();
                Assert.True(Result.IsAvailable, $"Cycle {Cycle}: {DescribeFailure(Result)}");
            }
        }

        static private string DescribeFailure
            (
            VulkanProbeResult Result
            )
        {
            return $"{Result.Platform}: {Result.FailureReason ?? "Vulkan unavailable."}";
        }
    }
}

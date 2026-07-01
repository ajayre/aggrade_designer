using AgGradeDesigner.Core.Rendering.Vulkan;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Validates cross-platform Vulkan bootstrap (Windows native drivers, macOS MoltenVK portability).
    /// </summary>
    public sealed class VulkanGraphicsBackendTests
    {
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
                    new GpuVertex { EastM = -100, NorthM = -100, ModelZ = 0, R = 1, G = 0, B = 0, A = 1 },
                    new GpuVertex { EastM = 100, NorthM = -100, ModelZ = 0, R = 0, G = 1, B = 0, A = 1 },
                    new GpuVertex { EastM = 0, NorthM = 100, ModelZ = 0, R = 0, G = 0, B = 1, A = 1 }
                },
                Camera = new CameraGpuUniforms
                {
                    ViewportWidth = 640,
                    ViewportHeight = 480,
                    ViewportMidX = 320,
                    ViewportMidY = 240,
                    Scale = 1.5f,
                    CosH = 1,
                    SinH = 0,
                    CosP = 0.8660254f,
                    SinP = 0.5f,
                    DepthMin = -500,
                    DepthMax = 500
                }
            };

            byte[]? Pixels = Renderer!.TryRender(Frame, 640, 480, out string? RenderFailure);
            Assert.NotNull(Pixels);
            Assert.Equal(640 * 480 * 4, Pixels!.Length);

            int NonBackground = 0;
            for (int I = 0; I < Pixels.Length; I += 4)
            {
                if (Pixels[I] != 255 || Pixels[I + 1] != 255 || Pixels[I + 2] != 255)
                    NonBackground++;
            }

            Assert.True(NonBackground > 1000, RenderFailure ?? "Triangle render produced no visible pixels.");
        }

        /// <summary>
        /// Repeated probe/dispose cycles must remain stable (no loader leaks or stale handles).
        /// </summary>
        [Fact]
        public void Probe_RepeatedCycles_RemainStable
            (
            )
        {
            for (int Cycle = 0; Cycle < 3; Cycle++)
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

using AgGradeDesigner.Core.Rendering.Vulkan;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Application-level graphics runtime state shared by the viewer and diagnostics UI.
    /// </summary>
    static public class GraphicsRuntime
    {
        static private readonly object Sync = new object();
        static private bool _Initialized;
        static private VulkanProbeResult? _VulkanProbe;
        static private VulkanGraphicsSession? _Session;
        static private VulkanOffscreenRenderer? _Renderer;
        static private string? _InitFailure;

        /// <summary>True after <see cref="Initialize"/> has completed.</summary>
        static public bool IsInitialized
        {
            get
            {
                lock (Sync)
                    return _Initialized;
            }
        }

        /// <summary>Latest Vulkan probe result, or null before initialization.</summary>
        static public VulkanProbeResult? VulkanProbe
        {
            get
            {
                lock (Sync)
                    return _VulkanProbe;
            }
        }

        /// <summary>True when a Vulkan device and graphics queue were detected.</summary>
        static public bool IsVulkanAvailable => VulkanProbe?.IsAvailable == true;

        /// <summary>True when the offscreen Vulkan renderer is ready for 3D field frames.</summary>
        static public bool IsVulkanRendererReady
        {
            get
            {
                lock (Sync)
                    return _Renderer != null;
            }
        }

        /// <summary>
        /// Probes Vulkan once at startup, creates a persistent session and offscreen renderer.
        /// Safe to call repeatedly.
        /// </summary>
        static public void Initialize
            (
            )
        {
            lock (Sync)
            {
                if (_Initialized)
                    return;

                _VulkanProbe = VulkanGraphicsBackend.Probe();
                if (_VulkanProbe.IsAvailable)
                {
                    _Session = VulkanGraphicsBackend.TryCreateSession(out string? SessionFailure);
                    if (_Session == null)
                    {
                        _InitFailure = SessionFailure;
                    }
                    else
                    {
                        _Renderer = VulkanOffscreenRenderer.TryCreate(_Session, out string? RendererFailure);
                        if (_Renderer == null)
                            _InitFailure = RendererFailure;
                    }
                }

                _Initialized = true;
            }
        }

        /// <summary>
        /// Idempotent alias for <see cref="Initialize"/> — ensures Vulkan is probed before 3D draws.
        /// </summary>
        static public void EnsureInitialized
            (
            )
        {
            Initialize();
        }

        /// <summary>
        /// True when the Vulkan renderer should draw all 3D field frames.
        /// </summary>
        static public bool ShouldUseVulkanFor3D
            (
            )
        {
            EnsureInitialized();
            return IsVulkanRendererReady;
        }

        /// <summary>
        /// True when the Vulkan renderer should draw 2D plan-view field frames.
        /// Kept off until the batched 2D path is validated interactively; Skia is the 2D path.
        /// </summary>
        static public bool ShouldUseVulkanFor2D
            (
            )
        {
            return false;
        }

        /// <summary>
        /// Renders one GPU field frame and returns BGRA8888 pixels.
        /// </summary>
        static public bool TryRenderGpuFieldFrame
            (
            GpuFieldFrame Frame,
            int Width,
            int Height,
            out byte[]? Pixels,
            out string? FailureReason
            )
        {
            Pixels = null;
            FailureReason = null;
            lock (Sync)
            {
                if (_Renderer == null)
                {
                    FailureReason = _InitFailure ?? "Vulkan renderer is not initialized.";
                    return false;
                }

                Pixels = _Renderer.TryRender(Frame, Width, Height, out FailureReason);
                return Pixels != null;
            }
        }

        /// <summary>
        /// Renders multiple GPU field frames under one runtime lock (one queue round-trip per frame).
        /// </summary>
        static public bool TryRenderGpuFieldFrameBatch
            (
            IReadOnlyList<GpuFieldFrame> Frames,
            int Width,
            int Height,
            out byte[]?[]? Pixels,
            out string? FailureReason
            )
        {
            Pixels = null;
            FailureReason = null;
            lock (Sync)
            {
                if (_Renderer == null)
                {
                    FailureReason = _InitFailure ?? "Vulkan renderer is not initialized.";
                    return false;
                }

                Pixels = _Renderer.TryRenderBatch(Frames, Width, Height, out FailureReason);
                return Pixels != null;
            }
        }

        /// <summary>
        /// Human-readable summary for status UI and logs.
        /// </summary>
        static public string DescribeVulkanStatus
            (
            )
        {
            VulkanProbeResult? Probe = VulkanProbe;
            if (Probe == null)
                return "Vulkan: not probed";

            if (!Probe.IsAvailable)
                return $"Vulkan: unavailable ({Probe.FailureReason})";

            string DriverKind = Probe.UsesPortabilityDriver ? "MoltenVK" : "native";
            if (!IsVulkanRendererReady)
                return $"Vulkan: {Probe.DeviceName} ({DriverKind}) — renderer failed ({_InitFailure})";

            return $"Vulkan: {Probe.DeviceName} ({DriverKind}, API {Probe.ApiVersion}, GPU 2D/3D)";
        }
    }
}

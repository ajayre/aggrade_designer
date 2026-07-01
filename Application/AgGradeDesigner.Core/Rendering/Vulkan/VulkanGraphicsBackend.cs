using System.Runtime.InteropServices;

using Silk.NET.Vulkan;

namespace AgGradeDesigner.Core.Rendering.Vulkan
{
    /// <summary>
    /// Cross-platform Vulkan bootstrap for Windows (native drivers) and macOS (MoltenVK via portability extensions).
    /// </summary>
    static public class VulkanGraphicsBackend
    {
        /// <summary>
        /// Probes the local Vulkan loader without retaining GPU resources.
        /// </summary>
        static public VulkanProbeResult Probe
            (
            )
        {
            string Platform = GetPlatformLabel();
            using VulkanGraphicsSession? Session = VulkanGraphicsSession.TryCreate(out string? FailureReason);
            if (Session == null)
            {
                return new VulkanProbeResult
                {
                    IsAvailable = false,
                    FailureReason = FailureReason ?? "Vulkan initialization failed.",
                    Platform = Platform
                };
            }

            return new VulkanProbeResult
            {
                IsAvailable = true,
                DeviceName = Session.DeviceName,
                VendorId = Session.VendorId,
                DeviceType = Session.DeviceType.ToString(),
                ApiVersion = FormatApiVersion(Session.ApiVersion),
                UsesPortabilityDriver = Session.UsesPortabilityDriver,
                Platform = Platform
            };
        }

        /// <summary>
        /// Creates a reusable Vulkan session when the runtime is available.
        /// </summary>
        static public VulkanGraphicsSession? TryCreateSession
            (
            out string? FailureReason
            )
        {
            return VulkanGraphicsSession.TryCreate(out FailureReason);
        }

        static private string GetPlatformLabel
            (
            )
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macOS";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Linux";
            return Environment.OSVersion.Platform.ToString();
        }

        static private string FormatApiVersion
            (
            uint ApiVersion
            )
        {
            uint Major = (ApiVersion >> 22) & 0x3FF;
            uint Minor = (ApiVersion >> 12) & 0x3FF;
            uint Patch = ApiVersion & 0xFFF;
            return $"{Major}.{Minor}.{Patch}";
        }
    }
}

namespace AgGradeDesigner.Core.Rendering.Vulkan
{
    /// <summary>
    /// Outcome of probing the local Vulkan runtime without retaining GPU resources.
    /// </summary>
    public sealed record VulkanProbeResult
    {
        /// <summary>True when instance creation and device enumeration succeeded.</summary>
        public required bool IsAvailable { get; init; }

        /// <summary>Human-readable reason when <see cref="IsAvailable"/> is false.</summary>
        public string? FailureReason { get; init; }

        /// <summary>Selected GPU device name.</summary>
        public string? DeviceName { get; init; }

        /// <summary>Selected GPU vendor id.</summary>
        public uint VendorId { get; init; }

        /// <summary>Selected GPU device type (discrete, integrated, etc.).</summary>
        public string? DeviceType { get; init; }

        /// <summary>Vulkan API version supported by the selected device.</summary>
        public string? ApiVersion { get; init; }

        /// <summary>True when the selected device is exposed through a portability implementation (e.g. MoltenVK).</summary>
        public bool UsesPortabilityDriver { get; init; }

        /// <summary>Operating system where the probe ran.</summary>
        public required string Platform { get; init; }
    }
}

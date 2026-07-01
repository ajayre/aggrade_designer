using System.Runtime.InteropServices;

using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace AgGradeDesigner.Core.Rendering.Vulkan
{
    /// <summary>
    /// Owns a minimal Vulkan instance, device, and graphics queue for offscreen rendering.
    /// </summary>
    public sealed unsafe class VulkanGraphicsSession : IDisposable
    {
        private const string PortabilityEnumerationExtensionName = "VK_KHR_portability_enumeration";
        private const string PortabilitySubsetExtensionName = "VK_KHR_portability_subset";

        private readonly Vk _Vk;
        private readonly Instance _Instance;
        private readonly PhysicalDevice _PhysicalDevice;
        private readonly Device _Device;
        private readonly Queue _GraphicsQueue;
        private readonly uint _GraphicsQueueFamilyIndex;
        private readonly CommandPool _CommandPool;
        private readonly Fence _Fence;
        private bool _Disposed;

        private VulkanGraphicsSession
            (
            Vk Vk,
            Instance Instance,
            PhysicalDevice PhysicalDevice,
            Device Device,
            Queue GraphicsQueue,
            uint GraphicsQueueFamilyIndex,
            CommandPool CommandPool,
            Fence Fence
            )
        {
            _Vk = Vk;
            _Instance = Instance;
            _PhysicalDevice = PhysicalDevice;
            _Device = Device;
            _GraphicsQueue = GraphicsQueue;
            _GraphicsQueueFamilyIndex = GraphicsQueueFamilyIndex;
            _CommandPool = CommandPool;
            _Fence = Fence;
        }

        /// <summary>Selected GPU device name.</summary>
        public string DeviceName { get; init; } = "";

        /// <summary>Selected GPU vendor id.</summary>
        public uint VendorId { get; init; }

        /// <summary>Selected GPU device type.</summary>
        public PhysicalDeviceType DeviceType { get; init; }

        /// <summary>Vulkan API version supported by the selected device.</summary>
        public uint ApiVersion { get; init; }

        /// <summary>True when the selected device is exposed through a portability implementation (e.g. MoltenVK).</summary>
        public bool UsesPortabilityDriver { get; init; }

        /// <summary>Vulkan API entry points.</summary>
        public Vk Vk => _Vk;

        /// <summary>Logical device handle.</summary>
        public Device Device => _Device;

        /// <summary>Selected physical device handle.</summary>
        public PhysicalDevice PhysicalDevice => _PhysicalDevice;

        /// <summary>Primary graphics queue.</summary>
        public Queue GraphicsQueue => _GraphicsQueue;

        /// <summary>Graphics queue family index.</summary>
        public uint GraphicsQueueFamilyIndex => _GraphicsQueueFamilyIndex;

        /// <summary>Shared command pool for render submissions.</summary>
        public CommandPool CommandPool => _CommandPool;

        /// <summary>Shared fence used to serialize GPU work.</summary>
        public Fence FrameFence => _Fence;

        /// <summary>
        /// Creates a Vulkan session when a suitable GPU and loader are available.
        /// </summary>
        static public VulkanGraphicsSession? TryCreate
            (
            out string? FailureReason
            )
        {
            FailureReason = null;
            Vk? VkApi = null;
            Instance Instance = default;
            Device Device = default;
            CommandPool CommandPool = default;
            Fence Fence = default;
            byte** InstanceExtensionsPtr = null;
            byte** DeviceExtensionsPtr = null;

            try
            {
                VkApi = Vk.GetApi();
                List<string> InstanceExtensions = new List<string>();
                InstanceCreateFlags InstanceFlags = InstanceCreateFlags.None;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    InstanceExtensions.Add(PortabilityEnumerationExtensionName);
                    InstanceFlags |= InstanceCreateFlags.EnumeratePortabilityBitKhr;
                }

                ApplicationInfo ApplicationInfo = new ApplicationInfo
                {
                    SType = StructureType.ApplicationInfo,
                    PApplicationName = (byte*)SilkMarshal.StringToPtr("AgGrade Designer"),
                    ApplicationVersion = Vk.MakeVersion(1, 0, 0),
                    PEngineName = (byte*)SilkMarshal.StringToPtr("AgGrade Designer"),
                    EngineVersion = Vk.MakeVersion(1, 0, 0),
                    ApiVersion = Vk.Version12
                };

                InstanceExtensionsPtr = (byte**)SilkMarshal.StringArrayToPtr(InstanceExtensions.ToArray());
                InstanceCreateInfo InstanceCreateInfo = new InstanceCreateInfo
                {
                    SType = StructureType.InstanceCreateInfo,
                    Flags = InstanceFlags,
                    PApplicationInfo = &ApplicationInfo,
                    EnabledExtensionCount = (uint)InstanceExtensions.Count,
                    PpEnabledExtensionNames = InstanceExtensionsPtr
                };

                Result CreateInstanceResult = VkApi.CreateInstance(in InstanceCreateInfo, null, out Instance);
                SilkMarshal.Free((nint)ApplicationInfo.PApplicationName);
                SilkMarshal.Free((nint)ApplicationInfo.PEngineName);
                if (CreateInstanceResult != Result.Success)
                {
                    FailureReason = $"vkCreateInstance returned {CreateInstanceResult}.";
                    return null;
                }

                PhysicalDevice[] PhysicalDevices = VkApi.GetPhysicalDevices(Instance).ToArray();
                if (PhysicalDevices.Length == 0)
                {
                    FailureReason = "No Vulkan physical devices were reported.";
                    VkApi.DestroyInstance(Instance, null);
                    return null;
                }

                PhysicalDevice SelectedDevice = SelectPhysicalDevice(VkApi, PhysicalDevices);
                PhysicalDeviceProperties DeviceProperties = VkApi.GetPhysicalDeviceProperties(SelectedDevice);
                string DeviceName = SilkMarshal.PtrToString((nint)DeviceProperties.DeviceName) ?? "Unknown";
                bool UsesPortabilityDriver = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

                List<string> DeviceExtensions = new List<string>();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    DeviceExtensions.Add(PortabilitySubsetExtensionName);

                if (!TryFindGraphicsQueueFamily(VkApi, SelectedDevice, out uint QueueFamilyIndex, out QueueFamilyProperties _))
                {
                    FailureReason = "No graphics queue family was found on the selected device.";
                    VkApi.DestroyInstance(Instance, null);
                    return null;
                }

                DeviceExtensionsPtr = (byte**)SilkMarshal.StringArrayToPtr(DeviceExtensions.ToArray());
                float QueuePriority = 1.0f;
                DeviceQueueCreateInfo QueueCreateInfo = new DeviceQueueCreateInfo
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = QueueFamilyIndex,
                    QueueCount = 1,
                    PQueuePriorities = &QueuePriority
                };

                DeviceCreateInfo DeviceCreateInfo = new DeviceCreateInfo
                {
                    SType = StructureType.DeviceCreateInfo,
                    QueueCreateInfoCount = 1,
                    PQueueCreateInfos = &QueueCreateInfo,
                    EnabledExtensionCount = (uint)DeviceExtensions.Count,
                    PpEnabledExtensionNames = DeviceExtensionsPtr
                };

                Result CreateDeviceResult = VkApi.CreateDevice(SelectedDevice, in DeviceCreateInfo, null, out Device);
                if (CreateDeviceResult != Result.Success)
                {
                    FailureReason = $"vkCreateDevice returned {CreateDeviceResult}.";
                    VkApi.DestroyInstance(Instance, null);
                    return null;
                }

                VkApi.GetDeviceQueue(Device, QueueFamilyIndex, 0, out Queue GraphicsQueue);

                CommandPoolCreateInfo CommandPoolCreateInfo = new CommandPoolCreateInfo
                {
                    SType = StructureType.CommandPoolCreateInfo,
                    QueueFamilyIndex = QueueFamilyIndex,
                    Flags = CommandPoolCreateFlags.ResetCommandBufferBit
                };
                Result CreateCommandPoolResult = VkApi.CreateCommandPool(Device, in CommandPoolCreateInfo, null, out CommandPool);
                if (CreateCommandPoolResult != Result.Success)
                {
                    FailureReason = $"vkCreateCommandPool returned {CreateCommandPoolResult}.";
                    VkApi.DestroyDevice(Device, null);
                    VkApi.DestroyInstance(Instance, null);
                    return null;
                }

                FenceCreateInfo FenceCreateInfo = new FenceCreateInfo
                {
                    SType = StructureType.FenceCreateInfo,
                    Flags = FenceCreateFlags.SignaledBit
                };
                Result CreateFenceResult = VkApi.CreateFence(Device, in FenceCreateInfo, null, out Fence);
                if (CreateFenceResult != Result.Success)
                {
                    FailureReason = $"vkCreateFence returned {CreateFenceResult}.";
                    VkApi.DestroyCommandPool(Device, CommandPool, null);
                    VkApi.DestroyDevice(Device, null);
                    VkApi.DestroyInstance(Instance, null);
                    return null;
                }

                VulkanGraphicsSession Session = new VulkanGraphicsSession(
                    VkApi,
                    Instance,
                    SelectedDevice,
                    Device,
                    GraphicsQueue,
                    QueueFamilyIndex,
                    CommandPool,
                    Fence)
                {
                    DeviceName = DeviceName,
                    VendorId = DeviceProperties.VendorID,
                    DeviceType = DeviceProperties.DeviceType,
                    ApiVersion = DeviceProperties.ApiVersion,
                    UsesPortabilityDriver = UsesPortabilityDriver
                };

                if (!Session.TrySubmitEmptyGraphicsWork(out string? SubmitFailure))
                {
                    FailureReason = SubmitFailure;
                    Session.Dispose();
                    return null;
                }

                return Session;
            }
            catch (Exception Ex)
            {
                FailureReason = Ex.Message;
                if (Fence.Handle != 0 && Device.Handle != 0 && VkApi != null)
                    VkApi.DestroyFence(Device, Fence, null);
                if (CommandPool.Handle != 0 && Device.Handle != 0 && VkApi != null)
                    VkApi.DestroyCommandPool(Device, CommandPool, null);
                if (Device.Handle != 0 && VkApi != null)
                    VkApi.DestroyDevice(Device, null);
                if (Instance.Handle != 0 && VkApi != null)
                    VkApi.DestroyInstance(Instance, null);
                VkApi?.Dispose();
                return null;
            }
            finally
            {
                if (InstanceExtensionsPtr != null)
                    SilkMarshal.Free((nint)InstanceExtensionsPtr);
                if (DeviceExtensionsPtr != null)
                    SilkMarshal.Free((nint)DeviceExtensionsPtr);
            }
        }

        /// <summary>
        /// Submits and waits on an empty command buffer to verify queue submission works.
        /// </summary>
        public bool TrySubmitEmptyGraphicsWork
            (
            out string? FailureReason
            )
        {
            FailureReason = null;
            CommandBufferAllocateInfo AllocateInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _CommandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };

            Result AllocateResult = _Vk.AllocateCommandBuffers(_Device, in AllocateInfo, out CommandBuffer CommandBuffer);
            if (AllocateResult != Result.Success)
            {
                FailureReason = $"vkAllocateCommandBuffers returned {AllocateResult}.";
                return false;
            }

            _Vk.ResetFences(_Device, 1, in _Fence);

            CommandBufferBeginInfo BeginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };

            Result BeginResult = _Vk.BeginCommandBuffer(CommandBuffer, in BeginInfo);
            if (BeginResult != Result.Success)
            {
                FailureReason = $"vkBeginCommandBuffer returned {BeginResult}.";
                _Vk.FreeCommandBuffers(_Device, _CommandPool, 1, in CommandBuffer);
                return false;
            }

            Result EndResult = _Vk.EndCommandBuffer(CommandBuffer);
            if (EndResult != Result.Success)
            {
                FailureReason = $"vkEndCommandBuffer returned {EndResult}.";
                _Vk.FreeCommandBuffers(_Device, _CommandPool, 1, in CommandBuffer);
                return false;
            }

            SubmitInfo SubmitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &CommandBuffer
            };

            Result SubmitResult = _Vk.QueueSubmit(_GraphicsQueue, 1, in SubmitInfo, _Fence);
            if (SubmitResult != Result.Success)
            {
                FailureReason = $"vkQueueSubmit returned {SubmitResult}.";
                _Vk.FreeCommandBuffers(_Device, _CommandPool, 1, in CommandBuffer);
                return false;
            }

            Result WaitResult = _Vk.WaitForFences(_Device, 1, in _Fence, true, 5_000_000_000);
            _Vk.FreeCommandBuffers(_Device, _CommandPool, 1, in CommandBuffer);
            if (WaitResult != Result.Success)
            {
                FailureReason = $"vkWaitForFences returned {WaitResult}.";
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public void Dispose
            (
            )
        {
            if (_Disposed)
                return;

            _Disposed = true;
            _Vk.DestroyFence(_Device, _Fence, null);
            _Vk.DestroyCommandPool(_Device, _CommandPool, null);
            _Vk.DestroyDevice(_Device, null);
            _Vk.DestroyInstance(_Instance, null);
            _Vk.Dispose();
        }

        static private PhysicalDevice SelectPhysicalDevice
            (
            Vk VkApi,
            IReadOnlyList<PhysicalDevice> Devices
            )
        {
            PhysicalDevice? Discrete = null;
            PhysicalDevice? Integrated = null;
            PhysicalDevice? Fallback = null;

            foreach (PhysicalDevice Device in Devices)
            {
                Fallback ??= Device;
                PhysicalDeviceProperties Properties = VkApi.GetPhysicalDeviceProperties(Device);
                if (Properties.DeviceType == PhysicalDeviceType.DiscreteGpu)
                {
                    Discrete = Device;
                    break;
                }

                if (Properties.DeviceType == PhysicalDeviceType.IntegratedGpu)
                    Integrated ??= Device;
            }

            if (Discrete.HasValue)
                return Discrete.Value;
            if (Integrated.HasValue)
                return Integrated.Value;
            return Fallback!.Value;
        }

        static private bool TryFindGraphicsQueueFamily
            (
            Vk VkApi,
            PhysicalDevice Device,
            out uint QueueFamilyIndex,
            out QueueFamilyProperties SelectedFamily
            )
        {
            uint FamilyCount = 0;
            VkApi.GetPhysicalDeviceQueueFamilyProperties(Device, ref FamilyCount, null);
            QueueFamilyProperties[] Families = new QueueFamilyProperties[FamilyCount];
            fixed (QueueFamilyProperties* FamiliesPtr = Families)
            {
                VkApi.GetPhysicalDeviceQueueFamilyProperties(Device, ref FamilyCount, FamiliesPtr);
            }

            for (uint Index = 0; Index < Families.Length; Index++)
            {
                if (!Families[Index].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                    continue;

                QueueFamilyIndex = Index;
                SelectedFamily = Families[Index];
                return true;
            }

            QueueFamilyIndex = 0;
            SelectedFamily = default;
            return false;
        }
    }
}

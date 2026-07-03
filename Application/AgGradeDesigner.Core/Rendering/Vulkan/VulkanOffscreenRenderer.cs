using System.Runtime.InteropServices;

using Silk.NET.Vulkan;

using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace AgGradeDesigner.Core.Rendering.Vulkan
{
    /// <summary>
    /// Renders field mesh and line overlays into an offscreen target and reads back BGRA pixels.
    /// </summary>
    public sealed unsafe class VulkanOffscreenRenderer : IDisposable
    {
        /// <summary>Offscreen colour attachment format (BGRA8888).</summary>
        private const Format ColorFormat = Format.B8G8R8A8Unorm;

        /// <summary>Offscreen depth attachment format.</summary>
        private const Format DepthFormat = Format.D32Sfloat;

        /// <summary>Active Vulkan session supplying device, queue, and command pool handles.</summary>
        private readonly VulkanGraphicsSession _Session;

        /// <summary>Vulkan API entry points bound to the session instance.</summary>
        private readonly Vk _Vk;

        /// <summary>Logical device handle used for all GPU resource creation.</summary>
        private readonly Device _Device;

        /// <summary>Selected physical device backing the logical device.</summary>
        private readonly PhysicalDevice _PhysicalDevice;

        /// <summary>Graphics queue used for offscreen render submissions.</summary>
        private readonly Queue _Queue;

        /// <summary>Shared command pool for one-time render command buffers.</summary>
        private readonly CommandPool _CommandPool;

        /// <summary>Shared fence serializing render submissions and readback.</summary>
        private readonly Fence _Fence;

        /// <summary>Compiled coloured mesh vertex shader module.</summary>
        private readonly ShaderModule _VertexShader;

        /// <summary>Compiled opaque fragment shader module.</summary>
        private readonly ShaderModule _FragmentShader;

        /// <summary>Compiled alpha-blended overlay fragment shader module.</summary>
        private readonly ShaderModule _OverlayFragmentShader;

        /// <summary>Compiled textured vertex shader module for satellite tiles.</summary>
        private readonly ShaderModule _TexturedVertexShader;

        /// <summary>Compiled textured fragment shader module for satellite tiles.</summary>
        private readonly ShaderModule _TexturedFragmentShader;

        /// <summary>Shared pipeline layout with camera UBO and overlay push constants.</summary>
        private readonly PipelineLayout _PipelineLayout;

        /// <summary>Descriptor set layout for camera UBO and tile sampler binding.</summary>
        private readonly DescriptorSetLayout _DescriptorSetLayout;

        /// <summary>Descriptor pool backing the per-frame descriptor set.</summary>
        private readonly DescriptorPool _DescriptorPool;

        /// <summary>Allocated descriptor set bound at draw time.</summary>
        private readonly DescriptorSet _DescriptorSet;

        /// <summary>Filled triangle pipeline for background geometry.</summary>
        private readonly Pipeline _TrianglePipeline;

        /// <summary>Alpha-blended triangle pipeline for terrain overlays.</summary>
        private readonly Pipeline _MeshTrianglePipeline;

        /// <summary>Textured triangle pipeline for satellite tile draws.</summary>
        private readonly Pipeline _TexturedPipeline;

        /// <summary>Opaque line pipeline for field border polylines.</summary>
        private readonly Pipeline _LinePipeline;

        /// <summary>Alpha-blended line pipeline for contour overlays.</summary>
        private readonly Pipeline _OverlayLinePipeline;

        /// <summary>Plan-view coloured vertex shader for 2D field rendering.</summary>
        private readonly ShaderModule _PlanVertexShader;

        /// <summary>Plan-view textured vertex shader for 2D satellite and colormap tiles.</summary>
        private readonly ShaderModule _PlanTexturedVertexShader;

        /// <summary>Plan-view filled triangle pipeline for placeholder tiles.</summary>
        private readonly Pipeline _PlanTrianglePipeline;

        /// <summary>Plan-view alpha-blended triangle pipeline (unused; reserved for layered 2D).</summary>
        private readonly Pipeline _PlanMeshTrianglePipeline;

        /// <summary>Plan-view textured triangle pipeline for satellite and colormap tiles.</summary>
        private readonly Pipeline _PlanTexturedPipeline;

        /// <summary>Plan-view opaque line pipeline for field borders.</summary>
        private readonly Pipeline _PlanLinePipeline;

        /// <summary>Plan-view alpha-blended line pipeline for contours.</summary>
        private readonly Pipeline _PlanOverlayLinePipeline;

        /// <summary>Colour-plus-depth offscreen render pass shared by all pipelines.</summary>
        private readonly RenderPass _RenderPass;

        /// <summary>Linear clamp sampler for satellite tile textures.</summary>
        private readonly Sampler _Sampler;

        /// <summary>Host-visible vertex buffer for combined textured tile draws.</summary>
        private VkBuffer _TexturedVertexBuffer;

        /// <summary>Device memory backing <see cref="_TexturedVertexBuffer"/>.</summary>
        private DeviceMemory _TexturedVertexMemory;

        /// <summary>Current capacity of <see cref="_TexturedVertexBuffer"/> in bytes.</summary>
        private ulong _TexturedVertexCapacity;

        /// <summary>Host-visible staging buffer for tile pixel uploads.</summary>
        private VkBuffer _TileStagingBuffer;

        /// <summary>Device memory backing <see cref="_TileStagingBuffer"/>.</summary>
        private DeviceMemory _TileStagingMemory;

        /// <summary>Current capacity of <see cref="_TileStagingBuffer"/> in bytes.</summary>
        private ulong _TileStagingCapacity;

        /// <summary>Device-local GPU textures keyed by Web Mercator tile coordinates.</summary>
        private readonly Dictionary<(int Zoom, int X, int Y), GpuCachedTileTexture> _GpuTileCache = new();

        /// <summary>Last satellite cache generation token applied to <see cref="_GpuTileCache"/>.</summary>
        private int _GpuTileCacheGeneration = VulkanConstants.INVALID_GPU_TILE_CACHE_GENERATION;

        /// <summary>Last uploaded background mesh vertex source array reference.</summary>
        private GpuVertex[]? _UploadedBackgroundSource;

        /// <summary>Last uploaded terrain overlay mesh vertex source array reference.</summary>
        private GpuVertex[]? _UploadedMeshSource;

        /// <summary>Last uploaded contour line vertex source array reference.</summary>
        private GpuVertex[]? _UploadedLineSource;

        /// <summary>Last uploaded border line vertex source array reference.</summary>
        private GpuVertex[]? _UploadedBorderLineSource;

        /// <summary>Last uploaded textured tile vertex source array reference.</summary>
        private GpuTexturedVertex[]? _UploadedTexturedSource;

        /// <summary>Reusable CPU readback buffer for rendered BGRA pixels.</summary>
        private byte[]? _ReadbackPixels;

        /// <summary>Current offscreen colour attachment width in pixels.</summary>
        private uint _Width;

        /// <summary>Current offscreen colour attachment height in pixels.</summary>
        private uint _Height;

        /// <summary>Offscreen colour attachment image.</summary>
        private Image _ColorImage;

        /// <summary>Device memory backing <see cref="_ColorImage"/>.</summary>
        private DeviceMemory _ColorMemory;

        /// <summary>Shader read/attachment view for the colour image.</summary>
        private ImageView _ColorView;

        /// <summary>Offscreen depth attachment image.</summary>
        private Image _DepthImage;

        /// <summary>Device memory backing <see cref="_DepthImage"/>.</summary>
        private DeviceMemory _DepthMemory;

        /// <summary>Depth attachment view for the offscreen target.</summary>
        private ImageView _DepthView;

        /// <summary>Framebuffer binding colour and depth attachments for the render pass.</summary>
        private Framebuffer _Framebuffer;

        /// <summary>Host-visible uniform buffer for camera parameters.</summary>
        private VkBuffer _UniformBuffer;

        /// <summary>Device memory backing <see cref="_UniformBuffer"/>.</summary>
        private DeviceMemory _UniformMemory;

        /// <summary>Host-visible vertex buffer for terrain overlay mesh draws.</summary>
        private VkBuffer _MeshVertexBuffer;

        /// <summary>Device memory backing <see cref="_MeshVertexBuffer"/>.</summary>
        private DeviceMemory _MeshVertexMemory;

        /// <summary>Current capacity of <see cref="_MeshVertexBuffer"/> in bytes.</summary>
        private ulong _MeshVertexCapacity;

        /// <summary>Host-visible vertex buffer for background mesh draws.</summary>
        private VkBuffer _BackgroundVertexBuffer;

        /// <summary>Device memory backing <see cref="_BackgroundVertexBuffer"/>.</summary>
        private DeviceMemory _BackgroundVertexMemory;

        /// <summary>Current capacity of <see cref="_BackgroundVertexBuffer"/> in bytes.</summary>
        private ulong _BackgroundVertexCapacity;

        /// <summary>Host-visible vertex buffer for contour line draws.</summary>
        private VkBuffer _LineVertexBuffer;

        /// <summary>Device memory backing <see cref="_LineVertexBuffer"/>.</summary>
        private DeviceMemory _LineVertexMemory;

        /// <summary>Current capacity of <see cref="_LineVertexBuffer"/> in bytes.</summary>
        private ulong _LineVertexCapacity;

        /// <summary>Host-visible vertex buffer for field border line draws.</summary>
        private VkBuffer _BorderLineVertexBuffer;

        /// <summary>Device memory backing <see cref="_BorderLineVertexBuffer"/>.</summary>
        private DeviceMemory _BorderLineVertexMemory;

        /// <summary>Current capacity of <see cref="_BorderLineVertexBuffer"/> in bytes.</summary>
        private ulong _BorderLineVertexCapacity;

        /// <summary>Host-visible buffer receiving colour attachment readback.</summary>
        private VkBuffer _ReadbackBuffer;

        /// <summary>Device memory backing <see cref="_ReadbackBuffer"/>.</summary>
        private DeviceMemory _ReadbackMemory;

        /// <summary>Byte size of the current readback buffer allocation.</summary>
        private ulong _ReadbackSize;

        /// <summary>True after GPU resources have been released in <see cref="Dispose"/>.</summary>
        private bool _Disposed;

        /// <summary>
        /// Wraps pipeline and descriptor resources created by <see cref="TryCreate"/>.
        /// </summary>
        /// <param name="Session">Active Vulkan session supplying device and queue handles.</param>
        /// <param name="VertexShader">Compiled coloured mesh vertex shader module.</param>
        /// <param name="FragmentShader">Compiled opaque fragment shader module.</param>
        /// <param name="OverlayFragmentShader">Compiled overlay fragment shader module.</param>
        /// <param name="TexturedVertexShader">Compiled textured vertex shader module.</param>
        /// <param name="TexturedFragmentShader">Compiled textured fragment shader module.</param>
        /// <param name="PipelineLayout">Shared pipeline layout with camera UBO and push constants.</param>
        /// <param name="DescriptorSetLayout">Descriptor layout for camera UBO and tile sampler.</param>
        /// <param name="DescriptorPool">Pool backing the frame descriptor set.</param>
        /// <param name="DescriptorSet">Allocated descriptor set bound at draw time.</param>
        /// <param name="TrianglePipeline">Filled triangle pipeline for background geometry.</param>
        /// <param name="MeshTrianglePipeline">Alpha-blended triangle pipeline for terrain overlays.</param>
        /// <param name="TexturedPipeline">Textured triangle pipeline for satellite tiles.</param>
        /// <param name="LinePipeline">Opaque line pipeline for field borders.</param>
        /// <param name="OverlayLinePipeline">Alpha-blended line pipeline for contours.</param>
        /// <param name="PlanVertexShader">Plan-view coloured vertex shader module.</param>
        /// <param name="PlanTexturedVertexShader">Plan-view textured vertex shader module.</param>
        /// <param name="PlanTrianglePipeline">Plan-view filled triangle pipeline.</param>
        /// <param name="PlanMeshTrianglePipeline">Plan-view alpha-blended triangle pipeline.</param>
        /// <param name="PlanTexturedPipeline">Plan-view textured triangle pipeline.</param>
        /// <param name="PlanLinePipeline">Plan-view opaque line pipeline.</param>
        /// <param name="PlanOverlayLinePipeline">Plan-view alpha-blended line pipeline.</param>
        /// <param name="RenderPass">Colour-plus-depth offscreen render pass.</param>
        /// <param name="Sampler">Linear clamp sampler for satellite tile textures.</param>
        private VulkanOffscreenRenderer
            (
            VulkanGraphicsSession Session,
            ShaderModule VertexShader,
            ShaderModule FragmentShader,
            ShaderModule OverlayFragmentShader,
            ShaderModule TexturedVertexShader,
            ShaderModule TexturedFragmentShader,
            PipelineLayout PipelineLayout,
            DescriptorSetLayout DescriptorSetLayout,
            DescriptorPool DescriptorPool,
            DescriptorSet DescriptorSet,
            Pipeline TrianglePipeline,
            Pipeline MeshTrianglePipeline,
            Pipeline TexturedPipeline,
            Pipeline LinePipeline,
            Pipeline OverlayLinePipeline,
            ShaderModule PlanVertexShader,
            ShaderModule PlanTexturedVertexShader,
            Pipeline PlanTrianglePipeline,
            Pipeline PlanMeshTrianglePipeline,
            Pipeline PlanTexturedPipeline,
            Pipeline PlanLinePipeline,
            Pipeline PlanOverlayLinePipeline,
            RenderPass RenderPass,
            Sampler Sampler
            )
        {
            _Session = Session;
            _Vk = Session.Vk;
            _Device = Session.Device;
            _PhysicalDevice = Session.PhysicalDevice;
            _Queue = Session.GraphicsQueue;
            _CommandPool = Session.CommandPool;
            _Fence = Session.FrameFence;
            _VertexShader = VertexShader;
            _FragmentShader = FragmentShader;
            _OverlayFragmentShader = OverlayFragmentShader;
            _TexturedVertexShader = TexturedVertexShader;
            _TexturedFragmentShader = TexturedFragmentShader;
            _PipelineLayout = PipelineLayout;
            _DescriptorSetLayout = DescriptorSetLayout;
            _DescriptorPool = DescriptorPool;
            _DescriptorSet = DescriptorSet;
            _TrianglePipeline = TrianglePipeline;
            _MeshTrianglePipeline = MeshTrianglePipeline;
            _TexturedPipeline = TexturedPipeline;
            _LinePipeline = LinePipeline;
            _OverlayLinePipeline = OverlayLinePipeline;
            _PlanVertexShader = PlanVertexShader;
            _PlanTexturedVertexShader = PlanTexturedVertexShader;
            _PlanTrianglePipeline = PlanTrianglePipeline;
            _PlanMeshTrianglePipeline = PlanMeshTrianglePipeline;
            _PlanTexturedPipeline = PlanTexturedPipeline;
            _PlanLinePipeline = PlanLinePipeline;
            _PlanOverlayLinePipeline = PlanOverlayLinePipeline;
            _RenderPass = RenderPass;
            _Sampler = Sampler;

            _UniformBuffer = VulkanMemoryUtilities.CreateBuffer(
                _Vk,
                _Device,
                _PhysicalDevice,
                (ulong)Marshal.SizeOf<CameraGpuUniforms>(),
                BufferUsageFlags.UniformBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                out _UniformMemory);

            DescriptorBufferInfo BufferInfo = new DescriptorBufferInfo
            {
                Buffer = _UniformBuffer,
                Offset = 0,
                Range = (ulong)Marshal.SizeOf<CameraGpuUniforms>()
            };

            WriteDescriptorSet Write = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _DescriptorSet,
                DstBinding = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                PBufferInfo = &BufferInfo
            };
            _Vk.UpdateDescriptorSets(_Device, 1, in Write, 0, null);
        }

        /// <summary>
        /// Creates an offscreen renderer bound to an active Vulkan session.
        /// </summary>
        /// <param name="Session">Active Vulkan session supplying device and queue handles.</param>
        /// <param name="FailureReason">Receives a human-readable failure message when creation fails.</param>
        /// <returns>A ready renderer, or null when shader or pipeline creation fails.</returns>
        static public VulkanOffscreenRenderer? TryCreate
            (
            VulkanGraphicsSession Session,
            out string? FailureReason
            )
        {
            FailureReason = null;
            Vk VkApi = Session.Vk;
            Device Device = Session.Device;

            byte[] VertexSpirv = VulkanShaderCompiler.CompileVertexShader(out string? VertexError);
            if (VertexSpirv.Length == 0)
            {
                FailureReason = VertexError ?? "Vertex shader compilation failed.";
                return null;
            }

            byte[] FragmentSpirv = VulkanShaderCompiler.CompileFragmentShader(out string? FragmentError);
            if (FragmentSpirv.Length == 0)
            {
                FailureReason = FragmentError ?? "Fragment shader compilation failed.";
                return null;
            }

            byte[] OverlayFragmentSpirv = VulkanShaderCompiler.CompileOverlayFragmentShader(out string? OverlayFragmentError);
            if (OverlayFragmentSpirv.Length == 0)
            {
                FailureReason = OverlayFragmentError ?? "Overlay fragment shader compilation failed.";
                return null;
            }

            byte[] TexturedVertexSpirv = VulkanShaderCompiler.CompileTexturedVertexShader(out string? TexturedVertexError);
            if (TexturedVertexSpirv.Length == 0)
            {
                FailureReason = TexturedVertexError ?? "Textured vertex shader compilation failed.";
                return null;
            }

            byte[] TexturedFragmentSpirv = VulkanShaderCompiler.CompileTexturedFragmentShader(out string? TexturedFragmentError);
            if (TexturedFragmentSpirv.Length == 0)
            {
                FailureReason = TexturedFragmentError ?? "Textured fragment shader compilation failed.";
                return null;
            }

            byte[] PlanVertexSpirv = VulkanShaderCompiler.CompilePlanViewColoredVertexShader(out string? PlanVertexError);
            if (PlanVertexSpirv.Length == 0)
            {
                FailureReason = PlanVertexError ?? "Plan-view vertex shader compilation failed.";
                return null;
            }

            byte[] PlanTexturedVertexSpirv = VulkanShaderCompiler.CompilePlanViewTexturedVertexShader(out string? PlanTexturedVertexError);
            if (PlanTexturedVertexSpirv.Length == 0)
            {
                FailureReason = PlanTexturedVertexError ?? "Plan-view textured vertex shader compilation failed.";
                return null;
            }

            ShaderModule VertexShader = default;
            ShaderModule FragmentShader = default;
            ShaderModule OverlayFragmentShader = default;
            ShaderModule TexturedVertexShader = default;
            ShaderModule TexturedFragmentShader = default;
            ShaderModule PlanVertexShader = default;
            ShaderModule PlanTexturedVertexShader = default;
            DescriptorSetLayout DescriptorSetLayout = default;
            DescriptorPool DescriptorPool = default;
            PipelineLayout PipelineLayout = default;
            Pipeline TrianglePipeline = default;
            Pipeline MeshTrianglePipeline = default;
            Pipeline TexturedPipeline = default;
            Pipeline LinePipeline = default;
            Pipeline OverlayLinePipeline = default;
            Pipeline PlanTrianglePipeline = default;
            Pipeline PlanMeshTrianglePipeline = default;
            Pipeline PlanTexturedPipeline = default;
            Pipeline PlanLinePipeline = default;
            Pipeline PlanOverlayLinePipeline = default;
            RenderPass RenderPass = default;
            Sampler Sampler = default;

            try
            {
                fixed (byte* VertexPtr = VertexSpirv)
                {
                    ShaderModuleCreateInfo VertexInfo = new ShaderModuleCreateInfo
                    {
                        SType = StructureType.ShaderModuleCreateInfo,
                        CodeSize = (nuint)VertexSpirv.Length,
                        PCode = (uint*)VertexPtr
                    };
                    if (VkApi.CreateShaderModule(Device, in VertexInfo, null, out VertexShader) != Result.Success)
                    {
                        FailureReason = "Failed to create vertex shader module.";
                        return null;
                    }
                }

                fixed (byte* FragmentPtr = FragmentSpirv)
                {
                    ShaderModuleCreateInfo FragmentInfo = new ShaderModuleCreateInfo
                    {
                        SType = StructureType.ShaderModuleCreateInfo,
                        CodeSize = (nuint)FragmentSpirv.Length,
                        PCode = (uint*)FragmentPtr
                    };
                    if (VkApi.CreateShaderModule(Device, in FragmentInfo, null, out FragmentShader) != Result.Success)
                    {
                        FailureReason = "Failed to create fragment shader module.";
                        return null;
                    }
                }

                fixed (byte* OverlayFragmentPtr = OverlayFragmentSpirv)
                {
                    ShaderModuleCreateInfo OverlayFragmentInfo = new ShaderModuleCreateInfo
                    {
                        SType = StructureType.ShaderModuleCreateInfo,
                        CodeSize = (nuint)OverlayFragmentSpirv.Length,
                        PCode = (uint*)OverlayFragmentPtr
                    };
                    if (VkApi.CreateShaderModule(Device, in OverlayFragmentInfo, null, out OverlayFragmentShader) != Result.Success)
                    {
                        FailureReason = "Failed to create overlay fragment shader module.";
                        return null;
                    }
                }

                fixed (byte* TexturedVertexPtr = TexturedVertexSpirv)
                {
                    ShaderModuleCreateInfo TexturedVertexInfo = new ShaderModuleCreateInfo
                    {
                        SType = StructureType.ShaderModuleCreateInfo,
                        CodeSize = (nuint)TexturedVertexSpirv.Length,
                        PCode = (uint*)TexturedVertexPtr
                    };
                    if (VkApi.CreateShaderModule(Device, in TexturedVertexInfo, null, out TexturedVertexShader) != Result.Success)
                    {
                        FailureReason = "Failed to create textured vertex shader module.";
                        return null;
                    }
                }

                fixed (byte* TexturedFragmentPtr = TexturedFragmentSpirv)
                {
                    ShaderModuleCreateInfo TexturedFragmentInfo = new ShaderModuleCreateInfo
                    {
                        SType = StructureType.ShaderModuleCreateInfo,
                        CodeSize = (nuint)TexturedFragmentSpirv.Length,
                        PCode = (uint*)TexturedFragmentPtr
                    };
                    if (VkApi.CreateShaderModule(Device, in TexturedFragmentInfo, null, out TexturedFragmentShader) != Result.Success)
                    {
                        FailureReason = "Failed to create textured fragment shader module.";
                        return null;
                    }
                }

                fixed (byte* PlanVertexPtr = PlanVertexSpirv)
                {
                    ShaderModuleCreateInfo PlanVertexInfo = new ShaderModuleCreateInfo
                    {
                        SType = StructureType.ShaderModuleCreateInfo,
                        CodeSize = (nuint)PlanVertexSpirv.Length,
                        PCode = (uint*)PlanVertexPtr
                    };
                    if (VkApi.CreateShaderModule(Device, in PlanVertexInfo, null, out PlanVertexShader) != Result.Success)
                    {
                        FailureReason = "Failed to create plan-view vertex shader module.";
                        return null;
                    }
                }

                fixed (byte* PlanTexturedVertexPtr = PlanTexturedVertexSpirv)
                {
                    ShaderModuleCreateInfo PlanTexturedVertexInfo = new ShaderModuleCreateInfo
                    {
                        SType = StructureType.ShaderModuleCreateInfo,
                        CodeSize = (nuint)PlanTexturedVertexSpirv.Length,
                        PCode = (uint*)PlanTexturedVertexPtr
                    };
                    if (VkApi.CreateShaderModule(Device, in PlanTexturedVertexInfo, null, out PlanTexturedVertexShader) != Result.Success)
                    {
                        FailureReason = "Failed to create plan-view textured vertex shader module.";
                        return null;
                    }
                }

                DescriptorSetLayoutBinding[] Bindings =
                [
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 0,
                        DescriptorType = DescriptorType.UniformBuffer,
                        DescriptorCount = 1,
                        StageFlags = ShaderStageFlags.VertexBit
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        DescriptorCount = 1,
                        StageFlags = ShaderStageFlags.FragmentBit
                    }
                ];
                fixed (DescriptorSetLayoutBinding* BindingsPtr = Bindings)
                {
                    DescriptorSetLayoutCreateInfo LayoutInfo = new DescriptorSetLayoutCreateInfo
                    {
                        SType = StructureType.DescriptorSetLayoutCreateInfo,
                        BindingCount = (uint)Bindings.Length,
                        PBindings = BindingsPtr
                    };
                    if (VkApi.CreateDescriptorSetLayout(Device, in LayoutInfo, null, out DescriptorSetLayout) != Result.Success)
                    {
                        FailureReason = "Failed to create descriptor set layout.";
                        return null;
                    }
                }

                DescriptorPoolSize[] PoolSizes =
                [
                    new DescriptorPoolSize
                    {
                        Type = DescriptorType.UniformBuffer,
                        DescriptorCount = 1
                    },
                    new DescriptorPoolSize
                    {
                        Type = DescriptorType.CombinedImageSampler,
                        DescriptorCount = 1
                    }
                ];
                fixed (DescriptorPoolSize* PoolSizesPtr = PoolSizes)
                {
                    DescriptorPoolCreateInfo PoolInfo = new DescriptorPoolCreateInfo
                    {
                        SType = StructureType.DescriptorPoolCreateInfo,
                        MaxSets = 1,
                        PoolSizeCount = (uint)PoolSizes.Length,
                        PPoolSizes = PoolSizesPtr
                    };
                    if (VkApi.CreateDescriptorPool(Device, in PoolInfo, null, out DescriptorPool) != Result.Success)
                    {
                        FailureReason = "Failed to create descriptor pool.";
                        return null;
                    }
                }

                DescriptorSetAllocateInfo AllocateInfo = new DescriptorSetAllocateInfo
                {
                    SType = StructureType.DescriptorSetAllocateInfo,
                    DescriptorPool = DescriptorPool,
                    DescriptorSetCount = 1,
                    PSetLayouts = &DescriptorSetLayout
                };
                if (VkApi.AllocateDescriptorSets(Device, in AllocateInfo, out DescriptorSet DescriptorSet) != Result.Success)
                {
                    FailureReason = "Failed to allocate descriptor set.";
                    return null;
                }

                PushConstantRange PushConstant = new PushConstantRange
                {
                    StageFlags = ShaderStageFlags.FragmentBit,
                    Offset = 0,
                    Size = VulkanConstants.PUSH_CONSTANT_OVERLAY_OPACITY_SIZE_BYTES
                };
                PipelineLayoutCreateInfo PipelineLayoutInfo = new PipelineLayoutCreateInfo
                {
                    SType = StructureType.PipelineLayoutCreateInfo,
                    SetLayoutCount = 1,
                    PSetLayouts = &DescriptorSetLayout,
                    PushConstantRangeCount = 1,
                    PPushConstantRanges = &PushConstant
                };
                if (VkApi.CreatePipelineLayout(Device, in PipelineLayoutInfo, null, out PipelineLayout) != Result.Success)
                {
                    FailureReason = "Failed to create pipeline layout.";
                    return null;
                }

                if (!TryCreateRenderPass(VkApi, Device, out RenderPass, out FailureReason))
                    return null;

                TrianglePipeline = CreateGraphicsPipeline(
                    VkApi,
                    Device,
                    RenderPass,
                    PipelineLayout,
                    VertexShader,
                    FragmentShader,
                    PrimitiveTopology.TriangleList,
                    PolygonMode.Fill,
                    VulkanConstants.DEFAULT_LINE_WIDTH);
                MeshTrianglePipeline = CreateGraphicsPipeline(
                    VkApi,
                    Device,
                    RenderPass,
                    PipelineLayout,
                    VertexShader,
                    OverlayFragmentShader,
                    PrimitiveTopology.TriangleList,
                    PolygonMode.Fill,
                    VulkanConstants.DEFAULT_LINE_WIDTH,
                    EnableAlphaBlend: true);
                LinePipeline = CreateGraphicsPipeline(
                    VkApi,
                    Device,
                    RenderPass,
                    PipelineLayout,
                    VertexShader,
                    FragmentShader,
                    PrimitiveTopology.LineList,
                    PolygonMode.Fill,
                    VulkanConstants.OVERLAY_LINE_WIDTH);
                OverlayLinePipeline = CreateGraphicsPipeline(
                    VkApi,
                    Device,
                    RenderPass,
                    PipelineLayout,
                    VertexShader,
                    OverlayFragmentShader,
                    PrimitiveTopology.LineList,
                    PolygonMode.Fill,
                    VulkanConstants.OVERLAY_LINE_WIDTH,
                    EnableAlphaBlend: true);
                TexturedPipeline = CreateTexturedGraphicsPipeline(
                    VkApi,
                    Device,
                    RenderPass,
                    PipelineLayout,
                    TexturedVertexShader,
                    TexturedFragmentShader);

                PlanTrianglePipeline = CreateGraphicsPipeline(
                    VkApi,
                    Device,
                    RenderPass,
                    PipelineLayout,
                    PlanVertexShader,
                    FragmentShader,
                    PrimitiveTopology.TriangleList,
                    PolygonMode.Fill,
                    VulkanConstants.DEFAULT_LINE_WIDTH);
                PlanMeshTrianglePipeline = CreateGraphicsPipeline(
                    VkApi,
                    Device,
                    RenderPass,
                    PipelineLayout,
                    PlanVertexShader,
                    OverlayFragmentShader,
                    PrimitiveTopology.TriangleList,
                    PolygonMode.Fill,
                    VulkanConstants.DEFAULT_LINE_WIDTH,
                    EnableAlphaBlend: true);
                PlanLinePipeline = CreateGraphicsPipeline(
                    VkApi,
                    Device,
                    RenderPass,
                    PipelineLayout,
                    PlanVertexShader,
                    FragmentShader,
                    PrimitiveTopology.LineList,
                    PolygonMode.Fill,
                    VulkanConstants.OVERLAY_LINE_WIDTH);
                PlanOverlayLinePipeline = CreateGraphicsPipeline(
                    VkApi,
                    Device,
                    RenderPass,
                    PipelineLayout,
                    PlanVertexShader,
                    OverlayFragmentShader,
                    PrimitiveTopology.LineList,
                    PolygonMode.Fill,
                    VulkanConstants.OVERLAY_LINE_WIDTH,
                    EnableAlphaBlend: true);
                PlanTexturedPipeline = CreateTexturedGraphicsPipeline(
                    VkApi,
                    Device,
                    RenderPass,
                    PipelineLayout,
                    PlanTexturedVertexShader,
                    TexturedFragmentShader);

                SamplerCreateInfo SamplerInfo = new SamplerCreateInfo
                {
                    SType = StructureType.SamplerCreateInfo,
                    MagFilter = Filter.Linear,
                    MinFilter = Filter.Linear,
                    AddressModeU = SamplerAddressMode.ClampToEdge,
                    AddressModeV = SamplerAddressMode.ClampToEdge,
                    AddressModeW = SamplerAddressMode.ClampToEdge
                };
                if (VkApi.CreateSampler(Device, in SamplerInfo, null, out Sampler) != Result.Success)
                {
                    FailureReason = "Failed to create sampler.";
                    return null;
                }

                return new VulkanOffscreenRenderer(
                    Session,
                    VertexShader,
                    FragmentShader,
                    OverlayFragmentShader,
                    TexturedVertexShader,
                    TexturedFragmentShader,
                    PipelineLayout,
                    DescriptorSetLayout,
                    DescriptorPool,
                    DescriptorSet,
                    TrianglePipeline,
                    MeshTrianglePipeline,
                    TexturedPipeline,
                    LinePipeline,
                    OverlayLinePipeline,
                    PlanVertexShader,
                    PlanTexturedVertexShader,
                    PlanTrianglePipeline,
                    PlanMeshTrianglePipeline,
                    PlanTexturedPipeline,
                    PlanLinePipeline,
                    PlanOverlayLinePipeline,
                    RenderPass,
                    Sampler);
            }
            catch (Exception Ex)
            {
                FailureReason = Ex.Message;
                if (Sampler.Handle != 0)
                    VkApi.DestroySampler(Device, Sampler, null);
                if (OverlayLinePipeline.Handle != 0)
                    VkApi.DestroyPipeline(Device, OverlayLinePipeline, null);
                if (PlanOverlayLinePipeline.Handle != 0)
                    VkApi.DestroyPipeline(Device, PlanOverlayLinePipeline, null);
                if (PlanLinePipeline.Handle != 0)
                    VkApi.DestroyPipeline(Device, PlanLinePipeline, null);
                if (PlanTexturedPipeline.Handle != 0)
                    VkApi.DestroyPipeline(Device, PlanTexturedPipeline, null);
                if (PlanMeshTrianglePipeline.Handle != 0)
                    VkApi.DestroyPipeline(Device, PlanMeshTrianglePipeline, null);
                if (PlanTrianglePipeline.Handle != 0)
                    VkApi.DestroyPipeline(Device, PlanTrianglePipeline, null);
                if (LinePipeline.Handle != 0)
                    VkApi.DestroyPipeline(Device, LinePipeline, null);
                if (TexturedPipeline.Handle != 0)
                    VkApi.DestroyPipeline(Device, TexturedPipeline, null);
                if (MeshTrianglePipeline.Handle != 0)
                    VkApi.DestroyPipeline(Device, MeshTrianglePipeline, null);
                if (TrianglePipeline.Handle != 0)
                    VkApi.DestroyPipeline(Device, TrianglePipeline, null);
                if (RenderPass.Handle != 0)
                    VkApi.DestroyRenderPass(Device, RenderPass, null);
                if (PipelineLayout.Handle != 0)
                    VkApi.DestroyPipelineLayout(Device, PipelineLayout, null);
                if (DescriptorPool.Handle != 0)
                    VkApi.DestroyDescriptorPool(Device, DescriptorPool, null);
                if (DescriptorSetLayout.Handle != 0)
                    VkApi.DestroyDescriptorSetLayout(Device, DescriptorSetLayout, null);
                if (FragmentShader.Handle != 0)
                    VkApi.DestroyShaderModule(Device, FragmentShader, null);
                if (OverlayFragmentShader.Handle != 0)
                    VkApi.DestroyShaderModule(Device, OverlayFragmentShader, null);
                if (TexturedFragmentShader.Handle != 0)
                    VkApi.DestroyShaderModule(Device, TexturedFragmentShader, null);
                if (TexturedVertexShader.Handle != 0)
                    VkApi.DestroyShaderModule(Device, TexturedVertexShader, null);
                if (PlanTexturedVertexShader.Handle != 0)
                    VkApi.DestroyShaderModule(Device, PlanTexturedVertexShader, null);
                if (PlanVertexShader.Handle != 0)
                    VkApi.DestroyShaderModule(Device, PlanVertexShader, null);
                if (VertexShader.Handle != 0)
                    VkApi.DestroyShaderModule(Device, VertexShader, null);
                return null;
            }
        }

        /// <summary>
        /// Renders one field frame and returns BGRA8888 premultiplied pixels (row-major).
        /// </summary>
        /// <param name="Frame">CPU-side geometry and camera state to render.</param>
        /// <param name="Width">Render target width in pixels.</param>
        /// <param name="Height">Render target height in pixels.</param>
        /// <param name="FailureReason">Receives a human-readable failure message when rendering fails.</param>
        /// <returns>Readback pixel buffer, or null on failure.</returns>
        public byte[]? TryRender
            (
            GpuFieldFrame Frame,
            int Width,
            int Height,
            out string? FailureReason
            )
        {
            FailureReason = null;
            if (Width <= 0 || Height <= 0)
            {
                FailureReason = "Invalid render target size.";
                return null;
            }

            try
            {
                EnsureTarget((uint)Width, (uint)Height);
                EnsureGpuTileCacheGeneration(Frame.SatelliteCacheGeneration);
                UploadUniforms(Frame.Camera);
                UploadVerticesIfChanged(Frame.BackgroundMeshVertices, ref _UploadedBackgroundSource, ref _BackgroundVertexBuffer, ref _BackgroundVertexMemory, ref _BackgroundVertexCapacity);
                UploadVerticesIfChanged(Frame.MeshVertices, ref _UploadedMeshSource, ref _MeshVertexBuffer, ref _MeshVertexMemory, ref _MeshVertexCapacity);
                UploadVerticesIfChanged(Frame.LineVertices, ref _UploadedLineSource, ref _LineVertexBuffer, ref _LineVertexMemory, ref _LineVertexCapacity);
                UploadVerticesIfChanged(Frame.BorderLineVertices, ref _UploadedBorderLineSource, ref _BorderLineVertexBuffer, ref _BorderLineVertexMemory, ref _BorderLineVertexCapacity);
                UploadTexturedVerticesIfChanged(Frame.CombinedTextureVertices);

                ulong StagingBytesRequired = 0;
                List<(GpuTextureMesh Mesh, ulong StagingOffset)> PendingTileUploads = new List<(GpuTextureMesh, ulong)>();
                foreach (GpuTextureDrawSpec DrawSpec in Frame.TextureDraws)
                {
                    GpuTextureMesh Mesh = Frame.TextureMeshes[DrawSpec.MeshIndex];
                    if (Mesh.Pixels.Length == 0)
                        continue;

                    if (_GpuTileCache.ContainsKey((Mesh.TileZoom, Mesh.TileX, Mesh.TileY)))
                        continue;

                    PendingTileUploads.Add((Mesh, StagingBytesRequired));
                    StagingBytesRequired += (ulong)Mesh.Pixels.Length;
                }

                if (StagingBytesRequired > 0)
                {
                    EnsureTileStagingBuffer(StagingBytesRequired);
                    StagePendingTilePixels(PendingTileUploads);
                }

                _Vk.WaitForFences(_Device, 1, in _Fence, true, ulong.MaxValue);
                _Vk.ResetFences(_Device, 1, in _Fence);
                _Vk.ResetCommandPool(_Device, _CommandPool, 0);

                CommandBufferAllocateInfo CommandAllocateInfo = new CommandBufferAllocateInfo
                {
                    SType = StructureType.CommandBufferAllocateInfo,
                    CommandPool = _CommandPool,
                    Level = CommandBufferLevel.Primary,
                    CommandBufferCount = 1
                };
                if (_Vk.AllocateCommandBuffers(_Device, in CommandAllocateInfo, out CommandBuffer CommandBuffer) != Result.Success)
                {
                    FailureReason = "Failed to allocate command buffer.";
                    return null;
                }

                CommandBufferBeginInfo BeginInfo = new CommandBufferBeginInfo
                {
                    SType = StructureType.CommandBufferBeginInfo,
                    Flags = CommandBufferUsageFlags.OneTimeSubmitBit
                };
                _Vk.BeginCommandBuffer(CommandBuffer, in BeginInfo);

                ClearValue ClearColor = new ClearValue
                {
                    Color = new ClearColorValue(Frame.ClearR, Frame.ClearG, Frame.ClearB, Frame.ClearA)
                };
                ClearValue ClearDepth = new ClearValue { DepthStencil = new ClearDepthStencilValue(VulkanConstants.DEPTH_CLEAR_VALUE, 0) };
                ClearValue* ClearsPtr = stackalloc ClearValue[(int)VulkanConstants.CLEAR_VALUE_COUNT];
                ClearsPtr[0] = ClearColor;
                ClearsPtr[1] = ClearDepth;

                RenderPassBeginInfo RenderPassInfo = new RenderPassBeginInfo
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _RenderPass,
                    Framebuffer = _Framebuffer,
                    RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D(_Width, _Height)),
                    ClearValueCount = VulkanConstants.CLEAR_VALUE_COUNT,
                    PClearValues = ClearsPtr
                };
                _Vk.CmdBeginRenderPass(CommandBuffer, in RenderPassInfo, SubpassContents.Inline);

                Viewport Viewport = new Viewport(0, 0, _Width, _Height, VulkanConstants.VIEWPORT_MIN_DEPTH, VulkanConstants.VIEWPORT_MAX_DEPTH);
                Rect2D Scissor = new Rect2D(new Offset2D(0, 0), new Extent2D(_Width, _Height));
                _Vk.CmdSetViewport(CommandBuffer, 0, 1, in Viewport);
                _Vk.CmdSetScissor(CommandBuffer, 0, 1, in Scissor);

                _Vk.CmdBindDescriptorSets(CommandBuffer, PipelineBindPoint.Graphics, _PipelineLayout, 0, 1, in _DescriptorSet, 0, null);

                Pipeline ActiveTrianglePipeline = Frame.UsePlanViewProjection ? _PlanTrianglePipeline : _TrianglePipeline;
                Pipeline ActiveMeshPipeline = Frame.UsePlanViewProjection ? _PlanMeshTrianglePipeline : _MeshTrianglePipeline;
                Pipeline ActiveTexturedPipeline = Frame.UsePlanViewProjection ? _PlanTexturedPipeline : _TexturedPipeline;
                Pipeline ActiveLinePipeline = Frame.UsePlanViewProjection ? _PlanLinePipeline : _LinePipeline;
                Pipeline ActiveOverlayLinePipeline = Frame.UsePlanViewProjection ? _PlanOverlayLinePipeline : _OverlayLinePipeline;

                if (Frame.BackgroundMeshVertices.Length > 0)
                {
                    _Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, ActiveTrianglePipeline);
                    ulong BackgroundOffset = 0;
                    _Vk.CmdBindVertexBuffers(CommandBuffer, 0, 1, in _BackgroundVertexBuffer, in BackgroundOffset);
                    _Vk.CmdDraw(CommandBuffer, (uint)Frame.BackgroundMeshVertices.Length, 1, 0, 0);
                }

                foreach (GpuTextureDrawSpec DrawSpec in Frame.TextureDraws)
                {
                    GpuTextureMesh Mesh = Frame.TextureMeshes[DrawSpec.MeshIndex];
                    if (Mesh.Pixels.Length == 0 || DrawSpec.VertexCount == 0)
                        continue;

                    (GpuTextureMesh Mesh, ulong StagingOffset)? PendingUpload = null;
                    foreach ((GpuTextureMesh PendingMesh, ulong Offset) in PendingTileUploads)
                    {
                        if (PendingMesh.TileZoom == Mesh.TileZoom
                            && PendingMesh.TileX == Mesh.TileX
                            && PendingMesh.TileY == Mesh.TileY)
                        {
                            PendingUpload = (PendingMesh, Offset);
                            break;
                        }
                    }

                    GpuCachedTileTexture CachedTile = GetOrCreateGpuTileTexture(Mesh);
                    if (PendingUpload.HasValue)
                        RecordGpuTileTextureUpload(CommandBuffer, CachedTile, PendingUpload.Value.StagingOffset);
                    else
                        EnsureGpuTileShaderRead(CommandBuffer, CachedTile);

                    UpdateTileTextureDescriptor(CachedTile.View);

                    _Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, ActiveTexturedPipeline);
                    _Vk.CmdBindDescriptorSets(CommandBuffer, PipelineBindPoint.Graphics, _PipelineLayout, 0, 1, in _DescriptorSet, 0, null);
                    ulong TexturedOffset = 0;
                    _Vk.CmdBindVertexBuffers(CommandBuffer, 0, 1, in _TexturedVertexBuffer, in TexturedOffset);
                    _Vk.CmdDraw(CommandBuffer, DrawSpec.VertexCount, 1, DrawSpec.FirstVertex, 0);
                }

                if (Frame.MeshVertices.Length > 0 && Frame.SurfaceOverlayOpacity > VulkanConstants.OVERLAY_OPACITY_MIN)
                {
                    float OverlayOpacity = Math.Clamp(
                        Frame.SurfaceOverlayOpacity,
                        VulkanConstants.OVERLAY_OPACITY_MIN,
                        VulkanConstants.OVERLAY_OPACITY_MAX);
                    _Vk.CmdPushConstants(
                        CommandBuffer,
                        _PipelineLayout,
                        ShaderStageFlags.FragmentBit,
                        0,
                        VulkanConstants.PUSH_CONSTANT_OVERLAY_OPACITY_SIZE_BYTES,
                        &OverlayOpacity);
                    _Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, ActiveMeshPipeline);
                    ulong MeshOffset = 0;
                    _Vk.CmdBindVertexBuffers(CommandBuffer, 0, 1, in _MeshVertexBuffer, in MeshOffset);
                    _Vk.CmdDraw(CommandBuffer, (uint)Frame.MeshVertices.Length, 1, 0, 0);
                }

                if (Frame.LineVertices.Length > 0 && Frame.SurfaceOverlayOpacity > VulkanConstants.OVERLAY_OPACITY_MIN)
                {
                    float OverlayOpacity = Math.Clamp(
                        Frame.SurfaceOverlayOpacity,
                        VulkanConstants.OVERLAY_OPACITY_MIN,
                        VulkanConstants.OVERLAY_OPACITY_MAX);
                    _Vk.CmdPushConstants(
                        CommandBuffer,
                        _PipelineLayout,
                        ShaderStageFlags.FragmentBit,
                        0,
                        VulkanConstants.PUSH_CONSTANT_OVERLAY_OPACITY_SIZE_BYTES,
                        &OverlayOpacity);
                    _Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, ActiveOverlayLinePipeline);
                    ulong LineOffset = 0;
                    _Vk.CmdBindVertexBuffers(CommandBuffer, 0, 1, in _LineVertexBuffer, in LineOffset);
                    _Vk.CmdDraw(CommandBuffer, (uint)Frame.LineVertices.Length, 1, 0, 0);
                }

                if (Frame.BorderLineVertices.Length > 0)
                {
                    _Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, ActiveLinePipeline);
                    ulong BorderLineOffset = 0;
                    _Vk.CmdBindVertexBuffers(CommandBuffer, 0, 1, in _BorderLineVertexBuffer, in BorderLineOffset);
                    _Vk.CmdDraw(CommandBuffer, (uint)Frame.BorderLineVertices.Length, 1, 0, 0);
                }

                _Vk.CmdEndRenderPass(CommandBuffer);

                ImageMemoryBarrier ToTransfer = new ImageMemoryBarrier
                {
                    SType = StructureType.ImageMemoryBarrier,
                    OldLayout = ImageLayout.ColorAttachmentOptimal,
                    NewLayout = ImageLayout.TransferSrcOptimal,
                    SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                    DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                    Image = _ColorImage,
                    SubresourceRange = new ImageSubresourceRange
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        BaseMipLevel = 0,
                        LevelCount = 1,
                        BaseArrayLayer = 0,
                        LayerCount = 1
                    },
                    SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
                    DstAccessMask = AccessFlags.TransferReadBit
                };
                _Vk.CmdPipelineBarrier(
                    CommandBuffer,
                    PipelineStageFlags.ColorAttachmentOutputBit,
                    PipelineStageFlags.TransferBit,
                    0,
                    0,
                    null,
                    0,
                    null,
                    1,
                    in ToTransfer);

                BufferImageCopy Region = new BufferImageCopy
                {
                    BufferOffset = 0,
                    BufferRowLength = 0,
                    BufferImageHeight = 0,
                    ImageSubresource = new ImageSubresourceLayers
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1
                    },
                    ImageOffset = new Offset3D(0, 0, 0),
                    ImageExtent = new Extent3D(_Width, _Height, 1)
                };
                _Vk.CmdCopyImageToBuffer(CommandBuffer, _ColorImage, ImageLayout.TransferSrcOptimal, _ReadbackBuffer, 1, in Region);

                _Vk.EndCommandBuffer(CommandBuffer);

                SubmitInfo SubmitInfo = new SubmitInfo
                {
                    SType = StructureType.SubmitInfo,
                    CommandBufferCount = 1,
                    PCommandBuffers = &CommandBuffer
                };
                if (_Vk.QueueSubmit(_Queue, 1, in SubmitInfo, _Fence) != Result.Success)
                {
                    _Vk.FreeCommandBuffers(_Device, _CommandPool, 1, in CommandBuffer);
                    FailureReason = "Queue submit failed.";
                    return null;
                }

                if (_Vk.WaitForFences(_Device, 1, in _Fence, true, VulkanConstants.FENCE_WAIT_TIMEOUT_NS) != Result.Success)
                {
                    _Vk.FreeCommandBuffers(_Device, _CommandPool, 1, in CommandBuffer);
                    FailureReason = "GPU render timed out.";
                    return null;
                }

                _Vk.FreeCommandBuffers(_Device, _CommandPool, 1, in CommandBuffer);

                void* Mapped = null;
                _Vk.MapMemory(_Device, _ReadbackMemory, 0, _ReadbackSize, 0, &Mapped);
                int PixelBytes = (int)_ReadbackSize;
                if (_ReadbackPixels == null || _ReadbackPixels.Length != PixelBytes)
                    _ReadbackPixels = new byte[PixelBytes];
                Marshal.Copy((nint)Mapped, _ReadbackPixels, 0, PixelBytes);
                _Vk.UnmapMemory(_Device, _ReadbackMemory);
                return _ReadbackPixels;
            }
            catch (Exception Ex)
            {
                FailureReason = Ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Renders multiple field frames with one GPU fence wait (one queue submission).
        /// </summary>
        public byte[]?[]? TryRenderBatch
            (
            IReadOnlyList<GpuFieldFrame> Frames,
            int Width,
            int Height,
            out string? FailureReason
            )
        {
            FailureReason = null;
            if (Frames.Count == 0)
            {
                FailureReason = "No frames to render.";
                return null;
            }

            if (Frames.Count == 1)
            {
                byte[]? Single = TryRender(Frames[0], Width, Height, out FailureReason);
                return Single == null ? null : new byte[]?[] { Single };
            }

            byte[]?[] Results = new byte[]?[Frames.Count];
            for (int I = 0; I < Frames.Count; I++)
            {
                Results[I] = TryRender(Frames[I], Width, Height, out FailureReason);
                if (Results[I] == null)
                    return null;
            }

            return Results;
        }

        /// <summary>
        /// Releases GPU images, buffers, pipelines, shaders, and tile textures owned by this renderer.
        /// </summary>
        public void Dispose
            (
            )
        {
            if (_Disposed)
                return;

            _Disposed = true;
            _Vk.DeviceWaitIdle(_Device);
            ClearGpuTileCache();
            DestroyTargetResources();
            DestroyBuffer(ref _MeshVertexBuffer, ref _MeshVertexMemory);
            DestroyBuffer(ref _BackgroundVertexBuffer, ref _BackgroundVertexMemory);
            DestroyBuffer(ref _LineVertexBuffer, ref _LineVertexMemory);
            DestroyBuffer(ref _BorderLineVertexBuffer, ref _BorderLineVertexMemory);
            DestroyBuffer(ref _TexturedVertexBuffer, ref _TexturedVertexMemory);
            DestroyBuffer(ref _TileStagingBuffer, ref _TileStagingMemory);
            DestroyBuffer(ref _UniformBuffer, ref _UniformMemory);
            _Vk.DestroySampler(_Device, _Sampler, null);
            _Vk.DestroyPipeline(_Device, _PlanOverlayLinePipeline, null);
            _Vk.DestroyPipeline(_Device, _PlanLinePipeline, null);
            _Vk.DestroyPipeline(_Device, _PlanTexturedPipeline, null);
            _Vk.DestroyPipeline(_Device, _PlanMeshTrianglePipeline, null);
            _Vk.DestroyPipeline(_Device, _PlanTrianglePipeline, null);
            _Vk.DestroyPipeline(_Device, _OverlayLinePipeline, null);
            _Vk.DestroyPipeline(_Device, _LinePipeline, null);
            _Vk.DestroyPipeline(_Device, _TexturedPipeline, null);
            _Vk.DestroyPipeline(_Device, _MeshTrianglePipeline, null);
            _Vk.DestroyPipeline(_Device, _TrianglePipeline, null);
            _Vk.DestroyRenderPass(_Device, _RenderPass, null);
            _Vk.DestroyPipelineLayout(_Device, _PipelineLayout, null);
            _Vk.DestroyDescriptorPool(_Device, _DescriptorPool, null);
            _Vk.DestroyDescriptorSetLayout(_Device, _DescriptorSetLayout, null);
            _Vk.DestroyShaderModule(_Device, _PlanTexturedVertexShader, null);
            _Vk.DestroyShaderModule(_Device, _PlanVertexShader, null);
            _Vk.DestroyShaderModule(_Device, _TexturedFragmentShader, null);
            _Vk.DestroyShaderModule(_Device, _TexturedVertexShader, null);
            _Vk.DestroyShaderModule(_Device, _OverlayFragmentShader, null);
            _Vk.DestroyShaderModule(_Device, _FragmentShader, null);
            _Vk.DestroyShaderModule(_Device, _VertexShader, null);
        }

        /// <summary>
        /// Recreates offscreen colour, depth, framebuffer, and readback resources when the target size changes.
        /// </summary>
        /// <param name="Width">Render target width in pixels.</param>
        /// <param name="Height">Render target height in pixels.</param>
        private void EnsureTarget
            (
            uint Width,
            uint Height
            )
        {
            if (_Width == Width && _Height == Height && _Framebuffer.Handle != 0)
                return;

            _Vk.DeviceWaitIdle(_Device);
            DestroyTargetResources();

            _Width = Width;
            _Height = Height;

            _ColorImage = VulkanMemoryUtilities.CreateImage(
                _Vk,
                _Device,
                _PhysicalDevice,
                Width,
                Height,
                ColorFormat,
                ImageTiling.Optimal,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
                MemoryPropertyFlags.DeviceLocalBit,
                out _ColorMemory);
            _ColorView = VulkanMemoryUtilities.CreateImageView(_Vk, _Device, _ColorImage, ColorFormat, ImageAspectFlags.ColorBit);

            _DepthImage = VulkanMemoryUtilities.CreateImage(
                _Vk,
                _Device,
                _PhysicalDevice,
                Width,
                Height,
                DepthFormat,
                ImageTiling.Optimal,
                ImageUsageFlags.DepthStencilAttachmentBit,
                MemoryPropertyFlags.DeviceLocalBit,
                out _DepthMemory);
            _DepthView = VulkanMemoryUtilities.CreateImageView(_Vk, _Device, _DepthImage, DepthFormat, ImageAspectFlags.DepthBit);

            ImageView[] Attachments = { _ColorView, _DepthView };
            fixed (ImageView* AttachmentsPtr = Attachments)
            {
                FramebufferCreateInfo FramebufferInfo = new FramebufferCreateInfo
                {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = _RenderPass,
                    AttachmentCount = VulkanConstants.RENDER_PASS_ATTACHMENT_COUNT,
                    PAttachments = AttachmentsPtr,
                    Width = Width,
                    Height = Height,
                    Layers = 1
                };
                if (_Vk.CreateFramebuffer(_Device, in FramebufferInfo, null, out _Framebuffer) != Result.Success)
                    throw new InvalidOperationException("Failed to create framebuffer.");
            }

            ulong RowPitch = Width * (ulong)VulkanConstants.BGRA_BYTES_PER_PIXEL;
            _ReadbackSize = RowPitch * Height;
            DestroyBuffer(ref _ReadbackBuffer, ref _ReadbackMemory);
            _ReadbackBuffer = VulkanMemoryUtilities.CreateBuffer(
                _Vk,
                _Device,
                _PhysicalDevice,
                _ReadbackSize,
                BufferUsageFlags.TransferDstBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                out _ReadbackMemory);
        }

        /// <summary>
        /// Destroys the current framebuffer, attachments, and readback buffer.
        /// </summary>
        private void DestroyTargetResources
            (
            )
        {
            if (_Framebuffer.Handle != 0)
            {
                _Vk.DestroyFramebuffer(_Device, _Framebuffer, null);
                _Framebuffer = default;
            }

            if (_ColorView.Handle != 0)
            {
                _Vk.DestroyImageView(_Device, _ColorView, null);
                _ColorView = default;
            }

            if (_ColorImage.Handle != 0)
            {
                _Vk.DestroyImage(_Device, _ColorImage, null);
                _Vk.FreeMemory(_Device, _ColorMemory, null);
                _ColorImage = default;
                _ColorMemory = default;
            }

            if (_DepthView.Handle != 0)
            {
                _Vk.DestroyImageView(_Device, _DepthView, null);
                _DepthView = default;
            }

            if (_DepthImage.Handle != 0)
            {
                _Vk.DestroyImage(_Device, _DepthImage, null);
                _Vk.FreeMemory(_Device, _DepthMemory, null);
                _DepthImage = default;
                _DepthMemory = default;
            }

            DestroyBuffer(ref _ReadbackBuffer, ref _ReadbackMemory);
            _ReadbackSize = 0;
            _Width = 0;
            _Height = 0;
        }

        /// <summary>
        /// Maps and writes the camera uniform buffer for the next draw.
        /// </summary>
        /// <param name="Uniforms">Camera parameters to upload.</param>
        private void UploadUniforms
            (
            CameraGpuUniforms Uniforms
            )
        {
            void* Mapped = null;
            _Vk.MapMemory(_Device, _UniformMemory, 0, (ulong)Marshal.SizeOf<CameraGpuUniforms>(), 0, &Mapped);
            float* Floats = (float*)Mapped;
            Floats[VulkanConstants.CAMERA_UNIFORM_FLOAT_VIEWPORT_WIDTH] = Uniforms.ViewportWidth;
            Floats[VulkanConstants.CAMERA_UNIFORM_FLOAT_VIEWPORT_HEIGHT] = Uniforms.ViewportHeight;
            Floats[VulkanConstants.CAMERA_UNIFORM_FLOAT_VIEWPORT_MID_X] = Uniforms.ViewportMidX;
            Floats[VulkanConstants.CAMERA_UNIFORM_FLOAT_VIEWPORT_MID_Y] = Uniforms.ViewportMidY;
            Floats[VulkanConstants.CAMERA_UNIFORM_FLOAT_SCALE] = Uniforms.Scale;
            Floats[VulkanConstants.CAMERA_UNIFORM_FLOAT_COS_H] = Uniforms.CosH;
            Floats[VulkanConstants.CAMERA_UNIFORM_FLOAT_SIN_H] = Uniforms.SinH;
            Floats[VulkanConstants.CAMERA_UNIFORM_FLOAT_COS_P] = Uniforms.CosP;
            Floats[VulkanConstants.CAMERA_UNIFORM_FLOAT_SIN_P] = Uniforms.SinP;
            Floats[VulkanConstants.CAMERA_UNIFORM_FLOAT_DEPTH_MIN] = Uniforms.DepthMin;
            Floats[VulkanConstants.CAMERA_UNIFORM_FLOAT_DEPTH_MAX] = Uniforms.DepthMax;
            _Vk.UnmapMemory(_Device, _UniformMemory);
        }

        /// <summary>
        /// Resizes and uploads a coloured vertex buffer when capacity is insufficient.
        /// </summary>
        /// <param name="Vertices">Source vertices to copy to device memory.</param>
        /// <param name="Buffer">Vertex buffer handle updated when reallocated.</param>
        /// <param name="Memory">Backing device memory handle updated when reallocated.</param>
        /// <param name="CapacityBytes">Current buffer capacity in bytes.</param>
        private void UploadVertices
            (
            GpuVertex[] Vertices,
            ref VkBuffer Buffer,
            ref DeviceMemory Memory,
            ref ulong CapacityBytes
            )
        {
            ulong RequiredBytes = (ulong)(Vertices.Length * Marshal.SizeOf<GpuVertex>());
            if (RequiredBytes > CapacityBytes)
            {
                DestroyBuffer(ref Buffer, ref Memory);
                if (RequiredBytes == 0)
                {
                    CapacityBytes = 0;
                    return;
                }

                Buffer = VulkanMemoryUtilities.CreateBuffer(
                    _Vk,
                    _Device,
                    _PhysicalDevice,
                    RequiredBytes,
                    BufferUsageFlags.VertexBufferBit,
                    MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                    out Memory);
                CapacityBytes = RequiredBytes;
            }

            if (RequiredBytes == 0)
                return;

            void* Mapped = null;
            _Vk.MapMemory(_Device, Memory, 0, RequiredBytes, 0, &Mapped);
            fixed (GpuVertex* Source = Vertices)
            {
                System.Buffer.MemoryCopy(Source, Mapped, RequiredBytes, RequiredBytes);
            }

            _Vk.UnmapMemory(_Device, Memory);
        }

        /// <summary>
        /// Resizes and uploads the combined textured vertex buffer when capacity is insufficient.
        /// </summary>
        /// <param name="Vertices">Source textured vertices to copy to device memory.</param>
        private void UploadTexturedVertices
            (
            GpuTexturedVertex[] Vertices
            )
        {
            ulong RequiredBytes = (ulong)(Vertices.Length * Marshal.SizeOf<GpuTexturedVertex>());
            if (RequiredBytes > _TexturedVertexCapacity)
            {
                DestroyBuffer(ref _TexturedVertexBuffer, ref _TexturedVertexMemory);
                if (RequiredBytes == 0)
                {
                    _TexturedVertexCapacity = 0;
                    return;
                }

                _TexturedVertexBuffer = VulkanMemoryUtilities.CreateBuffer(
                    _Vk,
                    _Device,
                    _PhysicalDevice,
                    RequiredBytes,
                    BufferUsageFlags.VertexBufferBit,
                    MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                    out _TexturedVertexMemory);
                _TexturedVertexCapacity = RequiredBytes;
            }

            if (RequiredBytes == 0)
                return;

            void* Mapped = null;
            _Vk.MapMemory(_Device, _TexturedVertexMemory, 0, RequiredBytes, 0, &Mapped);
            fixed (GpuTexturedVertex* Source = Vertices)
            {
                System.Buffer.MemoryCopy(Source, Mapped, RequiredBytes, RequiredBytes);
            }

            _Vk.UnmapMemory(_Device, _TexturedVertexMemory);
        }

        /// <summary>
        /// Uploads coloured vertices only when the source array reference or capacity changed.
        /// </summary>
        /// <param name="Vertices">Source vertices for the draw call.</param>
        /// <param name="UploadedSource">Last uploaded source array reference.</param>
        /// <param name="Buffer">Vertex buffer handle updated when reallocated.</param>
        /// <param name="Memory">Backing device memory handle updated when reallocated.</param>
        /// <param name="CapacityBytes">Current buffer capacity in bytes.</param>
        private void UploadVerticesIfChanged
            (
            GpuVertex[] Vertices,
            ref GpuVertex[]? UploadedSource,
            ref VkBuffer Buffer,
            ref DeviceMemory Memory,
            ref ulong CapacityBytes
            )
        {
            if (ReferenceEquals(Vertices, UploadedSource) && Vertices.Length > 0 && CapacityBytes > 0)
                return;

            UploadVertices(Vertices, ref Buffer, ref Memory, ref CapacityBytes);
            UploadedSource = Vertices;
        }

        /// <summary>
        /// Uploads textured vertices only when the source array reference or capacity changed.
        /// </summary>
        /// <param name="Vertices">Source textured vertices for tile draws.</param>
        private void UploadTexturedVerticesIfChanged
            (
            GpuTexturedVertex[] Vertices
            )
        {
            if (ReferenceEquals(Vertices, _UploadedTexturedSource) && Vertices.Length > 0 && _TexturedVertexCapacity > 0)
                return;

            UploadTexturedVertices(Vertices);
            _UploadedTexturedSource = Vertices;
        }

        /// <summary>
        /// Clears cached GPU tile textures when the satellite cache generation changes.
        /// </summary>
        /// <param name="SatelliteCacheGeneration">Current generation token from <see cref="GpuFieldFrame"/>.</param>
        private void EnsureGpuTileCacheGeneration
            (
            int SatelliteCacheGeneration
            )
        {
            if (_GpuTileCacheGeneration == SatelliteCacheGeneration)
                return;

            _Vk.DeviceWaitIdle(_Device);
            ClearGpuTileCache();
            _GpuTileCacheGeneration = SatelliteCacheGeneration;
        }

        /// <summary>
        /// Grows the host-visible staging buffer used for tile pixel uploads.
        /// </summary>
        /// <param name="TotalBytes">Required staging capacity in bytes.</param>
        private void EnsureTileStagingBuffer
            (
            ulong TotalBytes
            )
        {
            if (TotalBytes <= _TileStagingCapacity)
                return;

            DestroyBuffer(ref _TileStagingBuffer, ref _TileStagingMemory);
            _TileStagingBuffer = VulkanMemoryUtilities.CreateBuffer(
                _Vk,
                _Device,
                _PhysicalDevice,
                TotalBytes,
                BufferUsageFlags.TransferSrcBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                out _TileStagingMemory);
            _TileStagingCapacity = TotalBytes;
        }

        /// <summary>
        /// Copies pending tile pixel arrays into the mapped staging buffer.
        /// </summary>
        /// <param name="PendingUploads">Tiles and their byte offsets within the staging buffer.</param>
        private void StagePendingTilePixels
            (
            IReadOnlyList<(GpuTextureMesh Mesh, ulong StagingOffset)> PendingUploads
            )
        {
            if (PendingUploads.Count == 0)
                return;

            void* Mapped = null;
            _Vk.MapMemory(_Device, _TileStagingMemory, 0, _TileStagingCapacity, 0, &Mapped);
            byte* Destination = (byte*)Mapped;
            foreach ((GpuTextureMesh Mesh, ulong StagingOffset) in PendingUploads)
            {
                fixed (byte* Source = Mesh.Pixels)
                {
                    System.Buffer.MemoryCopy(Source, Destination + StagingOffset, Mesh.Pixels.Length, Mesh.Pixels.Length);
                }
            }

            _Vk.UnmapMemory(_Device, _TileStagingMemory);
        }

        /// <summary>
        /// Returns a cached GPU texture for a tile, creating device-local image resources when missing.
        /// </summary>
        /// <param name="Mesh">Tile mesh describing dimensions and cache key.</param>
        /// <returns>Cached tile texture entry ready for upload or sampling.</returns>
        private GpuCachedTileTexture GetOrCreateGpuTileTexture
            (
            GpuTextureMesh Mesh
            )
        {
            (int Zoom, int X, int Y) Key = (Mesh.TileZoom, Mesh.TileX, Mesh.TileY);
            if (_GpuTileCache.TryGetValue(Key, out GpuCachedTileTexture? Cached))
                return Cached;

            GpuCachedTileTexture Entry = new GpuCachedTileTexture
            {
                Width = (uint)Mesh.Width,
                Height = (uint)Mesh.Height,
                Image = VulkanMemoryUtilities.CreateImage(
                    _Vk,
                    _Device,
                    _PhysicalDevice,
                    (uint)Mesh.Width,
                    (uint)Mesh.Height,
                    ColorFormat,
                    ImageTiling.Optimal,
                    ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
                    MemoryPropertyFlags.DeviceLocalBit,
                    out DeviceMemory Memory),
                Memory = Memory,
                Layout = ImageLayout.Undefined
            };
            Entry.View = VulkanMemoryUtilities.CreateImageView(_Vk, _Device, Entry.Image, ColorFormat, ImageAspectFlags.ColorBit);
            _GpuTileCache[Key] = Entry;
            return Entry;
        }

        /// <summary>
        /// Records buffer-to-image copy and layout transitions for a newly staged tile texture.
        /// </summary>
        /// <param name="CommandBuffer">Active command buffer for the render submission.</param>
        /// <param name="Tile">Destination GPU tile texture.</param>
        /// <param name="StagingOffset">Byte offset within the tile staging buffer.</param>
        private void RecordGpuTileTextureUpload
            (
            CommandBuffer CommandBuffer,
            GpuCachedTileTexture Tile,
            ulong StagingOffset
            )
        {
            ImageMemoryBarrier ToTransfer = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = Tile.Layout,
                NewLayout = ImageLayout.TransferDstOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = Tile.Image,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                },
                SrcAccessMask = Tile.Layout == ImageLayout.ShaderReadOnlyOptimal ? AccessFlags.ShaderReadBit : 0,
                DstAccessMask = AccessFlags.TransferWriteBit
            };

            PipelineStageFlags SourceStage = Tile.Layout == ImageLayout.ShaderReadOnlyOptimal
                ? PipelineStageFlags.FragmentShaderBit
                : PipelineStageFlags.TopOfPipeBit;
            _Vk.CmdPipelineBarrier(
                CommandBuffer,
                SourceStage,
                PipelineStageFlags.TransferBit,
                0,
                0,
                null,
                0,
                null,
                1,
                in ToTransfer);

            BufferImageCopy Region = new BufferImageCopy
            {
                BufferOffset = StagingOffset,
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                },
                ImageOffset = new Offset3D(0, 0, 0),
                ImageExtent = new Extent3D(Tile.Width, Tile.Height, 1)
            };
            _Vk.CmdCopyBufferToImage(CommandBuffer, _TileStagingBuffer, Tile.Image, ImageLayout.TransferDstOptimal, 1, in Region);

            ImageMemoryBarrier ToShaderRead = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.TransferDstOptimal,
                NewLayout = ImageLayout.ShaderReadOnlyOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = Tile.Image,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                },
                SrcAccessMask = AccessFlags.TransferWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit
            };
            _Vk.CmdPipelineBarrier(
                CommandBuffer,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.FragmentShaderBit,
                0,
                0,
                null,
                0,
                null,
                1,
                in ToShaderRead);

            Tile.Layout = ImageLayout.ShaderReadOnlyOptimal;
        }

        /// <summary>
        /// Transitions a cached tile image to shader-read layout when it was created but not yet uploaded.
        /// </summary>
        /// <param name="CommandBuffer">Active command buffer for the render submission.</param>
        /// <param name="Tile">GPU tile texture whose layout is updated.</param>
        private void EnsureGpuTileShaderRead
            (
            CommandBuffer CommandBuffer,
            GpuCachedTileTexture Tile
            )
        {
            if (Tile.Layout == ImageLayout.ShaderReadOnlyOptimal)
                return;

            ImageMemoryBarrier ToShaderRead = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = Tile.Layout,
                NewLayout = ImageLayout.ShaderReadOnlyOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = Tile.Image,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                },
                SrcAccessMask = AccessFlags.TransferWriteBit,
                DstAccessMask = AccessFlags.ShaderReadBit
            };
            _Vk.CmdPipelineBarrier(
                CommandBuffer,
                PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.FragmentShaderBit,
                0,
                0,
                null,
                0,
                null,
                1,
                in ToShaderRead);
            Tile.Layout = ImageLayout.ShaderReadOnlyOptimal;
        }

        /// <summary>
        /// Binds the combined image sampler descriptor to the current tile image view.
        /// </summary>
        /// <param name="TileView">Image view for the tile about to be drawn.</param>
        private void UpdateTileTextureDescriptor
            (
            ImageView TileView
            )
        {
            DescriptorImageInfo ImageInfo = new DescriptorImageInfo
            {
                Sampler = _Sampler,
                ImageView = TileView,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal
            };

            WriteDescriptorSet Write = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _DescriptorSet,
                DstBinding = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                PImageInfo = &ImageInfo
            };
            _Vk.UpdateDescriptorSets(_Device, 1, in Write, 0, null);
        }

        /// <summary>
        /// Destroys all cached GPU tile textures and clears the tile cache dictionary.
        /// </summary>
        private void ClearGpuTileCache
            (
            )
        {
            foreach (GpuCachedTileTexture Tile in _GpuTileCache.Values)
            {
                if (Tile.View.Handle != 0)
                    _Vk.DestroyImageView(_Device, Tile.View, null);
                if (Tile.Image.Handle != 0)
                {
                    _Vk.DestroyImage(_Device, Tile.Image, null);
                    _Vk.FreeMemory(_Device, Tile.Memory, null);
                }
            }

            _GpuTileCache.Clear();
        }

        /// <summary>
        /// Device-local image resources for one cached satellite tile texture.
        /// </summary>
        private sealed class GpuCachedTileTexture
        {
            /// <summary>Device-local tile image handle.</summary>
            public Image Image;

            /// <summary>Device memory backing <see cref="Image"/>.</summary>
            public DeviceMemory Memory;

            /// <summary>Shader sampling view for the tile image.</summary>
            public ImageView View;

            /// <summary>Current image layout tracked for barrier insertion.</summary>
            public ImageLayout Layout;

            /// <summary>Tile image width in pixels.</summary>
            public uint Width;

            /// <summary>Tile image height in pixels.</summary>
            public uint Height;
        }

        /// <summary>
        /// Destroys a buffer and frees its backing device memory when allocated.
        /// </summary>
        /// <param name="Buffer">Buffer handle cleared after destruction.</param>
        /// <param name="Memory">Memory handle cleared after release.</param>
        private void DestroyBuffer
            (
            ref VkBuffer Buffer,
            ref DeviceMemory Memory
            )
        {
            if (Buffer.Handle != 0)
            {
                _Vk.DestroyBuffer(_Device, Buffer, null);
                Buffer = default;
            }

            if (Memory.Handle != 0)
            {
                _Vk.FreeMemory(_Device, Memory, null);
                Memory = default;
            }
        }

        /// <summary>
        /// Creates the colour-plus-depth offscreen render pass used by all pipelines.
        /// </summary>
        /// <param name="VkApi">Vulkan API entry points.</param>
        /// <param name="Device">Logical device handle.</param>
        /// <param name="RenderPass">Output render pass handle.</param>
        /// <param name="FailureReason">Receives a human-readable failure message when creation fails.</param>
        /// <returns>True when the render pass is created successfully.</returns>
        static private bool TryCreateRenderPass
            (
            Vk VkApi,
            Device Device,
            out RenderPass RenderPass,
            out string? FailureReason
            )
        {
            FailureReason = null;
            AttachmentDescription ColorAttachment = new AttachmentDescription
            {
                Format = ColorFormat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.ColorAttachmentOptimal
            };
            AttachmentDescription DepthAttachment = new AttachmentDescription
            {
                Format = DepthFormat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
            };
            AttachmentDescription[] Attachments = { ColorAttachment, DepthAttachment };

            AttachmentReference ColorRef = new AttachmentReference
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal
            };
            AttachmentReference DepthRef = new AttachmentReference
            {
                Attachment = 1,
                Layout = ImageLayout.DepthStencilAttachmentOptimal
            };

            SubpassDescription Subpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &ColorRef,
                PDepthStencilAttachment = &DepthRef
            };

            SubpassDependency Dependency = new SubpassDependency
            {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
                SrcAccessMask = 0,
                DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
                DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
            };

            fixed (AttachmentDescription* AttachmentsPtr = Attachments)
            {
                RenderPassCreateInfo RenderPassInfo = new RenderPassCreateInfo
                {
                    SType = StructureType.RenderPassCreateInfo,
                    AttachmentCount = VulkanConstants.RENDER_PASS_ATTACHMENT_COUNT,
                    PAttachments = AttachmentsPtr,
                    SubpassCount = 1,
                    PSubpasses = &Subpass,
                    DependencyCount = 1,
                    PDependencies = &Dependency
                };

                if (VkApi.CreateRenderPass(Device, in RenderPassInfo, null, out RenderPass) != Result.Success)
                {
                    FailureReason = "Failed to create render pass.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Builds a graphics pipeline for coloured mesh or line draws using <see cref="GpuVertex"/> layout.
        /// </summary>
        /// <param name="VkApi">Vulkan API entry points.</param>
        /// <param name="Device">Logical device handle.</param>
        /// <param name="RenderPass">Compatible render pass handle.</param>
        /// <param name="PipelineLayout">Pipeline layout with camera UBO and push constants.</param>
        /// <param name="VertexShader">Compiled vertex shader module.</param>
        /// <param name="FragmentShader">Compiled fragment shader module.</param>
        /// <param name="Topology">Input assembly topology for the pipeline.</param>
        /// <param name="PolygonMode">Rasterizer polygon mode.</param>
        /// <param name="LineWidth">Rasterizer line width in pixels.</param>
        /// <param name="EnableAlphaBlend">When true, enables standard alpha blending on the colour attachment.</param>
        /// <returns>Created graphics pipeline handle.</returns>
        static private Pipeline CreateGraphicsPipeline
            (
            Vk VkApi,
            Device Device,
            RenderPass RenderPass,
            PipelineLayout PipelineLayout,
            ShaderModule VertexShader,
            ShaderModule FragmentShader,
            PrimitiveTopology Topology,
            PolygonMode PolygonMode,
            float LineWidth,
            bool EnableAlphaBlend = false
            )
        {
            byte* EntryName = (byte*)Silk.NET.Core.Native.SilkMarshal.StringToPtr("main");
            PipelineShaderStageCreateInfo[] Stages =
            {
                new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.VertexBit,
                    Module = VertexShader,
                    PName = EntryName
                },
                new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.FragmentBit,
                    Module = FragmentShader,
                    PName = EntryName
                }
            };

            try
            {
                VertexInputBindingDescription Binding = new VertexInputBindingDescription
                {
                    Binding = 0,
                    Stride = (uint)Marshal.SizeOf<GpuVertex>(),
                    InputRate = VertexInputRate.Vertex
                };
                VertexInputAttributeDescription[] Attributes =
                {
                    new VertexInputAttributeDescription
                    {
                        Location = 0,
                        Binding = 0,
                        Format = Format.R32G32B32Sfloat,
                        Offset = 0
                    },
                    new VertexInputAttributeDescription
                    {
                        Location = 1,
                        Binding = 0,
                        Format = Format.R32G32B32A32Sfloat,
                        Offset = (uint)Marshal.OffsetOf<GpuVertex>("R")
                    }
                };

                fixed (VertexInputAttributeDescription* AttributesPtr = Attributes)
                {
                    PipelineVertexInputStateCreateInfo VertexInput = new PipelineVertexInputStateCreateInfo
                    {
                        SType = StructureType.PipelineVertexInputStateCreateInfo,
                        VertexBindingDescriptionCount = 1,
                        PVertexBindingDescriptions = &Binding,
                        VertexAttributeDescriptionCount = (uint)Attributes.Length,
                        PVertexAttributeDescriptions = AttributesPtr
                    };

                    PipelineInputAssemblyStateCreateInfo InputAssembly = new PipelineInputAssemblyStateCreateInfo
                    {
                        SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                        Topology = Topology,
                        PrimitiveRestartEnable = false
                    };

                    PipelineViewportStateCreateInfo ViewportState = new PipelineViewportStateCreateInfo
                    {
                        SType = StructureType.PipelineViewportStateCreateInfo,
                        ViewportCount = 1,
                        ScissorCount = 1
                    };

                    PipelineRasterizationStateCreateInfo Rasterization = new PipelineRasterizationStateCreateInfo
                    {
                        SType = StructureType.PipelineRasterizationStateCreateInfo,
                        DepthClampEnable = false,
                        RasterizerDiscardEnable = false,
                        PolygonMode = PolygonMode,
                        LineWidth = LineWidth,
                        CullMode = CullModeFlags.None,
                        FrontFace = FrontFace.CounterClockwise,
                        DepthBiasEnable = false
                    };

                    PipelineMultisampleStateCreateInfo Multisample = new PipelineMultisampleStateCreateInfo
                    {
                        SType = StructureType.PipelineMultisampleStateCreateInfo,
                        RasterizationSamples = SampleCountFlags.Count1Bit
                    };

                    PipelineDepthStencilStateCreateInfo DepthStencil = new PipelineDepthStencilStateCreateInfo
                    {
                        SType = StructureType.PipelineDepthStencilStateCreateInfo,
                        DepthTestEnable = true,
                        DepthWriteEnable = true,
                        DepthCompareOp = CompareOp.LessOrEqual
                    };

                    PipelineColorBlendAttachmentState BlendAttachment = new PipelineColorBlendAttachmentState
                    {
                        ColorWriteMask = ColorComponentFlags.RBit
                            | ColorComponentFlags.GBit
                            | ColorComponentFlags.BBit
                            | ColorComponentFlags.ABit,
                        BlendEnable = EnableAlphaBlend,
                        SrcColorBlendFactor = BlendFactor.SrcAlpha,
                        DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                        ColorBlendOp = BlendOp.Add,
                        SrcAlphaBlendFactor = BlendFactor.One,
                        DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                        AlphaBlendOp = BlendOp.Add
                    };
                    PipelineColorBlendStateCreateInfo ColorBlend = new PipelineColorBlendStateCreateInfo
                    {
                        SType = StructureType.PipelineColorBlendStateCreateInfo,
                        AttachmentCount = VulkanConstants.COLOR_BLEND_ATTACHMENT_COUNT,
                        PAttachments = &BlendAttachment
                    };

                    DynamicState[] DynamicStates = { DynamicState.Viewport, DynamicState.Scissor };
                    fixed (DynamicState* DynamicStatesPtr = DynamicStates)
                    {
                        PipelineDynamicStateCreateInfo DynamicStateInfo = new PipelineDynamicStateCreateInfo
                        {
                            SType = StructureType.PipelineDynamicStateCreateInfo,
                            DynamicStateCount = VulkanConstants.DYNAMIC_STATE_COUNT,
                            PDynamicStates = DynamicStatesPtr
                        };

                        fixed (PipelineShaderStageCreateInfo* StagesPtr = Stages)
                        {
                            GraphicsPipelineCreateInfo PipelineInfo = new GraphicsPipelineCreateInfo
                            {
                                SType = StructureType.GraphicsPipelineCreateInfo,
                                StageCount = VulkanConstants.GRAPHICS_SHADER_STAGE_COUNT,
                                PStages = StagesPtr,
                                PVertexInputState = &VertexInput,
                                PInputAssemblyState = &InputAssembly,
                                PViewportState = &ViewportState,
                                PRasterizationState = &Rasterization,
                                PMultisampleState = &Multisample,
                                PDepthStencilState = &DepthStencil,
                                PColorBlendState = &ColorBlend,
                                PDynamicState = &DynamicStateInfo,
                                Layout = PipelineLayout,
                                RenderPass = RenderPass,
                                Subpass = 0
                            };

                            if (VkApi.CreateGraphicsPipelines(Device, default, 1, in PipelineInfo, null, out Pipeline Pipeline) != Result.Success)
                                throw new InvalidOperationException("Failed to create graphics pipeline.");

                            return Pipeline;
                        }
                    }
                }
            }
            finally
            {
                Silk.NET.Core.Native.SilkMarshal.Free((nint)EntryName);
            }
        }

        /// <summary>
        /// Builds the textured satellite tile graphics pipeline using <see cref="GpuTexturedVertex"/> layout.
        /// </summary>
        /// <param name="VkApi">Vulkan API entry points.</param>
        /// <param name="Device">Logical device handle.</param>
        /// <param name="RenderPass">Compatible render pass handle.</param>
        /// <param name="PipelineLayout">Pipeline layout with camera UBO and tile sampler binding.</param>
        /// <param name="VertexShader">Compiled textured vertex shader module.</param>
        /// <param name="FragmentShader">Compiled textured fragment shader module.</param>
        /// <returns>Created textured graphics pipeline handle.</returns>
        static private Pipeline CreateTexturedGraphicsPipeline
            (
            Vk VkApi,
            Device Device,
            RenderPass RenderPass,
            PipelineLayout PipelineLayout,
            ShaderModule VertexShader,
            ShaderModule FragmentShader
            )
        {
            byte* EntryName = (byte*)Silk.NET.Core.Native.SilkMarshal.StringToPtr("main");
            PipelineShaderStageCreateInfo[] Stages =
            {
                new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.VertexBit,
                    Module = VertexShader,
                    PName = EntryName
                },
                new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.FragmentBit,
                    Module = FragmentShader,
                    PName = EntryName
                }
            };

            try
            {
                VertexInputBindingDescription Binding = new VertexInputBindingDescription
                {
                    Binding = 0,
                    Stride = (uint)Marshal.SizeOf<GpuTexturedVertex>(),
                    InputRate = VertexInputRate.Vertex
                };
                VertexInputAttributeDescription[] Attributes =
                {
                    new VertexInputAttributeDescription
                    {
                        Location = 0,
                        Binding = 0,
                        Format = Format.R32G32B32Sfloat,
                        Offset = 0
                    },
                    new VertexInputAttributeDescription
                    {
                        Location = 1,
                        Binding = 0,
                        Format = Format.R32G32Sfloat,
                        Offset = (uint)Marshal.OffsetOf<GpuTexturedVertex>("U")
                    }
                };

                fixed (VertexInputAttributeDescription* AttributesPtr = Attributes)
                {
                    PipelineVertexInputStateCreateInfo VertexInput = new PipelineVertexInputStateCreateInfo
                    {
                        SType = StructureType.PipelineVertexInputStateCreateInfo,
                        VertexBindingDescriptionCount = 1,
                        PVertexBindingDescriptions = &Binding,
                        VertexAttributeDescriptionCount = (uint)Attributes.Length,
                        PVertexAttributeDescriptions = AttributesPtr
                    };

                    PipelineInputAssemblyStateCreateInfo InputAssembly = new PipelineInputAssemblyStateCreateInfo
                    {
                        SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                        Topology = PrimitiveTopology.TriangleList,
                        PrimitiveRestartEnable = false
                    };

                    PipelineViewportStateCreateInfo ViewportState = new PipelineViewportStateCreateInfo
                    {
                        SType = StructureType.PipelineViewportStateCreateInfo,
                        ViewportCount = 1,
                        ScissorCount = 1
                    };

                    PipelineRasterizationStateCreateInfo Rasterization = new PipelineRasterizationStateCreateInfo
                    {
                        SType = StructureType.PipelineRasterizationStateCreateInfo,
                        DepthClampEnable = false,
                        RasterizerDiscardEnable = false,
                        PolygonMode = PolygonMode.Fill,
                        LineWidth = VulkanConstants.DEFAULT_LINE_WIDTH,
                        CullMode = CullModeFlags.None,
                        FrontFace = FrontFace.CounterClockwise,
                        DepthBiasEnable = false
                    };

                    PipelineMultisampleStateCreateInfo Multisample = new PipelineMultisampleStateCreateInfo
                    {
                        SType = StructureType.PipelineMultisampleStateCreateInfo,
                        RasterizationSamples = SampleCountFlags.Count1Bit
                    };

                    PipelineDepthStencilStateCreateInfo DepthStencil = new PipelineDepthStencilStateCreateInfo
                    {
                        SType = StructureType.PipelineDepthStencilStateCreateInfo,
                        DepthTestEnable = true,
                        DepthWriteEnable = true,
                        DepthCompareOp = CompareOp.LessOrEqual
                    };

                    PipelineColorBlendAttachmentState BlendAttachment = new PipelineColorBlendAttachmentState
                    {
                        ColorWriteMask = ColorComponentFlags.RBit
                            | ColorComponentFlags.GBit
                            | ColorComponentFlags.BBit
                            | ColorComponentFlags.ABit
                    };
                    PipelineColorBlendStateCreateInfo ColorBlend = new PipelineColorBlendStateCreateInfo
                    {
                        SType = StructureType.PipelineColorBlendStateCreateInfo,
                        AttachmentCount = VulkanConstants.COLOR_BLEND_ATTACHMENT_COUNT,
                        PAttachments = &BlendAttachment
                    };

                    DynamicState[] DynamicStates = { DynamicState.Viewport, DynamicState.Scissor };
                    fixed (DynamicState* DynamicStatesPtr = DynamicStates)
                    {
                        PipelineDynamicStateCreateInfo DynamicStateInfo = new PipelineDynamicStateCreateInfo
                        {
                            SType = StructureType.PipelineDynamicStateCreateInfo,
                            DynamicStateCount = VulkanConstants.DYNAMIC_STATE_COUNT,
                            PDynamicStates = DynamicStatesPtr
                        };

                        fixed (PipelineShaderStageCreateInfo* StagesPtr = Stages)
                        {
                            GraphicsPipelineCreateInfo PipelineInfo = new GraphicsPipelineCreateInfo
                            {
                                SType = StructureType.GraphicsPipelineCreateInfo,
                                StageCount = VulkanConstants.GRAPHICS_SHADER_STAGE_COUNT,
                                PStages = StagesPtr,
                                PVertexInputState = &VertexInput,
                                PInputAssemblyState = &InputAssembly,
                                PViewportState = &ViewportState,
                                PRasterizationState = &Rasterization,
                                PMultisampleState = &Multisample,
                                PDepthStencilState = &DepthStencil,
                                PColorBlendState = &ColorBlend,
                                PDynamicState = &DynamicStateInfo,
                                Layout = PipelineLayout,
                                RenderPass = RenderPass,
                                Subpass = 0
                            };

                            if (VkApi.CreateGraphicsPipelines(Device, default, 1, in PipelineInfo, null, out Pipeline Pipeline) != Result.Success)
                                throw new InvalidOperationException("Failed to create textured graphics pipeline.");

                            return Pipeline;
                        }
                    }
                }
            }
            finally
            {
                Silk.NET.Core.Native.SilkMarshal.Free((nint)EntryName);
            }
        }
    }
}

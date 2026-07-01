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
        private const Format ColorFormat = Format.B8G8R8A8Unorm;
        private const Format DepthFormat = Format.D32Sfloat;

        private readonly VulkanGraphicsSession _Session;
        private readonly Vk _Vk;
        private readonly Device _Device;
        private readonly PhysicalDevice _PhysicalDevice;
        private readonly Queue _Queue;
        private readonly CommandPool _CommandPool;
        private readonly Fence _Fence;

        private readonly ShaderModule _VertexShader;
        private readonly ShaderModule _FragmentShader;
        private readonly ShaderModule _TexturedVertexShader;
        private readonly ShaderModule _TexturedFragmentShader;
        private readonly PipelineLayout _PipelineLayout;
        private readonly DescriptorSetLayout _DescriptorSetLayout;
        private readonly DescriptorPool _DescriptorPool;
        private readonly DescriptorSet _DescriptorSet;
        private readonly Pipeline _TrianglePipeline;
        private readonly Pipeline _TexturedPipeline;
        private readonly Pipeline _LinePipeline;
        private readonly RenderPass _RenderPass;
        private readonly Sampler _Sampler;

        private VkBuffer _TexturedVertexBuffer;
        private DeviceMemory _TexturedVertexMemory;
        private ulong _TexturedVertexCapacity;
        private VkBuffer _TileStagingBuffer;
        private DeviceMemory _TileStagingMemory;
        private ulong _TileStagingCapacity;
        private readonly Dictionary<(int Zoom, int X, int Y), GpuCachedTileTexture> _GpuTileCache = new();
        private int _GpuTileCacheGeneration = -1;

        private GpuVertex[]? _UploadedBackgroundSource;
        private GpuVertex[]? _UploadedMeshSource;
        private GpuVertex[]? _UploadedLineSource;
        private GpuTexturedVertex[]? _UploadedTexturedSource;
        private byte[]? _ReadbackPixels;

        private uint _Width;
        private uint _Height;
        private Image _ColorImage;
        private DeviceMemory _ColorMemory;
        private ImageView _ColorView;
        private Image _DepthImage;
        private DeviceMemory _DepthMemory;
        private ImageView _DepthView;
        private Framebuffer _Framebuffer;
        private VkBuffer _UniformBuffer;
        private DeviceMemory _UniformMemory;
        private VkBuffer _MeshVertexBuffer;
        private DeviceMemory _MeshVertexMemory;
        private ulong _MeshVertexCapacity;
        private VkBuffer _BackgroundVertexBuffer;
        private DeviceMemory _BackgroundVertexMemory;
        private ulong _BackgroundVertexCapacity;
        private VkBuffer _LineVertexBuffer;
        private DeviceMemory _LineVertexMemory;
        private ulong _LineVertexCapacity;
        private VkBuffer _ReadbackBuffer;
        private DeviceMemory _ReadbackMemory;
        private ulong _ReadbackSize;
        private bool _Disposed;

        private VulkanOffscreenRenderer
            (
            VulkanGraphicsSession Session,
            ShaderModule VertexShader,
            ShaderModule FragmentShader,
            ShaderModule TexturedVertexShader,
            ShaderModule TexturedFragmentShader,
            PipelineLayout PipelineLayout,
            DescriptorSetLayout DescriptorSetLayout,
            DescriptorPool DescriptorPool,
            DescriptorSet DescriptorSet,
            Pipeline TrianglePipeline,
            Pipeline TexturedPipeline,
            Pipeline LinePipeline,
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
            _TexturedVertexShader = TexturedVertexShader;
            _TexturedFragmentShader = TexturedFragmentShader;
            _PipelineLayout = PipelineLayout;
            _DescriptorSetLayout = DescriptorSetLayout;
            _DescriptorPool = DescriptorPool;
            _DescriptorSet = DescriptorSet;
            _TrianglePipeline = TrianglePipeline;
            _TexturedPipeline = TexturedPipeline;
            _LinePipeline = LinePipeline;
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

            ShaderModule VertexShader = default;
            ShaderModule FragmentShader = default;
            ShaderModule TexturedVertexShader = default;
            ShaderModule TexturedFragmentShader = default;
            DescriptorSetLayout DescriptorSetLayout = default;
            DescriptorPool DescriptorPool = default;
            PipelineLayout PipelineLayout = default;
            Pipeline TrianglePipeline = default;
            Pipeline TexturedPipeline = default;
            Pipeline LinePipeline = default;
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

                PipelineLayoutCreateInfo PipelineLayoutInfo = new PipelineLayoutCreateInfo
                {
                    SType = StructureType.PipelineLayoutCreateInfo,
                    SetLayoutCount = 1,
                    PSetLayouts = &DescriptorSetLayout
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
                    1.0f);
                LinePipeline = CreateGraphicsPipeline(
                    VkApi,
                    Device,
                    RenderPass,
                    PipelineLayout,
                    VertexShader,
                    FragmentShader,
                    PrimitiveTopology.LineList,
                    PolygonMode.Fill,
                    1.75f);
                TexturedPipeline = CreateTexturedGraphicsPipeline(
                    VkApi,
                    Device,
                    RenderPass,
                    PipelineLayout,
                    TexturedVertexShader,
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
                    TexturedVertexShader,
                    TexturedFragmentShader,
                    PipelineLayout,
                    DescriptorSetLayout,
                    DescriptorPool,
                    DescriptorSet,
                    TrianglePipeline,
                    TexturedPipeline,
                    LinePipeline,
                    RenderPass,
                    Sampler);
            }
            catch (Exception Ex)
            {
                FailureReason = Ex.Message;
                if (Sampler.Handle != 0)
                    VkApi.DestroySampler(Device, Sampler, null);
                if (LinePipeline.Handle != 0)
                    VkApi.DestroyPipeline(Device, LinePipeline, null);
                if (TexturedPipeline.Handle != 0)
                    VkApi.DestroyPipeline(Device, TexturedPipeline, null);
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
                if (TexturedFragmentShader.Handle != 0)
                    VkApi.DestroyShaderModule(Device, TexturedFragmentShader, null);
                if (TexturedVertexShader.Handle != 0)
                    VkApi.DestroyShaderModule(Device, TexturedVertexShader, null);
                if (VertexShader.Handle != 0)
                    VkApi.DestroyShaderModule(Device, VertexShader, null);
                return null;
            }
        }

        /// <summary>
        /// Renders one field frame and returns BGRA8888 premultiplied pixels (row-major).
        /// </summary>
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
                ClearValue ClearDepth = new ClearValue { DepthStencil = new ClearDepthStencilValue(1.0f, 0) };
                ClearValue* ClearsPtr = stackalloc ClearValue[2];
                ClearsPtr[0] = ClearColor;
                ClearsPtr[1] = ClearDepth;

                RenderPassBeginInfo RenderPassInfo = new RenderPassBeginInfo
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _RenderPass,
                    Framebuffer = _Framebuffer,
                    RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D(_Width, _Height)),
                    ClearValueCount = 2,
                    PClearValues = ClearsPtr
                };
                _Vk.CmdBeginRenderPass(CommandBuffer, in RenderPassInfo, SubpassContents.Inline);

                Viewport Viewport = new Viewport(0, 0, _Width, _Height, 0, 1);
                Rect2D Scissor = new Rect2D(new Offset2D(0, 0), new Extent2D(_Width, _Height));
                _Vk.CmdSetViewport(CommandBuffer, 0, 1, in Viewport);
                _Vk.CmdSetScissor(CommandBuffer, 0, 1, in Scissor);

                _Vk.CmdBindDescriptorSets(CommandBuffer, PipelineBindPoint.Graphics, _PipelineLayout, 0, 1, in _DescriptorSet, 0, null);

                if (Frame.BackgroundMeshVertices.Length > 0)
                {
                    _Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, _TrianglePipeline);
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

                    _Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, _TexturedPipeline);
                    _Vk.CmdBindDescriptorSets(CommandBuffer, PipelineBindPoint.Graphics, _PipelineLayout, 0, 1, in _DescriptorSet, 0, null);
                    ulong TexturedOffset = 0;
                    _Vk.CmdBindVertexBuffers(CommandBuffer, 0, 1, in _TexturedVertexBuffer, in TexturedOffset);
                    _Vk.CmdDraw(CommandBuffer, DrawSpec.VertexCount, 1, DrawSpec.FirstVertex, 0);
                }

                if (Frame.MeshVertices.Length > 0)
                {
                    _Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, _TrianglePipeline);
                    ulong MeshOffset = 0;
                    _Vk.CmdBindVertexBuffers(CommandBuffer, 0, 1, in _MeshVertexBuffer, in MeshOffset);
                    _Vk.CmdDraw(CommandBuffer, (uint)Frame.MeshVertices.Length, 1, 0, 0);
                }

                if (Frame.LineVertices.Length > 0)
                {
                    _Vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, _LinePipeline);
                    ulong LineOffset = 0;
                    _Vk.CmdBindVertexBuffers(CommandBuffer, 0, 1, in _LineVertexBuffer, in LineOffset);
                    _Vk.CmdDraw(CommandBuffer, (uint)Frame.LineVertices.Length, 1, 0, 0);
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

                if (_Vk.WaitForFences(_Device, 1, in _Fence, true, 5_000_000_000) != Result.Success)
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

        /// <inheritdoc />
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
            DestroyBuffer(ref _TexturedVertexBuffer, ref _TexturedVertexMemory);
            DestroyBuffer(ref _TileStagingBuffer, ref _TileStagingMemory);
            DestroyBuffer(ref _UniformBuffer, ref _UniformMemory);
            _Vk.DestroySampler(_Device, _Sampler, null);
            _Vk.DestroyPipeline(_Device, _LinePipeline, null);
            _Vk.DestroyPipeline(_Device, _TexturedPipeline, null);
            _Vk.DestroyPipeline(_Device, _TrianglePipeline, null);
            _Vk.DestroyRenderPass(_Device, _RenderPass, null);
            _Vk.DestroyPipelineLayout(_Device, _PipelineLayout, null);
            _Vk.DestroyDescriptorPool(_Device, _DescriptorPool, null);
            _Vk.DestroyDescriptorSetLayout(_Device, _DescriptorSetLayout, null);
            _Vk.DestroyShaderModule(_Device, _TexturedFragmentShader, null);
            _Vk.DestroyShaderModule(_Device, _TexturedVertexShader, null);
            _Vk.DestroyShaderModule(_Device, _FragmentShader, null);
            _Vk.DestroyShaderModule(_Device, _VertexShader, null);
        }

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
                    AttachmentCount = 2,
                    PAttachments = AttachmentsPtr,
                    Width = Width,
                    Height = Height,
                    Layers = 1
                };
                if (_Vk.CreateFramebuffer(_Device, in FramebufferInfo, null, out _Framebuffer) != Result.Success)
                    throw new InvalidOperationException("Failed to create framebuffer.");
            }

            ulong RowPitch = Width * 4;
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

        private void UploadUniforms
            (
            CameraGpuUniforms Uniforms
            )
        {
            void* Mapped = null;
            _Vk.MapMemory(_Device, _UniformMemory, 0, (ulong)Marshal.SizeOf<CameraGpuUniforms>(), 0, &Mapped);
            float* Floats = (float*)Mapped;
            Floats[0] = Uniforms.ViewportWidth;
            Floats[1] = Uniforms.ViewportHeight;
            Floats[2] = Uniforms.ViewportMidX;
            Floats[3] = Uniforms.ViewportMidY;
            Floats[4] = Uniforms.Scale;
            Floats[5] = Uniforms.CosH;
            Floats[6] = Uniforms.SinH;
            Floats[7] = Uniforms.CosP;
            Floats[8] = Uniforms.SinP;
            Floats[9] = Uniforms.DepthMin;
            Floats[10] = Uniforms.DepthMax;
            _Vk.UnmapMemory(_Device, _UniformMemory);
        }

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

        private sealed class GpuCachedTileTexture
        {
            public Image Image;
            public DeviceMemory Memory;
            public ImageView View;
            public ImageLayout Layout;
            public uint Width;
            public uint Height;
        }

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
                    AttachmentCount = 2,
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
            float LineWidth
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
                            | ColorComponentFlags.ABit
                    };
                    PipelineColorBlendStateCreateInfo ColorBlend = new PipelineColorBlendStateCreateInfo
                    {
                        SType = StructureType.PipelineColorBlendStateCreateInfo,
                        AttachmentCount = 1,
                        PAttachments = &BlendAttachment
                    };

                    DynamicState[] DynamicStates = { DynamicState.Viewport, DynamicState.Scissor };
                    fixed (DynamicState* DynamicStatesPtr = DynamicStates)
                    {
                        PipelineDynamicStateCreateInfo DynamicStateInfo = new PipelineDynamicStateCreateInfo
                        {
                            SType = StructureType.PipelineDynamicStateCreateInfo,
                            DynamicStateCount = (uint)DynamicStates.Length,
                            PDynamicStates = DynamicStatesPtr
                        };

                        fixed (PipelineShaderStageCreateInfo* StagesPtr = Stages)
                        {
                            GraphicsPipelineCreateInfo PipelineInfo = new GraphicsPipelineCreateInfo
                            {
                                SType = StructureType.GraphicsPipelineCreateInfo,
                                StageCount = 2,
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
                        LineWidth = 1.0f,
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
                        AttachmentCount = 1,
                        PAttachments = &BlendAttachment
                    };

                    DynamicState[] DynamicStates = { DynamicState.Viewport, DynamicState.Scissor };
                    fixed (DynamicState* DynamicStatesPtr = DynamicStates)
                    {
                        PipelineDynamicStateCreateInfo DynamicStateInfo = new PipelineDynamicStateCreateInfo
                        {
                            SType = StructureType.PipelineDynamicStateCreateInfo,
                            DynamicStateCount = (uint)DynamicStates.Length,
                            PDynamicStates = DynamicStatesPtr
                        };

                        fixed (PipelineShaderStageCreateInfo* StagesPtr = Stages)
                        {
                            GraphicsPipelineCreateInfo PipelineInfo = new GraphicsPipelineCreateInfo
                            {
                                SType = StructureType.GraphicsPipelineCreateInfo,
                                StageCount = 2,
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

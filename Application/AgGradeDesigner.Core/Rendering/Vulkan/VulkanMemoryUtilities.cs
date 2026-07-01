using Silk.NET.Vulkan;

using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace AgGradeDesigner.Core.Rendering.Vulkan
{
    /// <summary>
    /// Buffer allocation helpers for Vulkan render resources.
    /// </summary>
    static internal unsafe class VulkanMemoryUtilities
    {
        static public uint FindMemoryType
            (
            Vk VkApi,
            PhysicalDevice PhysicalDevice,
            uint TypeFilter,
            MemoryPropertyFlags RequiredProperties
            )
        {
            VkApi.GetPhysicalDeviceMemoryProperties(PhysicalDevice, out PhysicalDeviceMemoryProperties MemoryProperties);
            for (uint Index = 0; Index < MemoryProperties.MemoryTypeCount; Index++)
            {
                if (((TypeFilter & (1u << (int)Index)) != 0)
                    && (MemoryProperties.MemoryTypes[(int)Index].PropertyFlags & RequiredProperties) == RequiredProperties)
                {
                    return Index;
                }
            }

            throw new InvalidOperationException("No suitable Vulkan memory type was found.");
        }

        static public VkBuffer CreateBuffer
            (
            Vk VkApi,
            Device Device,
            PhysicalDevice PhysicalDevice,
            ulong Size,
            BufferUsageFlags Usage,
            MemoryPropertyFlags Properties,
            out DeviceMemory Memory
            )
        {
            BufferCreateInfo BufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = Size,
                Usage = Usage,
                SharingMode = SharingMode.Exclusive
            };

            Result CreateBufferResult = VkApi.CreateBuffer(Device, in BufferInfo, null, out VkBuffer Buffer);
            if (CreateBufferResult != Result.Success)
                throw new InvalidOperationException($"vkCreateBuffer failed: {CreateBufferResult}.");

            VkApi.GetBufferMemoryRequirements(Device, Buffer, out MemoryRequirements Requirements);
            MemoryAllocateInfo AllocateInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = Requirements.Size,
                MemoryTypeIndex = FindMemoryType(VkApi, PhysicalDevice, Requirements.MemoryTypeBits, Properties)
            };

            Result AllocateResult = VkApi.AllocateMemory(Device, in AllocateInfo, null, out Memory);
            if (AllocateResult != Result.Success)
            {
                VkApi.DestroyBuffer(Device, Buffer, null);
                throw new InvalidOperationException($"vkAllocateMemory failed: {AllocateResult}.");
            }

            VkApi.BindBufferMemory(Device, Buffer, Memory, 0);
            return Buffer;
        }

        static public Image CreateImage
            (
            Vk VkApi,
            Device Device,
            PhysicalDevice PhysicalDevice,
            uint Width,
            uint Height,
            Format Format,
            ImageTiling Tiling,
            ImageUsageFlags Usage,
            MemoryPropertyFlags Properties,
            out DeviceMemory Memory
            )
        {
            ImageCreateInfo ImageInfo = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Format = Format,
                Extent = new Extent3D(Width, Height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Samples = SampleCountFlags.Count1Bit,
                Tiling = Tiling,
                Usage = Usage,
                SharingMode = SharingMode.Exclusive,
                InitialLayout = ImageLayout.Undefined
            };

            Result CreateImageResult = VkApi.CreateImage(Device, in ImageInfo, null, out Image Image);
            if (CreateImageResult != Result.Success)
                throw new InvalidOperationException($"vkCreateImage failed: {CreateImageResult}.");

            VkApi.GetImageMemoryRequirements(Device, Image, out MemoryRequirements Requirements);
            MemoryAllocateInfo AllocateInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = Requirements.Size,
                MemoryTypeIndex = FindMemoryType(VkApi, PhysicalDevice, Requirements.MemoryTypeBits, Properties)
            };

            Result AllocateResult = VkApi.AllocateMemory(Device, in AllocateInfo, null, out Memory);
            if (AllocateResult != Result.Success)
            {
                VkApi.DestroyImage(Device, Image, null);
                throw new InvalidOperationException($"vkAllocateMemory failed: {AllocateResult}.");
            }

            VkApi.BindImageMemory(Device, Image, Memory, 0);
            return Image;
        }

        static public ImageView CreateImageView
            (
            Vk VkApi,
            Device Device,
            Image Image,
            Format Format,
            ImageAspectFlags AspectMask
            )
        {
            ImageViewCreateInfo ViewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = Image,
                ViewType = ImageViewType.Type2D,
                Format = Format,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = AspectMask,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            Result CreateViewResult = VkApi.CreateImageView(Device, in ViewInfo, null, out ImageView View);
            if (CreateViewResult != Result.Success)
                throw new InvalidOperationException($"vkCreateImageView failed: {CreateViewResult}.");

            return View;
        }
    }
}

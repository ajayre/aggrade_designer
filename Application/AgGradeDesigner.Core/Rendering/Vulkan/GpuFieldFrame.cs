using System.Runtime.InteropServices;

namespace AgGradeDesigner.Core.Rendering.Vulkan
{
    /// <summary>
    /// Interleaved mesh/line vertex uploaded to the GPU (model east/north/Z + RGBA colour).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuVertex
    {
        /// <summary>East offset from the field origin in metres.</summary>
        public float EastM;

        /// <summary>North offset from the field origin in metres.</summary>
        public float NorthM;

        /// <summary>Model-space elevation in metres.</summary>
        public float ModelZ;

        /// <summary>Red colour component in [0, 1].</summary>
        public float R;

        /// <summary>Green colour component in [0, 1].</summary>
        public float G;

        /// <summary>Blue colour component in [0, 1].</summary>
        public float B;

        /// <summary>Alpha colour component in [0, 1].</summary>
        public float A;
    }

    /// <summary>
    /// Camera parameters shared with the field 3D Vulkan shaders (std140 layout).
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = VulkanConstants.CAMERA_UNIFORM_SIZE_BYTES)]
    public struct CameraGpuUniforms
    {
        /// <summary>Render target width in pixels.</summary>
        [FieldOffset(VulkanConstants.CAMERA_UNIFORM_VIEWPORT_WIDTH_OFFSET)]
        public float ViewportWidth;

        /// <summary>Render target height in pixels.</summary>
        [FieldOffset(VulkanConstants.CAMERA_UNIFORM_VIEWPORT_HEIGHT_OFFSET)]
        public float ViewportHeight;

        /// <summary>Horizontal screen centre in pixels.</summary>
        [FieldOffset(VulkanConstants.CAMERA_UNIFORM_VIEWPORT_MID_X_OFFSET)]
        public float ViewportMidX;

        /// <summary>Vertical screen centre in pixels.</summary>
        [FieldOffset(VulkanConstants.CAMERA_UNIFORM_VIEWPORT_MID_Y_OFFSET)]
        public float ViewportMidY;

        /// <summary>Metres per pixel scale factor for orthographic projection.</summary>
        [FieldOffset(VulkanConstants.CAMERA_UNIFORM_SCALE_OFFSET)]
        public float Scale;

        /// <summary>Cosine of the map heading rotation in radians.</summary>
        [FieldOffset(VulkanConstants.CAMERA_UNIFORM_COS_H_OFFSET)]
        public float CosH;

        /// <summary>Sine of the map heading rotation in radians.</summary>
        [FieldOffset(VulkanConstants.CAMERA_UNIFORM_SIN_H_OFFSET)]
        public float SinH;

        /// <summary>Cosine of the camera pitch angle in radians.</summary>
        [FieldOffset(VulkanConstants.CAMERA_UNIFORM_COS_P_OFFSET)]
        public float CosP;

        /// <summary>Sine of the camera pitch angle in radians.</summary>
        [FieldOffset(VulkanConstants.CAMERA_UNIFORM_SIN_P_OFFSET)]
        public float SinP;

        /// <summary>Nearest camera depth in model metres.</summary>
        [FieldOffset(VulkanConstants.CAMERA_UNIFORM_DEPTH_MIN_OFFSET)]
        public float DepthMin;

        /// <summary>Farthest camera depth in model metres.</summary>
        [FieldOffset(VulkanConstants.CAMERA_UNIFORM_DEPTH_MAX_OFFSET)]
        public float DepthMax;
    }

    /// <summary>
    /// CPU-side geometry for one 3D field frame rendered on the GPU.
    /// </summary>
    public sealed class GpuFieldFrame
    {
        /// <summary>Sky, plinth sides, and satellite top — drawn before terrain.</summary>
        public GpuVertex[] BackgroundMeshVertices { get; init; } = Array.Empty<GpuVertex>();

        /// <summary>Satellite imagery tiles sampled on the GPU.</summary>
        public GpuTextureMesh[] TextureMeshes { get; init; } = Array.Empty<GpuTextureMesh>();

        /// <summary>All textured vertices concatenated for a single GPU vertex buffer.</summary>
        public GpuTexturedVertex[] CombinedTextureVertices { get; init; } = Array.Empty<GpuTexturedVertex>();

        /// <summary>Draw ranges referencing <see cref="CombinedTextureVertices"/> and <see cref="TextureMeshes"/>.</summary>
        public GpuTextureDrawSpec[] TextureDraws { get; init; } = Array.Empty<GpuTextureDrawSpec>();

        /// <summary>Coloured terrain mesh triangles drawn with overlay opacity.</summary>
        public GpuVertex[] MeshVertices { get; init; } = Array.Empty<GpuVertex>();

        /// <summary>Contour and overlay line segments.</summary>
        public GpuVertex[] LineVertices { get; init; } = Array.Empty<GpuVertex>();

        /// <summary>Field boundary outline drawn at full opacity.</summary>
        public GpuVertex[] BorderLineVertices { get; init; } = Array.Empty<GpuVertex>();

        /// <summary>Camera uniform values uploaded before draw.</summary>
        public CameraGpuUniforms Camera { get; init; }

        /// <summary>Generation token used to invalidate cached GPU satellite tiles.</summary>
        public int SatelliteCacheGeneration { get; init; }

        /// <summary>Red component of the render-pass clear colour.</summary>
        public float ClearR { get; init; } = VulkanConstants.DEFAULT_CLEAR_COLOR_COMPONENT;

        /// <summary>Green component of the render-pass clear colour.</summary>
        public float ClearG { get; init; } = VulkanConstants.DEFAULT_CLEAR_COLOR_COMPONENT;

        /// <summary>Blue component of the render-pass clear colour.</summary>
        public float ClearB { get; init; } = VulkanConstants.DEFAULT_CLEAR_COLOR_COMPONENT;

        /// <summary>Alpha component of the render-pass clear colour.</summary>
        public float ClearA { get; init; } = VulkanConstants.DEFAULT_CLEAR_COLOR_COMPONENT;

        /// <summary>Opacity applied to coloured mesh and contour lines (0–1).</summary>
        public float SurfaceOverlayOpacity { get; init; } = VulkanConstants.DEFAULT_SURFACE_OVERLAY_OPACITY;

        /// <summary>
        /// When true, vertices store screen pixel coordinates and plan-view shaders are used.
        /// </summary>
        public bool UsePlanViewProjection { get; init; }
    }
}


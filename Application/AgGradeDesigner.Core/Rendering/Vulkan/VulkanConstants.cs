namespace AgGradeDesigner.Core.Rendering.Vulkan
{
    /// <summary>
    /// Shared numeric constants for Vulkan bootstrap, offscreen rendering, and camera UBO layout.
    /// </summary>
    internal static class VulkanConstants
    {
        /// <summary>Application version major component passed to <c>vkCreateInstance</c>.</summary>
        public const uint APPLICATION_VERSION_MAJOR = 1;

        /// <summary>Application version minor component passed to <c>vkCreateInstance</c>.</summary>
        public const uint APPLICATION_VERSION_MINOR = 0;

        /// <summary>Application version patch component passed to <c>vkCreateInstance</c>.</summary>
        public const uint APPLICATION_VERSION_PATCH = 0;

        /// <summary>Priority assigned to the primary graphics queue at device creation.</summary>
        public const float GRAPHICS_QUEUE_PRIORITY = 1.0f;

        /// <summary>Maximum time to wait on a fence during render submission, in nanoseconds.</summary>
        public const ulong FENCE_WAIT_TIMEOUT_NS = 5_000_000_000;

        /// <summary>Bytes per BGRA8888 pixel used for readback buffer sizing.</summary>
        public const int BGRA_BYTES_PER_PIXEL = 4;

        /// <summary>Depth buffer clear value (Vulkan: 1.0 = far plane).</summary>
        public const float DEPTH_CLEAR_VALUE = 1.0f;

        /// <summary>Default rasterizer line width for filled and textured pipelines.</summary>
        public const float DEFAULT_LINE_WIDTH = 1.0f;

        /// <summary>Line width for contour and overlay line pipelines.</summary>
        public const float OVERLAY_LINE_WIDTH = 1.75f;

        /// <summary>Minimum normalized depth for dynamic viewport setup.</summary>
        public const float VIEWPORT_MIN_DEPTH = 0f;

        /// <summary>Maximum normalized depth for dynamic viewport setup.</summary>
        public const float VIEWPORT_MAX_DEPTH = 1f;

        /// <summary>Lower bound when clamping surface overlay opacity push constants.</summary>
        public const float OVERLAY_OPACITY_MIN = 0f;

        /// <summary>Upper bound when clamping surface overlay opacity push constants.</summary>
        public const float OVERLAY_OPACITY_MAX = 1f;

        /// <summary>Default RGBA component for offscreen colour clears.</summary>
        public const float DEFAULT_CLEAR_COLOR_COMPONENT = 1f;

        /// <summary>Default opacity for coloured mesh and contour overlays.</summary>
        public const float DEFAULT_SURFACE_OVERLAY_OPACITY = 1f;

        /// <summary>Number of colour and depth attachments in the offscreen render pass.</summary>
        public const uint RENDER_PASS_ATTACHMENT_COUNT = 2;

        /// <summary>Number of clear values supplied at render-pass begin (colour + depth).</summary>
        public const uint CLEAR_VALUE_COUNT = 2;

        /// <summary>Vertex and fragment shader stages in each graphics pipeline.</summary>
        public const uint GRAPHICS_SHADER_STAGE_COUNT = 2;

        /// <summary>Dynamic viewport and scissor states configured at draw time.</summary>
        public const uint DYNAMIC_STATE_COUNT = 2;

        /// <summary>Colour blend attachments per subpass.</summary>
        public const uint COLOR_BLEND_ATTACHMENT_COUNT = 1;

        /// <summary>Byte size of the overlay-opacity push-constant block.</summary>
        public const uint PUSH_CONSTANT_OVERLAY_OPACITY_SIZE_BYTES = sizeof(float);

        /// <summary>Bit shift used to decode the major component of a Vulkan API version integer.</summary>
        public const int API_VERSION_MAJOR_SHIFT = 22;

        /// <summary>Bit shift used to decode the minor component of a Vulkan API version integer.</summary>
        public const int API_VERSION_MINOR_SHIFT = 12;

        /// <summary>Bitmask for the major component of a Vulkan API version integer.</summary>
        public const uint API_VERSION_MAJOR_MASK = 0x3FF;

        /// <summary>Bitmask for the patch component of a Vulkan API version integer.</summary>
        public const uint API_VERSION_PATCH_MASK = 0xFFF;

        /// <summary>Total byte size of the std140 camera uniform buffer object.</summary>
        public const int CAMERA_UNIFORM_SIZE_BYTES = 64;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.ViewportWidth"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_VIEWPORT_WIDTH_OFFSET = 0;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.ViewportHeight"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_VIEWPORT_HEIGHT_OFFSET = 4;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.ViewportMidX"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_VIEWPORT_MID_X_OFFSET = 8;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.ViewportMidY"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_VIEWPORT_MID_Y_OFFSET = 12;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.Scale"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_SCALE_OFFSET = 16;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.CosH"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_COS_H_OFFSET = 20;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.SinH"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_SIN_H_OFFSET = 24;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.CosP"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_COS_P_OFFSET = 28;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.SinP"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_SIN_P_OFFSET = 32;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.DepthMin"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_DEPTH_MIN_OFFSET = 36;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.DepthMax"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_DEPTH_MAX_OFFSET = 40;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.PivotEastM"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_PIVOT_EAST_OFFSET = 44;

        /// <summary>Byte offset of <see cref="CameraGpuUniforms.PivotNorthM"/> in the camera UBO.</summary>
        public const int CAMERA_UNIFORM_PIVOT_NORTH_OFFSET = 48;

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.ViewportWidth"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_VIEWPORT_WIDTH = CAMERA_UNIFORM_VIEWPORT_WIDTH_OFFSET / sizeof(float);

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.ViewportHeight"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_VIEWPORT_HEIGHT = CAMERA_UNIFORM_VIEWPORT_HEIGHT_OFFSET / sizeof(float);

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.ViewportMidX"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_VIEWPORT_MID_X = CAMERA_UNIFORM_VIEWPORT_MID_X_OFFSET / sizeof(float);

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.ViewportMidY"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_VIEWPORT_MID_Y = CAMERA_UNIFORM_VIEWPORT_MID_Y_OFFSET / sizeof(float);

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.Scale"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_SCALE = CAMERA_UNIFORM_SCALE_OFFSET / sizeof(float);

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.CosH"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_COS_H = CAMERA_UNIFORM_COS_H_OFFSET / sizeof(float);

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.SinH"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_SIN_H = CAMERA_UNIFORM_SIN_H_OFFSET / sizeof(float);

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.CosP"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_COS_P = CAMERA_UNIFORM_COS_P_OFFSET / sizeof(float);

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.SinP"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_SIN_P = CAMERA_UNIFORM_SIN_P_OFFSET / sizeof(float);

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.DepthMin"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_DEPTH_MIN = CAMERA_UNIFORM_DEPTH_MIN_OFFSET / sizeof(float);

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.DepthMax"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_DEPTH_MAX = CAMERA_UNIFORM_DEPTH_MAX_OFFSET / sizeof(float);

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.PivotEastM"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_PIVOT_EAST = CAMERA_UNIFORM_PIVOT_EAST_OFFSET / sizeof(float);

        /// <summary>Float slot index for <see cref="CameraGpuUniforms.PivotNorthM"/> in manual UBO uploads.</summary>
        public const int CAMERA_UNIFORM_FLOAT_PIVOT_NORTH = CAMERA_UNIFORM_PIVOT_NORTH_OFFSET / sizeof(float);

        /// <summary>Sentinel generation value indicating the GPU tile cache has not been initialized.</summary>
        public const int INVALID_GPU_TILE_CACHE_GENERATION = -1;
    }
}

namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Shared orbit-camera projection math for CPU rendering and Vulkan vertex shaders.
    /// </summary>
    static public class FieldCameraProjection
    {
        /// <summary>Multiplier applied when mapping screen coordinates to NDC.</summary>
        private const float NDC_SCALE_FACTOR = 2.0f;

        /// <summary>Offset applied when mapping screen coordinates to NDC.</summary>
        private const float NDC_OFFSET = 1.0f;

        /// <summary>Minimum camera depth range used to avoid divide-by-zero in NDC mapping.</summary>
        private const double MIN_DEPTH_RANGE = 1.0e-6;

        /// <summary>Minimum normalized depth value after clamping.</summary>
        private const double DEPTH_NORMALIZED_MIN = 0.0;

        /// <summary>Maximum normalized depth value after clamping.</summary>
        private const double DEPTH_NORMALIZED_MAX = 1.0;

        /// <summary>
        /// Projects model-space east/north/Z to screen pixels and camera depth.
        /// </summary>
        /// <param name="OffsetEastM">East offset from the field centre in metres.</param>
        /// <param name="OffsetNorthM">North offset from the field centre in metres.</param>
        /// <param name="ModelZ">Model-space vertical coordinate in metres.</param>
        /// <param name="ViewportWidth">Viewport width in pixels.</param>
        /// <param name="ViewportHeight">Viewport height in pixels.</param>
        /// <param name="ViewportMidX">Horizontal viewport centre in pixels.</param>
        /// <param name="ViewportMidY">Vertical viewport centre in pixels.</param>
        /// <param name="Scale">Pixels per model metre at the current zoom.</param>
        /// <param name="CosH">Cosine of the camera heading in radians.</param>
        /// <param name="SinH">Sine of the camera heading in radians.</param>
        /// <param name="CosP">Cosine of the camera pitch in radians.</param>
        /// <param name="SinP">Sine of the camera pitch in radians.</param>
        /// <param name="ScreenX">Output horizontal screen coordinate in pixels.</param>
        /// <param name="ScreenY">Output vertical screen coordinate in pixels.</param>
        /// <param name="Depth">Output camera-space depth in metres.</param>
        static public void ProjectModelOffset
            (
            double OffsetEastM,
            double OffsetNorthM,
            double ModelZ,
            double ViewportWidth,
            double ViewportHeight,
            double ViewportMidX,
            double ViewportMidY,
            double Scale,
            double CosH,
            double SinH,
            double CosP,
            double SinP,
            out float ScreenX,
            out float ScreenY,
            out double Depth
            )
        {
            double Rx = (OffsetEastM * CosH) - (OffsetNorthM * SinH);
            double Fy = (OffsetEastM * SinH) + (OffsetNorthM * CosH);

            Depth = (Fy * CosP) + (ModelZ * SinP);
            double Up = (ModelZ * CosP) - (Fy * SinP);

            ScreenX = (float)(ViewportMidX - (Rx * Scale));
            ScreenY = (float)(ViewportMidY - (Up * Scale));
        }

        /// <summary>
        /// Maps screen Y (pixels, origin top) to Vulkan NDC Y (origin top = -1).
        /// </summary>
        /// <param name="ScreenY">Vertical screen coordinate in pixels.</param>
        /// <param name="ViewportHeight">Viewport height in pixels.</param>
        /// <returns>Vulkan NDC Y coordinate.</returns>
        static public float ScreenYToVulkanNdcY
            (
            float ScreenY,
            float ViewportHeight
            )
        {
            return (ScreenY / ViewportHeight) * NDC_SCALE_FACTOR - NDC_OFFSET;
        }

        /// <summary>
        /// Maps camera depth to Vulkan NDC Z (0 = near, 1 = far).
        /// </summary>
        /// <param name="Depth">Camera-space depth in metres.</param>
        /// <param name="DepthMin">Near depth of the visible range in metres.</param>
        /// <param name="DepthMax">Far depth of the visible range in metres.</param>
        /// <returns>Vulkan NDC Z coordinate.</returns>
        static public float CameraDepthToVulkanNdcZ
            (
            double Depth,
            double DepthMin,
            double DepthMax
            )
        {
            double DepthRange = Math.Max(DepthMax - DepthMin, MIN_DEPTH_RANGE);
            double Normalized = Math.Clamp((Depth - DepthMin) / DepthRange, DEPTH_NORMALIZED_MIN, DEPTH_NORMALIZED_MAX);
            return (float)(NDC_OFFSET - Normalized);
        }
    }
}


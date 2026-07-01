namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Shared orbit-camera projection math for CPU rendering and Vulkan vertex shaders.
    /// </summary>
    static public class FieldCameraProjection
    {
        /// <summary>
        /// Projects model-space east/north/Z to screen pixels and camera depth.
        /// </summary>
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
        static public float ScreenYToVulkanNdcY
            (
            float ScreenY,
            float ViewportHeight
            )
        {
            return (ScreenY / ViewportHeight) * 2.0f - 1.0f;
        }

        /// <summary>
        /// Maps camera depth to Vulkan NDC Z (0 = near, 1 = far).
        /// </summary>
        static public float CameraDepthToVulkanNdcZ
            (
            double Depth,
            double DepthMin,
            double DepthMax
            )
        {
            double DepthRange = Math.Max(DepthMax - DepthMin, 1.0e-6);
            double Normalized = Math.Clamp((Depth - DepthMin) / DepthRange, 0.0, 1.0);
            return (float)(1.0 - Normalized);
        }
    }
}

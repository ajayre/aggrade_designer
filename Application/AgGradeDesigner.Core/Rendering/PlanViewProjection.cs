using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Rendering.Vulkan;

namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Screen-space projection helpers shared by 2D Skia and Vulkan plan-view rendering.
    /// </summary>
    static public class PlanViewProjection
    {
        /// <summary>Depth layer for satellite and background fills (far plane).</summary>
        public const float DEPTH_BACKGROUND = 0.05f;

        /// <summary>Depth layer for the colormap surface mesh.</summary>
        public const float DEPTH_SURFACE = 0.5f;

        /// <summary>Depth layer for contour polylines.</summary>
        public const float DEPTH_CONTOUR = 0.65f;

        /// <summary>Depth layer for field border lines (near plane).</summary>
        public const float DEPTH_BORDER = 0.8f;

        /// <summary>
        /// Builds camera uniforms for plan-view Vulkan shaders (viewport size only).
        /// </summary>
        static public CameraGpuUniforms CreateUniforms
            (
            float ViewportWidth,
            float ViewportHeight
            )
        {
            return new CameraGpuUniforms
            {
                ViewportWidth = ViewportWidth,
                ViewportHeight = ViewportHeight,
                ViewportMidX = ViewportWidth * (float)GeoConstants.VIEWPORT_CENTRE_FRACTION,
                ViewportMidY = ViewportHeight * (float)GeoConstants.VIEWPORT_CENTRE_FRACTION,
                Scale = 1f,
                CosH = 1f,
                SinH = 0f,
                CosP = 1f,
                SinP = 0f,
                DepthMin = 0f,
                DepthMax = 1f
            };
        }

        /// <summary>
        /// Projects a geographic point to viewport screen coordinates.
        /// </summary>
        static public bool LatLonToScreen
            (
            double Lat,
            double Lon,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            MapViewport Viewport,
            out float ScreenX,
            out float ScreenY
            )
        {
            return MapProjection.LatLonToScreen(
                Lat,
                Lon,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport,
                out ScreenX,
                out ScreenY);
        }

        /// <summary>
        /// Creates a coloured plan-view vertex in screen pixel coordinates.
        /// </summary>
        static public GpuVertex ScreenColoredVertex
            (
            float ScreenX,
            float ScreenY,
            float Depth,
            byte Red,
            byte Green,
            byte Blue,
            byte Alpha = 255
            )
        {
            const float ColorScale = 1f / 255f;
            return new GpuVertex
            {
                EastM = ScreenX,
                NorthM = ScreenY,
                ModelZ = Depth,
                R = Red * ColorScale,
                G = Green * ColorScale,
                B = Blue * ColorScale,
                A = Alpha * ColorScale
            };
        }

        /// <summary>
        /// Converts a top-origin bitmap V coordinate for plan-view shaders (screen space is top-origin).
        /// </summary>
        static public float FlipBitmapTextureV
            (
            float NormalizedV
            )
        {
            return NormalizedV;
        }

        /// <summary>
        /// Creates a textured plan-view vertex in screen pixel coordinates.
        /// </summary>
        static public GpuTexturedVertex ScreenTexturedVertex
            (
            float ScreenX,
            float ScreenY,
            float Depth,
            float U,
            float V
            )
        {
            return new GpuTexturedVertex
            {
                EastM = ScreenX,
                NorthM = ScreenY,
                ModelZ = Depth,
                U = U,
                V = V
            };
        }
    }
}

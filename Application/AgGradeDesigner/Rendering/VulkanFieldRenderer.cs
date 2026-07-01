using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Rendering.Vulkan;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// GPU path for 3D field rendering via Vulkan offscreen targets.
    /// </summary>
    static internal class VulkanFieldRenderer
    {
        /// <summary>
        /// Renders the full 3D scene (satellite plinth, mesh, contours, border) via Vulkan.
        /// </summary>
        static public bool TryRenderScene
            (
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            FieldCamera3D Camera,
            SatelliteTileService? Satellite,
            bool IncludeColoredMesh,
            bool IncludeLineDecorations,
            IProgress<int>? Progress,
            out SKBitmap? SceneBitmap,
            out string? FailureReason
            )
        {
            SceneBitmap = null;
            FailureReason = null;

            if (!GraphicsRuntime.TryRenderGpuFieldFrame(
                    Field3DGeometryBuilder.BuildFrame(
                        Surface,
                        Project,
                        ContentView,
                        Display,
                        Camera,
                        Viewport,
                        Satellite,
                        IncludeColoredMesh,
                        IncludeLineDecorations,
                        Progress),
                    (int)Viewport.Width,
                    (int)Viewport.Height,
                    out byte[]? Pixels,
                    out FailureReason))
            {
                return false;
            }

            int Width = Math.Max(1, (int)Viewport.Width);
            int Height = Math.Max(1, (int)Viewport.Height);
            SKBitmap Bitmap = new SKBitmap(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKPixmap Pixmap = Bitmap.PeekPixels();
            System.Runtime.InteropServices.Marshal.Copy(Pixels!, 0, Pixmap.GetPixels(), Pixels!.Length);
            SceneBitmap = Bitmap;
            return true;
        }

        /// <summary>
        /// Renders only the coloured mesh and contour lines on a transparent background.
        /// </summary>
        static public bool TryRenderOverlay
            (
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            FieldCamera3D Camera,
            IProgress<int>? Progress,
            out SKBitmap? SceneBitmap,
            out string? FailureReason
            )
        {
            SceneBitmap = null;
            FailureReason = null;

            if (!GraphicsRuntime.TryRenderGpuFieldFrame(
                    Field3DGeometryBuilder.BuildOverlayFrame(
                        Surface,
                        Project,
                        ContentView,
                        Display,
                        Camera,
                        Viewport,
                        Progress),
                    (int)Viewport.Width,
                    (int)Viewport.Height,
                    out byte[]? Pixels,
                    out FailureReason))
            {
                return false;
            }

            int Width = Math.Max(1, (int)Viewport.Width);
            int Height = Math.Max(1, (int)Viewport.Height);
            SKBitmap Bitmap = new SKBitmap(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKPixmap Pixmap = Bitmap.PeekPixels();
            System.Runtime.InteropServices.Marshal.Copy(Pixels!, 0, Pixmap.GetPixels(), Pixels!.Length);
            SceneBitmap = Bitmap;
            return true;
        }
    }
}

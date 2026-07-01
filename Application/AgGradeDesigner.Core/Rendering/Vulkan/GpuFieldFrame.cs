using System.Runtime.InteropServices;

namespace AgGradeDesigner.Core.Rendering.Vulkan
{
    /// <summary>
    /// Interleaved mesh/line vertex uploaded to the GPU (model east/north/Z + RGBA colour).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuVertex
    {
        public float EastM;
        public float NorthM;
        public float ModelZ;
        public float R;
        public float G;
        public float B;
        public float A;
    }

    /// <summary>
    /// Camera parameters shared with the field 3D Vulkan shaders (std140 layout).
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct CameraGpuUniforms
    {
        [FieldOffset(0)]
        public float ViewportWidth;

        [FieldOffset(4)]
        public float ViewportHeight;

        [FieldOffset(8)]
        public float ViewportMidX;

        [FieldOffset(12)]
        public float ViewportMidY;

        [FieldOffset(16)]
        public float Scale;

        [FieldOffset(20)]
        public float CosH;

        [FieldOffset(24)]
        public float SinH;

        [FieldOffset(28)]
        public float CosP;

        [FieldOffset(32)]
        public float SinP;

        [FieldOffset(36)]
        public float DepthMin;

        [FieldOffset(40)]
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
        public GpuTexturedVertex[] CombinedTextureVertices { get; init; } = Array.Empty<GpuTexturedVertex>();
        public GpuTextureDrawSpec[] TextureDraws { get; init; } = Array.Empty<GpuTextureDrawSpec>();

        public GpuVertex[] MeshVertices { get; init; } = Array.Empty<GpuVertex>();
        public GpuVertex[] LineVertices { get; init; } = Array.Empty<GpuVertex>();
        public CameraGpuUniforms Camera { get; init; }
        public int SatelliteCacheGeneration { get; init; }
        public float ClearR { get; init; } = 1f;
        public float ClearG { get; init; } = 1f;
        public float ClearB { get; init; } = 1f;
        public float ClearA { get; init; } = 1f;
    }
}

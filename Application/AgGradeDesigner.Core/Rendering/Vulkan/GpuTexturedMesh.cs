using System.Runtime.InteropServices;

namespace AgGradeDesigner.Core.Rendering.Vulkan
{
    /// <summary>
    /// Model-space vertex with UV coordinates for textured satellite tiles.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuTexturedVertex
    {
        public float EastM;
        public float NorthM;
        public float ModelZ;
        public float U;
        public float V;
    }

    /// <summary>
    /// One draw call for a cached satellite tile texture.
    /// </summary>
    public readonly struct GpuTextureDrawSpec
    {
        public int MeshIndex { get; init; }
        public uint FirstVertex { get; init; }
        public uint VertexCount { get; init; }
    }

    /// <summary>
    /// One satellite tile uploaded as a GPU texture and drawn as a triangle list.
    /// </summary>
    public sealed class GpuTextureMesh
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public int TileZoom { get; init; }
        public int TileX { get; init; }
        public int TileY { get; init; }
        public byte[] Pixels { get; init; } = Array.Empty<byte>();
        public GpuTexturedVertex[] Vertices { get; init; } = Array.Empty<GpuTexturedVertex>();
    }
}

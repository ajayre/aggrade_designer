using System.Runtime.InteropServices;

namespace AgGradeDesigner.Core.Rendering.Vulkan
{
    /// <summary>
    /// Model-space vertex with UV coordinates for textured satellite tiles.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuTexturedVertex
    {
        /// <summary>East offset from the field origin in metres.</summary>
        public float EastM;

        /// <summary>North offset from the field origin in metres.</summary>
        public float NorthM;

        /// <summary>Model-space elevation in metres.</summary>
        public float ModelZ;

        /// <summary>Horizontal texture coordinate in [0, 1].</summary>
        public float U;

        /// <summary>Vertical texture coordinate in [0, 1].</summary>
        public float V;
    }

    /// <summary>
    /// One draw call for a cached satellite tile texture.
    /// </summary>
    public readonly struct GpuTextureDrawSpec
    {
        /// <summary>Index into the frame's <see cref="GpuFieldFrame.TextureMeshes"/> array.</summary>
        public int MeshIndex { get; init; }

        /// <summary>First vertex index within the combined textured vertex buffer.</summary>
        public uint FirstVertex { get; init; }

        /// <summary>Number of vertices to draw for this tile.</summary>
        public uint VertexCount { get; init; }
    }

    /// <summary>
    /// One satellite tile uploaded as a GPU texture and drawn as a triangle list.
    /// </summary>
    public sealed class GpuTextureMesh
    {
        /// <summary>Tile image width in pixels.</summary>
        public int Width { get; init; }

        /// <summary>Tile image height in pixels.</summary>
        public int Height { get; init; }

        /// <summary>Web Mercator zoom level of the tile.</summary>
        public int TileZoom { get; init; }

        /// <summary>Tile column index at <see cref="TileZoom"/>.</summary>
        public int TileX { get; init; }

        /// <summary>Tile row index at <see cref="TileZoom"/>.</summary>
        public int TileY { get; init; }

        /// <summary>Raw BGRA8888 pixel bytes for GPU upload.</summary>
        public byte[] Pixels { get; init; } = Array.Empty<byte>();

        /// <summary>Triangle-list vertices covering the tile in model space.</summary>
        public GpuTexturedVertex[] Vertices { get; init; } = Array.Empty<GpuTexturedVertex>();
    }
}

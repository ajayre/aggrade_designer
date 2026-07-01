using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Rendering.Vulkan;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Model-space GPU geometry that is invariant under orbit rotation (heading/pitch).
    /// </summary>
    internal sealed class GpuFieldStaticGeometry
    {
        public GpuVertex[] BackgroundMeshVertices { get; init; } = Array.Empty<GpuVertex>();
        public GpuTextureMesh[] TextureMeshes { get; init; } = Array.Empty<GpuTextureMesh>();
        public GpuTexturedVertex[] CombinedTextureVertices { get; init; } = Array.Empty<GpuTexturedVertex>();
        public GpuTextureDrawSpec[] TextureDraws { get; init; } = Array.Empty<GpuTextureDrawSpec>();
        public GpuVertex[] MeshVertices { get; init; } = Array.Empty<GpuVertex>();
        public GpuVertex[] LineVertices { get; init; } = Array.Empty<GpuVertex>();
        public GpuVertex[] BorderLineVertices { get; init; } = Array.Empty<GpuVertex>();
        public float ClearR { get; init; } = 1f;
        public float ClearG { get; init; } = 1f;
        public float ClearB { get; init; } = 1f;
        public float ClearA { get; init; } = 1f;
    }

    /// <summary>
    /// Caches orbit-invariant 3D mesh geometry so orbit drag only updates camera uniforms.
    /// </summary>
    static internal class Field3DGeometryCache
    {
        private static readonly object Sync = new object();
        private static Field3DGeometryCacheKey _CachedKey;
        private static GpuFieldStaticGeometry? _CachedGeometry;

        /// <summary>Clears cached geometry when the surface or display configuration changes.</summary>
        static public void Invalidate
            (
            )
        {
            lock (Sync)
            {
                _CachedGeometry = null;
            }
        }

        /// <summary>
        /// Returns cached model-space geometry or rebuilds it when the cache key misses.
        /// </summary>
        static public GpuFieldStaticGeometry GetOrBuild
            (
            Field3DGeometryCacheKey Key,
            Func<GpuFieldStaticGeometry> Build
            )
        {
            lock (Sync)
            {
                if (_CachedGeometry != null && Key.Equals(_CachedKey))
                    return _CachedGeometry;

                _CachedGeometry = Build();
                _CachedKey = Key;
                return _CachedGeometry;
            }
        }
    }

    /// <summary>
    /// Fingerprint for geometry that does not depend on orbit heading or pitch.
    /// </summary>
    internal readonly struct Field3DGeometryCacheKey : IEquatable<Field3DGeometryCacheKey>
    {
        public FieldSurfaceSampler Surface { get; init; }
        public ProjectDocument Project { get; init; }
        public FieldContentView ContentView { get; init; }
        public bool ShowSatellite { get; init; }
        public bool Contours { get; init; }
        public bool Hillshade { get; init; }
        public double VerticalExaggeration { get; init; }
        public double OrbitCentreLat { get; init; }
        public double OrbitCentreLon { get; init; }
        public int SatelliteCacheGeneration { get; init; }

        public bool Equals
            (
            Field3DGeometryCacheKey Other
            )
        {
            return ReferenceEquals(Surface, Other.Surface)
                && ReferenceEquals(Project, Other.Project)
                && ContentView == Other.ContentView
                && ShowSatellite == Other.ShowSatellite
                && Contours == Other.Contours
                && Hillshade == Other.Hillshade
                && VerticalExaggeration.Equals(Other.VerticalExaggeration)
                && OrbitCentreLat.Equals(Other.OrbitCentreLat)
                && OrbitCentreLon.Equals(Other.OrbitCentreLon)
                && SatelliteCacheGeneration == Other.SatelliteCacheGeneration;
        }

        public override bool Equals
            (
            object? Obj
            )
        {
            return Obj is Field3DGeometryCacheKey Other && Equals(Other);
        }

        public override int GetHashCode
            (
            )
        {
            HashCode Hash = new HashCode();
            Hash.Add(Surface);
            Hash.Add(Project);
            Hash.Add(ContentView);
            Hash.Add(ShowSatellite);
            Hash.Add(Contours);
            Hash.Add(Hillshade);
            Hash.Add(VerticalExaggeration);
            Hash.Add(OrbitCentreLat);
            Hash.Add(OrbitCentreLon);
            Hash.Add(SatelliteCacheGeneration);
            return Hash.ToHashCode();
        }
    }
}

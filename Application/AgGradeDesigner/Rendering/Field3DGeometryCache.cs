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
        /// <summary>Coloured vertices for satellite plinth sides and other background solids.</summary>
        public GpuVertex[] BackgroundMeshVertices { get; init; } = Array.Empty<GpuVertex>();
        /// <summary>Per-tile textured meshes for the satellite ground plane.</summary>
        public GpuTextureMesh[] TextureMeshes { get; init; } = Array.Empty<GpuTextureMesh>();
        /// <summary>All textured vertices concatenated for a single GPU buffer upload.</summary>
        public GpuTexturedVertex[] CombinedTextureVertices { get; init; } = Array.Empty<GpuTexturedVertex>();
        /// <summary>Draw ranges indexing into <see cref="CombinedTextureVertices"/> per tile.</summary>
        public GpuTextureDrawSpec[] TextureDraws { get; init; } = Array.Empty<GpuTextureDrawSpec>();
        /// <summary>Coloured topo or cut/fill surface triangles in model space.</summary>
        public GpuVertex[] MeshVertices { get; init; } = Array.Empty<GpuVertex>();
        /// <summary>Contour and decoration line segments in model space.</summary>
        public GpuVertex[] LineVertices { get; init; } = Array.Empty<GpuVertex>();
        /// <summary>Field boundary polyline vertices in model space.</summary>
        public GpuVertex[] BorderLineVertices { get; init; } = Array.Empty<GpuVertex>();
        /// <summary>Clear colour red channel for the 3D frame background.</summary>
        public float ClearR { get; init; } = 1f;
        /// <summary>Clear colour green channel for the 3D frame background.</summary>
        public float ClearG { get; init; } = 1f;
        /// <summary>Clear colour blue channel for the 3D frame background.</summary>
        public float ClearB { get; init; } = 1f;
        /// <summary>Clear colour alpha channel for the 3D frame background.</summary>
        public float ClearA { get; init; } = 1f;
    }

    /// <summary>
    /// Caches orbit-invariant 3D mesh geometry so orbit drag only updates camera uniforms.
    /// </summary>
    static internal class Field3DGeometryCache
    {
        /// <summary>Synchronizes access to cached static geometry.</summary>
        private static readonly object Sync = new object();
        /// <summary>Cache key of the currently stored geometry.</summary>
        private static Field3DGeometryCacheKey _CachedKey;
        /// <summary>Cached orbit-invariant geometry, or null after invalidation.</summary>
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
        /// <param name="Key">Fingerprint of inputs that affect static geometry.</param>
        /// <param name="Build">Factory invoked on cache miss to produce new geometry.</param>
        /// <returns>Shared static geometry for the given key.</returns>
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
        /// <summary>Sampled field surface defining mesh topology and scalars.</summary>
        public FieldSurfaceSampler Surface { get; init; }
        /// <summary>Active project document (survey, design, viewer state).</summary>
        public ProjectDocument Project { get; init; }
        /// <summary>Scalar view (existing, proposed, cut/fill) used for mesh colouring.</summary>
        public FieldContentView ContentView { get; init; }
        /// <summary>Whether satellite background tiles are included.</summary>
        public bool ShowSatellite { get; init; }
        /// <summary>Whether contour line geometry is built.</summary>
        public bool Contours { get; init; }
        /// <summary>Whether hillshade modulation is applied to mesh colours.</summary>
        public bool Hillshade { get; init; }
        /// <summary>Vertical exaggeration factor baked into model-space Z.</summary>
        public double VerticalExaggeration { get; init; }
        /// <summary>Orbit pivot latitude used for model-space east/north offsets.</summary>
        public double OrbitCentreLat { get; init; }
        /// <summary>Orbit pivot longitude used for model-space east/north offsets.</summary>
        public double OrbitCentreLon { get; init; }
        /// <summary>Satellite tile cache generation; bumps when tiles reload.</summary>
        public int SatelliteCacheGeneration { get; init; }

        /// <summary>
        /// Compares all cache-key fields for equality.
        /// </summary>
        /// <param name="Other">Key to compare against.</param>
        /// <returns>True when every fingerprint field matches.</returns>
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

        /// <summary>
        /// Returns whether <paramref name="Obj"/> is an equal <see cref="Field3DGeometryCacheKey"/>.
        /// </summary>
        /// <param name="Obj">Object to compare.</param>
        /// <returns>True when <paramref name="Obj"/> is an equal key.</returns>
        public override bool Equals
            (
            object? Obj
            )
        {
            return Obj is Field3DGeometryCacheKey Other && Equals(Other);
        }

        /// <summary>
        /// Hash code combining all fingerprint fields.
        /// </summary>
        /// <returns>Combined hash for dictionary or cache lookup use.</returns>
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

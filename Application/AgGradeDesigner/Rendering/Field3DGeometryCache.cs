using System.Diagnostics;

using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering.Vulkan;
using AgGradeDesigner.Diagnostics;

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
    /// Caches orbit- and pan-invariant 3D mesh geometry so orbit drag and plan-view pan only update camera uniforms.
    /// </summary>
    static internal class Field3DGeometryCache
    {
        /// <summary>Synchronizes access to cached static geometry.</summary>
        private static readonly object Sync = new object();
        /// <summary>Cache key of the currently stored geometry.</summary>
        private static Field3DGeometryCacheKey _CachedKey;
        /// <summary>Cached orbit-invariant geometry, or null after invalidation.</summary>
        private static GpuFieldStaticGeometry? _CachedGeometry;
        /// <summary>Test hook: geometry rebuilds observed since the last reset.</summary>
        private static int _TestGeometryBuildCount;

        /// <summary>Clears cached geometry when display configuration changes require a hard reset.</summary>
        static public void Invalidate
            (
            )
        {
            lock (Sync)
            {
                _CachedGeometry = null;
            }
        }

        /// <summary>Resets test-only geometry rebuild counters without invalidating cached geometry.</summary>
        static internal void TestResetGeometryBuildCount
            (
            )
        {
            lock (Sync)
            {
                _TestGeometryBuildCount = 0;
            }
        }

        /// <summary>Returns geometry rebuild count since the last test reset.</summary>
        static internal int TestGeometryCacheBuildCount
        {
            get
            {
                lock (Sync)
                {
                    return _TestGeometryBuildCount;
                }
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
                if (_CachedGeometry != null &&
                    Key.Equals(_CachedKey) &&
                    !Key.IsGeometryDirty())
                {
                    if (Key.ContentView == FieldContentView.Haul)
                    {
                        HaulThreeDInstrumentation.LogGeometryCacheDecision(
                            Hit: true,
                            Key,
                            _CachedGeometry.MeshVertices.Length,
                            _CachedGeometry.LineVertices.Length,
                            BuildElapsedMs: 0);
                    }

                    return _CachedGeometry;
                }

                Stopwatch BuildWatch = Stopwatch.StartNew();
                _CachedGeometry = Build();
                BuildWatch.Stop();
                _CachedKey = Key;
                _TestGeometryBuildCount++;
                Key.ClearGeometryDirtyAfterBuild();
                if (Key.ContentView == FieldContentView.Haul)
                {
                    HaulThreeDInstrumentation.LogGeometryCacheDecision(
                        Hit: false,
                        Key,
                        _CachedGeometry.MeshVertices.Length,
                        _CachedGeometry.LineVertices.Length,
                        BuildWatch.ElapsedMilliseconds);
                }

                return _CachedGeometry;
            }
        }
    }

    /// <summary>
    /// Fingerprint for geometry that does not depend on orbit heading, pitch, or pan pivot.
    /// </summary>
    internal readonly struct Field3DGeometryCacheKey : IEquatable<Field3DGeometryCacheKey>
    {
        /// <summary>Active project document (survey, design, viewer state).</summary>
        public ProjectDocument Project { get; init; }
        /// <summary>Scalar view (existing, proposed, cut/fill, haul) used for mesh colouring.</summary>
        public FieldContentView ContentView { get; init; }
        /// <summary>Whether hillshade modulation is applied to mesh colours.</summary>
        public bool Hillshade { get; init; }
        /// <summary>Bumps when true-scale relief Z storage rules change to invalidate stale entries.</summary>
        public int TrueScaleReliefStorageVersion { get; init; }

        /// <summary>
        /// Pinned haul route scrub index for haul 3D meshes, or -1 when not applicable.
        /// </summary>
        public int HaulRouteScrubIndex { get; init; }

        /// <summary>
        /// Returns true when project topo dirty flags require rebuilding this content view.
        /// </summary>
        public bool IsGeometryDirty
            (
            )
        {
            return Project == null || Project.TopoGeometryDirty.IsDirtyForContentView(ContentView);
        }

        /// <summary>Clears topo dirty flags after a successful geometry build for this view.</summary>
        public void ClearGeometryDirtyAfterBuild
            (
            )
        {
            Project?.TopoGeometryDirty.ClearAfterBuild(ContentView);
        }

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
            return ReferenceEquals(Project, Other.Project)
                && ContentView == Other.ContentView
                && Hillshade == Other.Hillshade
                && HaulRouteScrubIndex == Other.HaulRouteScrubIndex
                && TrueScaleReliefStorageVersion == Other.TrueScaleReliefStorageVersion;
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
            Hash.Add(Project);
            Hash.Add(ContentView);
            Hash.Add(Hillshade);
            Hash.Add(HaulRouteScrubIndex);
            Hash.Add(TrueScaleReliefStorageVersion);
            return Hash.ToHashCode();
        }
    }
}

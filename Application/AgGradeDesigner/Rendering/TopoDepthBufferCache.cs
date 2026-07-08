using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Reuses topo depth buffers between split 3D render passes for the same camera frame.
    /// </summary>
    internal static class TopoDepthBufferCache
    {
        /// <summary>Synchronizes access to cached depth-buffer state.</summary>
        private static readonly object Sync = new object();

        /// <summary>Cached depth buffer from the most recent compatible surface-overlay pass.</summary>
        private static MeshDepthBuffer? CachedBuffer;

        /// <summary>Cache key describing the frame that produced <see cref="CachedBuffer"/>.</summary>
        private static TopoDepthCacheKey CachedKey;

        /// <summary>
        /// Stores a topo depth buffer built during the surface-overlay pass for decoration reuse.
        /// </summary>
        /// <param name="Key">Frame key for the supplied buffer.</param>
        /// <param name="Buffer">Depth buffer to cache.</param>
        public static void Store(TopoDepthCacheKey Key, MeshDepthBuffer Buffer)
        {
            if (Buffer.IsPassthrough)
                return;

            lock (Sync)
            {
                CachedKey = Key;
                CachedBuffer = Buffer;
            }
        }

        /// <summary>
        /// Returns a cached topo depth buffer or builds one when haul-flow occlusion needs it.
        /// </summary>
        /// <param name="Viewport">Active viewport rectangle.</param>
        /// <param name="Surface">Scalar surface sampler.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <param name="Camera">Orbit camera.</param>
        /// <param name="BuildWhenMissing">Factory used when no compatible cache entry exists.</param>
        /// <returns>Depth buffer for overlay occlusion testing.</returns>
        public static MeshDepthBuffer ResolveOrBuild
            (
            SKRect Viewport,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            Func<MeshDepthBuffer> BuildWhenMissing
            )
        {
            TopoDepthCacheKey Key = TopoDepthCacheKey.From(Viewport, Surface, ContentView, Camera);
            lock (Sync)
            {
                if (CachedBuffer != null && CachedKey.Equals(Key))
                    return CachedBuffer;
            }

            MeshDepthBuffer Built = BuildWhenMissing();
            if (!Built.IsPassthrough)
            {
                lock (Sync)
                {
                    CachedKey = Key;
                    CachedBuffer = Built;
                }
            }

            return Built;
        }

        /// <summary>
        /// Cache key tying a topo depth buffer to one 3D camera frame.
        /// </summary>
        internal readonly struct TopoDepthCacheKey : IEquatable<TopoDepthCacheKey>
        {
            /// <summary>Viewport width in pixels.</summary>
            public int ViewportWidth { get; init; }

            /// <summary>Viewport height in pixels.</summary>
            public int ViewportHeight { get; init; }

            /// <summary>Active scalar content view.</summary>
            public FieldContentView ContentView { get; init; }

            /// <summary>Surface sampler instance for the open project.</summary>
            public FieldSurfaceSampler Surface { get; init; }

            /// <summary>Orbit pivot latitude in degrees.</summary>
            public double CentreLat { get; init; }

            /// <summary>Orbit pivot longitude in degrees.</summary>
            public double CentreLon { get; init; }

            /// <summary>Orbit heading in radians.</summary>
            public double HeadingRad { get; init; }

            /// <summary>Orbit pitch in radians.</summary>
            public double PitchRad { get; init; }

            /// <summary>Camera distance from the orbit pivot in metres.</summary>
            public double DistanceM { get; init; }

            /// <summary>Vertical exaggeration applied during projection.</summary>
            public double VerticalExaggeration { get; init; }

            /// <summary>Ground-plane clearance applied to relief geometry.</summary>
            public double GroundPlaneClearanceM { get; init; }

            /// <summary>
            /// Builds a cache key for one 3D frame.
            /// </summary>
            /// <param name="Viewport">Active viewport rectangle.</param>
            /// <param name="Surface">Scalar surface sampler.</param>
            /// <param name="ContentView">Active field content view.</param>
            /// <param name="Camera">Orbit camera.</param>
            /// <returns>Frame cache key.</returns>
            public static TopoDepthCacheKey From
                (
                SKRect Viewport,
                FieldSurfaceSampler Surface,
                FieldContentView ContentView,
                FieldCamera3D Camera
                )
            {
                return new TopoDepthCacheKey
                {
                    ViewportWidth = (int)Viewport.Width,
                    ViewportHeight = (int)Viewport.Height,
                    ContentView = ContentView,
                    Surface = Surface,
                    CentreLat = Camera.CentreLat,
                    CentreLon = Camera.CentreLon,
                    HeadingRad = Camera.HeadingRad,
                    PitchRad = Camera.PitchRad,
                    DistanceM = Camera.DistanceM,
                    VerticalExaggeration = Camera.VerticalExaggeration,
                    GroundPlaneClearanceM = Camera.GroundPlaneClearanceM
                };
            }

            /// <inheritdoc/>
            public bool Equals(TopoDepthCacheKey Other)
            {
                return ViewportWidth == Other.ViewportWidth &&
                    ViewportHeight == Other.ViewportHeight &&
                    ContentView == Other.ContentView &&
                    ReferenceEquals(Surface, Other.Surface) &&
                    CentreLat.Equals(Other.CentreLat) &&
                    CentreLon.Equals(Other.CentreLon) &&
                    HeadingRad.Equals(Other.HeadingRad) &&
                    PitchRad.Equals(Other.PitchRad) &&
                    DistanceM.Equals(Other.DistanceM) &&
                    VerticalExaggeration.Equals(Other.VerticalExaggeration) &&
                    GroundPlaneClearanceM.Equals(Other.GroundPlaneClearanceM);
            }

            /// <inheritdoc/>
            public override bool Equals(object? Obj) => Obj is TopoDepthCacheKey Other && Equals(Other);

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                HashCode Hash = new HashCode();
                Hash.Add(ViewportWidth);
                Hash.Add(ViewportHeight);
                Hash.Add(ContentView);
                Hash.Add(Surface);
                Hash.Add(CentreLat);
                Hash.Add(CentreLon);
                Hash.Add(HeadingRad);
                Hash.Add(PitchRad);
                Hash.Add(DistanceM);
                Hash.Add(VerticalExaggeration);
                Hash.Add(GroundPlaneClearanceM);
                return Hash.ToHashCode();
            }
        }
    }
}

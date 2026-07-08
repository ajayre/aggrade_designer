using AgGradeDesigner.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Unit tests for <see cref="MeshDepthBuffer"/> overlay occlusion behaviour.
    /// </summary>
    public sealed class MeshDepthBufferTests
    {
        /// <summary>Viewport width used when building test depth buffers.</summary>
        private const int VIEWPORT_WIDTH = 400;

        /// <summary>Viewport height used when building test depth buffers.</summary>
        private const int VIEWPORT_HEIGHT = 300;

        /// <summary>Camera depth assigned to the occluding test triangle.</summary>
        private const double OCCLUDER_DEPTH = 1000.0;

        /// <summary>Camera depth behind the occluding triangle.</summary>
        private const double OCCLUDED_DEPTH = 100.0;

        /// <summary>Screen X inside the occluding triangle.</summary>
        private const float INTERIOR_SCREEN_X = 200f;

        /// <summary>Screen Y inside the occluding triangle.</summary>
        private const float INTERIOR_SCREEN_Y = 150f;

        /// <summary>
        /// Verifies passthrough depth buffers treat every sample as visible.
        /// </summary>
        [Fact]
        public void IsVisible_PassthroughBuffer_AlwaysReturnsTrue()
        {
            Assert.True(MeshDepthBuffer.AlwaysVisible.IsVisible(INTERIOR_SCREEN_X, INTERIOR_SCREEN_Y, OCCLUDED_DEPTH));
        }

        /// <summary>
        /// Verifies samples behind a closer mesh triangle are rejected.
        /// </summary>
        [Fact]
        public void IsVisible_BehindCloserTriangle_ReturnsFalse()
        {
            MeshDepthBuffer Buffer = BuildFullScreenOccluder();
            Assert.False(Buffer.IsVisible(INTERIOR_SCREEN_X, INTERIOR_SCREEN_Y, OCCLUDED_DEPTH));
        }

        /// <summary>
        /// Verifies samples in front of a closer mesh triangle remain visible.
        /// </summary>
        [Fact]
        public void IsVisible_InFrontOfTriangle_ReturnsTrue()
        {
            MeshDepthBuffer Buffer = BuildFullScreenOccluder();
            Assert.True(Buffer.IsVisible(INTERIOR_SCREEN_X, INTERIOR_SCREEN_Y, OCCLUDER_DEPTH + 10.0));
        }

        /// <summary>
        /// Builds a depth buffer with one screen-filling triangle close to the camera.
        /// </summary>
        private static MeshDepthBuffer BuildFullScreenOccluder()
        {
            SKRect Viewport = SKRect.Create(0, 0, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);
            MeshTriangle Occluder = new MeshTriangle
            {
                P0 = new SKPoint(0, 0),
                P1 = new SKPoint(VIEWPORT_WIDTH, 0),
                P2 = new SKPoint(VIEWPORT_WIDTH * 0.5f, VIEWPORT_HEIGHT),
                C0 = SKColors.White,
                C1 = SKColors.White,
                C2 = SKColors.White,
                D0 = OCCLUDER_DEPTH,
                D1 = OCCLUDER_DEPTH,
                D2 = OCCLUDER_DEPTH,
                Depth = OCCLUDER_DEPTH
            };

            return MeshDepthBuffer.Build(Viewport, new[] { Occluder });
        }
    }
}

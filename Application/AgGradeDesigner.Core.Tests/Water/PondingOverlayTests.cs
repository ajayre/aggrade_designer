using AgGradeDesigner.Core.Water;

namespace AgGradeDesigner.Core.Tests.Water
{
    /// <summary>
    /// Unit tests for ponding overlay depth filtering and absolute colour scaling.
    /// </summary>
    public sealed class PondingOverlayTests
    {
        /// <summary>
        /// Verifies cells below the minimum depth threshold are fully transparent.
        /// </summary>
        [Fact]
        public void BuildOverlayRgba_BelowMinimumDepth_IsTransparent
            (
            )
        {
            float[] Depths = [0.01f, 0.05f];
            byte[] Overlay = PondingAnalysisEngine.BuildOverlayRgba(
                Depths,
                2,
                1,
                MinimumDepthMm: 25,
                OverlayMaxDepthMm: WaterConstants.POND_DEPTH_OVERLAY_MAX_MM);

            Assert.Equal(0, Overlay[3]);
            Assert.True(Overlay[7] > 0);
        }

        /// <summary>
        /// Verifies shallow ponding is faint on the fixed depth scale instead of normalized to full opacity.
        /// </summary>
        [Fact]
        public void BuildOverlayRgba_UsesAbsoluteScaleNotFieldMaximum
            (
            )
        {
            float[] Depths = [0.001f];
            byte[] Overlay = PondingAnalysisEngine.BuildOverlayRgba(
                Depths,
                1,
                1,
                MinimumDepthMm: 0,
                OverlayMaxDepthMm: 100);

            Assert.True(Overlay[3] > 0);
            Assert.True(Overlay[3] < WaterConstants.WATER_OVERLAY_MAX_ALPHA / 2);
        }

        /// <summary>
        /// Verifies changing display settings rebuilds the overlay without altering stored depths.
        /// </summary>
        [Fact]
        public void RefreshOverlay_RebuildsRgbaFromStoredDepths
            (
            )
        {
            float[] Depths = [0.01f, 0.01f];
            WaterLayerCacheEntry Entry = new WaterLayerCacheEntry
            {
                GridWidth = 2,
                GridHeight = 1,
                CellSizeM = 1.0,
                PondDepthM = Depths,
            };
            PondingAnalysisEngine.AssignSmoothOverlay(Entry, new WaterSettings { MinimumPondDepthMm = 0 });
            Assert.Contains(Entry.OverlayRgba, Value => Value > 0);

            WaterSettings StrictDisplay = new WaterSettings { MinimumPondDepthMm = 25 };
            PondingAnalysisEngine.RefreshOverlay(Entry, StrictDisplay);

            for (Int32 Index = 3; Index < Entry.OverlayRgba.Length; Index += 4)
                Assert.Equal(0, Entry.OverlayRgba[Index]);
            Assert.Equal(0.01f, Entry.PondDepthM[0]);
        }
        /// <summary>
        /// Verifies bilinear depth sampling smooths transitions between shallow and deep neighbours.
        /// </summary>
        [Fact]
        public void TrySamplePondDepthBilinear_InterpolatesBetweenNeighbours
            (
            )
        {
            float[] Depths = [0.02f, 0.06f, 0.02f, 0.06f];
            Assert.True(PondingAnalysisEngine.TrySamplePondDepthBilinear(Depths, 2, 2, 0.5, 0.5, out float DepthM));
            Assert.InRange(DepthM, 0.03f, 0.05f);
        }

        /// <summary>
        /// Verifies deeper ponding produces higher overlay alpha than shallow ponding.
        /// </summary>
        [Fact]
        public void ComputeOverlayAlpha_IncreasesWithDepth
            (
            )
        {
            byte Shallow = PondingAnalysisEngine.ComputeOverlayAlpha(0.02f, 0, WaterConstants.POND_DEPTH_OVERLAY_MAX_MM);
            byte Deep = PondingAnalysisEngine.ComputeOverlayAlpha(0.08f, 0, WaterConstants.POND_DEPTH_OVERLAY_MAX_MM);
            Assert.True(Deep > Shallow);
        }

        /// <summary>
        /// Verifies minimum depth uses a soft fade instead of a hard cutoff.
        /// </summary>
        [Fact]
        public void ComputeOverlayPixel_SoftFadesNearMinimumDepth
            (
            )
        {
            WaterOverlayPixel Below = PondingAnalysisEngine.ComputeOverlayPixel(0.014f, 25, WaterConstants.POND_DEPTH_OVERLAY_MAX_MM);
            WaterOverlayPixel Above = PondingAnalysisEngine.ComputeOverlayPixel(0.040f, 25, WaterConstants.POND_DEPTH_OVERLAY_MAX_MM);
            Assert.False(Below.IsVisible);
            Assert.True(Above.IsVisible);
            Assert.True(Above.A > PondingAnalysisEngine.ComputeOverlayPixel(0.030f, 25, WaterConstants.POND_DEPTH_OVERLAY_MAX_MM).A);
        }

        /// <summary>
        /// Verifies deeper ponding uses a darker tint than shallow ponding.
        /// </summary>
        [Fact]
        public void ComputeOverlayPixel_DarkensWithDepth
            (
            )
        {
            WaterOverlayPixel Shallow = PondingAnalysisEngine.ComputeOverlayPixel(0.03f, 0, WaterConstants.POND_DEPTH_OVERLAY_MAX_MM);
            WaterOverlayPixel Deep = PondingAnalysisEngine.ComputeOverlayPixel(0.09f, 0, WaterConstants.POND_DEPTH_OVERLAY_MAX_MM);
            Assert.True(Deep.B < Shallow.B);
            Assert.True(Deep.A > Shallow.A);
        }
    }
}

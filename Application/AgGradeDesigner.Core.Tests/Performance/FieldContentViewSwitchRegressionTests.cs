using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.Integration;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Regression tests ensuring existing/proposed switches do not discard prepared 3D frames via surface invalidation.
    /// </summary>
    [Collection("ImportHandoff")]
    [Trait("Category", "RenderingRegression")]
    [Trait("Category", "FieldContentViewSwitch")]
    public sealed class FieldContentViewSwitchRegressionTests
    {
        /// <summary>
        /// Warm proposed ↔ existing switches must not route water-toggle sync through
        /// <see cref="AgGradeDesigner.Controls.FieldViewerControl.InvalidateSurface"/>, which wipes prepared frame caches.
        /// </summary>
        [Fact]
        public async Task DesignAgd_3D_ProposedExistingRoundTrip_WaterSyncDoesNotInvalidateSurface
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
                FieldContentViewSwitchRegressionHarness.Session Session =
                    FieldContentViewSwitchRegressionHarness.CreateThreeDSessionWithProposedWater(Project);

                await FieldContentViewSwitchRegressionHarness.WarmBothContentViewCachesAsync(Session).ConfigureAwait(true);

                long RoundTripMs = await FieldContentViewSwitchRegressionHarness.RunWarmRoundTripAsync(Session).ConfigureAwait(true);

                Assert.Equal(0, Session.SurfaceInvalidateCount);
                Assert.True(
                    RoundTripMs <= FieldContentViewSwitchRegressionHarness.MaxWarmRoundTripMs,
                    $"Warm proposed/existing round trip too slow: {RoundTripMs} ms " +
                    $"(budget {FieldContentViewSwitchRegressionHarness.MaxWarmRoundTripMs} ms).");

                byte[]? Pixels = FieldContentViewSwitchRegressionHarness.CopyCurrentThreeDFrameBytes(Session);
                Assert.NotNull(Pixels);

                RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels!,
                    ImportHandoffHarness.DefaultViewportWidth,
                    ImportHandoffHarness.DefaultViewportHeight);
                Assert.False(
                    RenderFrameAnalyzer.IsMostlyBlack(Metrics),
                    RenderFrameAnalyzer.Describe(Metrics, "3D after warm proposed/existing round trip"));
                Assert.True(
                    RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics),
                    RenderFrameAnalyzer.Describe(Metrics, "3D after warm proposed/existing round trip"));

                return true;
            });
        }
    }
}

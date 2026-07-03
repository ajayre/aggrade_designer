using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.Integration;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Import
{
    /// <summary>
    /// Ensures alternate content-view frame caches do not cross-contaminate.
    /// </summary>
    [Collection("ImportHandoff")]
    [Trait("Category", "ImportHandoff")]
    [Trait("Category", "ImportHandoffContract")]
    public sealed class ContentViewFrameCacheIsolationTests
    {
        /// <summary>
        /// Proposed and cut/fill monolithic caches must occupy distinct slots on FakeField_01.
        /// </summary>
        [Fact]
        public async Task FakeField01_ProposedAndCutFillTopoPackets_AreDistinct
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                FieldViewerControl Viewer = ImportHandoffHarness.CreateViewer(
                    ImportHandoffProjectFactory.CreateFakeField01Project());
                Viewer.EnsureSurfaceBuiltForFit();

                Viewer.TestBuildAndCacheContentView(FieldContentView.Proposed);
                Viewer.TestBuildAndCacheContentView(FieldContentView.CutFill);

                Assert.True(
                    Viewer.TestTryGetMonolithicCacheInfo(
                        FieldContentView.Proposed,
                        out FieldContentView ProposedView,
                        out string ProposedKey));
                Assert.True(
                    Viewer.TestTryGetMonolithicCacheInfo(
                        FieldContentView.CutFill,
                        out FieldContentView CutFillView,
                        out string CutFillKey));

                Assert.Equal(FieldContentView.Proposed, ProposedView);
                Assert.Equal(FieldContentView.CutFill, CutFillView);
                Assert.NotEqual(ProposedKey, CutFillKey);

                byte[]? ProposedBytes = Viewer.TestCopyMonolithicPacketBytes(FieldContentView.Proposed);
                byte[]? CutFillBytes = Viewer.TestCopyMonolithicPacketBytes(FieldContentView.CutFill);
                Assert.NotNull(ProposedBytes);
                Assert.NotNull(CutFillBytes);
                return true;
            });
        }

        /// <summary>
        /// Concurrent surface builds must not replace proposed monolithic cache with existing cache.
        /// </summary>
        [Fact]
        public async Task FakeField01_ConcurrentExistingPrewarm_DoesNotCorruptProposedTopo
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                FieldViewerControl Viewer = ImportHandoffHarness.CreateViewer(
                    ImportHandoffProjectFactory.CreateFakeField01Project());
                Viewer.EnsureSurfaceBuiltForFit();

                Viewer.TestBuildAndCacheContentView(FieldContentView.Proposed);
                byte[]? BaselineProposed = Viewer.TestCopyMonolithicPacketBytes(FieldContentView.Proposed);
                Assert.NotNull(BaselineProposed);

                Viewer.TestScheduleBackgroundContentViewBuild(FieldContentView.Existing);
                Task BackgroundExisting = Task.Run(() =>
                    Viewer.TestRunScheduledBackgroundContentViewBuild());
                Viewer.TestBuildAndCacheContentView(FieldContentView.Proposed);
                await BackgroundExisting;

                byte[]? ProposedAfter = Viewer.TestCopyMonolithicPacketBytes(FieldContentView.Proposed);
                byte[]? ExistingBytes = Viewer.TestCopyMonolithicPacketBytes(FieldContentView.Existing);

                Assert.NotNull(ProposedAfter);
                Assert.NotNull(ExistingBytes);
                Assert.True(ProposedAfter.AsSpan().SequenceEqual(BaselineProposed));

                Assert.True(
                    Viewer.TestTryGetMonolithicCacheInfo(
                        FieldContentView.Proposed,
                        out FieldContentView ProposedView,
                        out string ProposedKey));
                Assert.True(
                    Viewer.TestTryGetMonolithicCacheInfo(
                        FieldContentView.Existing,
                        out FieldContentView ExistingView,
                        out string ExistingKey));
                Assert.Equal(FieldContentView.Proposed, ProposedView);
                Assert.Equal(FieldContentView.Existing, ExistingView);
                Assert.NotEqual(ProposedKey, ExistingKey);
                return true;
            });
        }
    }
}

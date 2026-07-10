using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Unit tests for live route scrub index reporting during slider drag.
    /// </summary>
    public sealed class HaulScrubControllerDisplayedIndexTests
    {
        /// <summary>
        /// Verifies the displayed scrub index tracks the live drag position before commit.
        /// </summary>
        [Fact]
        public void DisplayedRouteScrubIndex_WhileDragging_UsesLiveIndex
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());

            using HaulScrubController Controller = new HaulScrubController();
            Controller.Attach(Project, Data);

            int TargetIndex = Math.Min(2, Math.Max(0, Controller.RouteStepCount - 1));
            Controller.NotifySliderDrag(TargetIndex);

            Assert.Equal(0, Controller.RouteScrubIndex);
            Assert.Equal(TargetIndex, Controller.DisplayedRouteScrubIndex);
            Assert.Equal($"{TargetIndex + 1}/{Controller.RouteStepCount}", Controller.RoutePositionLabel);
        }
    }
}

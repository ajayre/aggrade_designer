using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Verifies haul scrub workers pause in 3D and resume in 2D.
    /// </summary>
    public sealed class HaulScrubThreeDViewSuspendTests
    {
        /// <summary>
        /// Suspend blocks scrub stepping; resume restores normal scrub behavior.
        /// </summary>
        [Fact]
        public void SuspendForThreeDView_BlocksScrubStepUntilResumeAfterTwoDView
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());

            using HaulScrubController Controller = new HaulScrubController();
            Controller.Attach(Project, Data);

            SpinWait.SpinUntil(
                () => Controller.Cache.IsCached(0),
                TimeSpan.FromSeconds(30));
            Assert.True(Controller.Cache.IsCached(0));

            int IndexBeforeSuspend = Controller.RouteScrubIndex;
            Controller.SuspendForThreeDView();
            Controller.StepRouteScrubIndex(1);
            Assert.Equal(IndexBeforeSuspend, Controller.RouteScrubIndex);

            Controller.ResumeAfterTwoDView();
            Controller.StepRouteScrubIndex(1);
            Assert.Equal(IndexBeforeSuspend + 1, Controller.RouteScrubIndex);
        }

        /// <summary>
        /// Resume after 2D is a no-op when 3D suspend was never active.
        /// </summary>
        [Fact]
        public void ResumeAfterTwoDView_IsIdempotentWhenNotSuspended
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());

            using HaulScrubController Controller = new HaulScrubController();
            Controller.Attach(Project, Data);

            SpinWait.SpinUntil(
                () => Controller.Cache.IsCached(0),
                TimeSpan.FromSeconds(30));

            int Index = Controller.RouteScrubIndex;
            int CachedCount = Controller.Cache.CachedFrameCount;
            Controller.ResumeAfterTwoDView();
            Assert.Equal(Index, Controller.RouteScrubIndex);
            Assert.Equal(CachedCount, Controller.Cache.CachedFrameCount);
        }
    }
}

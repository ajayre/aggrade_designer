using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Haul
{
    public sealed class HaulScrubViewSwitchTests
    {
        [Fact]
        public void SuspendAndResume_PreservesCachedFramesAndScrubIndex
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());
            HaulBuilder.AgdField Field = BuildAgdField(Project);

            using HaulScrubController Controller = new HaulScrubController();
            Controller.Attach(Project, Data);

            SpinWait.SpinUntil(
                () => Controller.Cache.IsCached(0),
                TimeSpan.FromSeconds(30));
            Assert.True(Controller.Cache.IsCached(0));

            int CachedBeforeSuspend = Controller.Cache.CachedFrameCount;
            Controller.StepRouteScrubIndex(2);
            int ExpectedIndex = Controller.RouteScrubIndex;

            Controller.SuspendForFieldViewSwitch();
            Assert.True(Controller.IsAttachedTo(Data));
            Assert.Equal(CachedBeforeSuspend, Controller.Cache.CachedFrameCount);

            Controller.ResumeAfterFieldViewSwitch();
            Assert.Equal(ExpectedIndex, Controller.RouteScrubIndex);
            Assert.Equal(CachedBeforeSuspend, Controller.Cache.CachedFrameCount);
        }

        private static HaulBuilder.AgdField BuildAgdField
            (
            ProjectDocument Project
            )
        {
            string TempAgd = Path.Combine(Path.GetTempPath(), $"aggrade-haul-switch-{Guid.NewGuid():N}.agd");
            try
            {
                AgdService.Export(
                    TempAgd,
                    Project.Survey!,
                    Project.DesignGrid!,
                    Project.DesignParameters ?? new DesignParameters());
                return HaulBuilder.Agd.Parse(TempAgd);
            }
            finally
            {
                if (File.Exists(TempAgd))
                    File.Delete(TempAgd);
            }
        }
    }
}

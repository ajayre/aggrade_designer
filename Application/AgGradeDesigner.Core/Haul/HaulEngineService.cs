using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Runs the vendored <see cref="HaulBuilder.HaulBuilderEngine"/> against an open project (§6).
    /// </summary>
    public static class HaulEngineService
    {
        /// <summary>
        /// Generates haul data for the supplied project using user haul settings.
        /// </summary>
        /// <param name="Project">Project with survey and design.</param>
        /// <param name="Settings">User haul defaults.</param>
        /// <param name="GuidanceProgress">Optional engine progress reporter.</param>
        /// <param name="CancellationToken">Cancellation token observed during the engine run.</param>
        /// <returns>Structured haul data from a successful engine run.</returns>
        /// <exception cref="InvalidOperationException">Project lacks survey or design.</exception>
        public static HaulData Generate
            (
            ProjectDocument Project,
            HaulSettings Settings,
            IProgress<HaulBuilder.GuidanceBuildProgress>? GuidanceProgress = null,
            CancellationToken CancellationToken = default
            )
        {
            if (!Project.HasSurvey || !Project.HasDesign || Project.Survey == null || Project.DesignGrid == null)
                throw new InvalidOperationException("Survey and design are required to generate haul data.");

            string TempAgd = Path.Combine(Path.GetTempPath(), $"aggrade-haul-{Guid.NewGuid():N}.agd");
            try
            {
                AgdService.Export(
                    TempAgd,
                    Project.Survey,
                    Project.DesignGrid,
                    Project.DesignParameters ?? new DesignParameters());

                HaulBuilder.HaulBuilderOptions Options = HaulOptionsBuilder.Build(Project, Settings, TempAgd);
                HaulBuilder.HaulBuilderResult Result = new HaulBuilder.HaulBuilderEngine().Run(
                    Options,
                    GuidanceProgress,
                    CancellationToken);
                HaulGuidanceBuilder.EnsureOnResult(Result, Options);
                return HaulData.FromResult(Result);
            }
            finally
            {
                if (File.Exists(TempAgd))
                    File.Delete(TempAgd);
            }
        }
    }
}

using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Verifies haul 3D scalar-channel health analysis used by diagnostics logging.
    /// </summary>
    [Trait("Category", "HaulRegression")]
    public sealed class HaulThreeDScalarChannelHealthTests
    {
        /// <summary>
        /// Empty samplers must report zero valid samples.
        /// </summary>
        [Fact]
        public void Analyze_EmptySampler_ReportsZeroValidSamples
            (
            )
        {
            FieldSurfaceSampler Sampler = new FieldSurfaceSampler();
            HaulThreeDScalarChannelHealth Health = HaulThreeDScalarChannelHealthAnalyzer.Analyze(Sampler);

            Assert.False(Health.HaulLeveledIsBuilt);
            Assert.Equal(0, Health.ValidSampleCount);
            Assert.Equal(0, Health.FiniteHaulSampleCount);
        }

        /// <summary>
        /// Built haul channels must flag samples that still match survey Existing.
        /// </summary>
        [Fact]
        public void Analyze_BuiltChannel_CountsExistingMatches
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateFakeField01Project();
            FieldSurfaceSampler Sampler = new FieldSurfaceSampler();
            Sampler.BuildFromProject(Project);
            Assert.True(Sampler.SampleWidth > 0);

            double[] HaulLeveled = (double[])Sampler.Existing.Clone();
            int Modified = 0;
            for (int I = 0; I < HaulLeveled.Length && Modified < 3; I++)
            {
                if (!Sampler.Valid[I])
                    continue;

                HaulLeveled[I] += 2.5;
                Modified++;
            }

            Sampler.SetHaulLeveledChannel(HaulLeveled);
            HaulThreeDScalarChannelHealth Health = HaulThreeDScalarChannelHealthAnalyzer.Analyze(Sampler);

            Assert.True(Health.HaulLeveledIsBuilt);
            Assert.True(Health.FiniteHaulSampleCount > 0);
            Assert.True(Health.MatchesExistingCount > 0);
            Assert.True(Health.DiffersFromExistingCount > 0);
        }
    }
}

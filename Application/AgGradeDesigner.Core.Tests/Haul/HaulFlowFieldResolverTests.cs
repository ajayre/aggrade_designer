using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Unit tests for <see cref="HaulFlowFieldResolver"/>.
    /// </summary>
    public sealed class HaulFlowFieldResolverTests
    {
        /// <summary>Minimum arrow-mask cells expected on The Shop 2 haul flow field.</summary>
        private const int MIN_VISIBLE_ARROW_CELLS = 1;

        /// <summary>
        /// Verifies null and invalid haul payloads are rejected for flow-arrow enablement.
        /// </summary>
        [Fact]
        public void HasFlowArrowData_ReturnsFalse_WhenHaulMissingOrInvalid
            (
            )
        {
            Assert.False(HaulFlowFieldResolver.HasFlowArrowData(null));
            Assert.False(HaulFlowFieldResolver.HasFlowArrowData(new HaulData()));
            Assert.False(HaulFlowFieldResolver.HasFlowArrowData(new HaulData
            {
                Payload = new HaulPersistedPayload()
            }));
        }

        /// <summary>
        /// Verifies a live haul engine result exposes flow-arrow data.
        /// </summary>
        [Fact]
        public void HasFlowArrowData_ReturnsTrue_ForGeneratedHaulResult
            (
            )
        {
            HaulFlowFieldResolver.ClearCache();
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());

            Assert.True(HaulFlowFieldResolver.HasFlowArrowData(Data));
        }

        /// <summary>
        /// Verifies <see cref="HaulFlowFieldResolver.TryResolve"/> returns flow arrows for a live haul session.
        /// </summary>
        [Fact]
        public void TryResolve_LiveHaulResult_ExposesVisibleArrows
            (
            )
        {
            HaulFlowFieldResolver.ClearCache();
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();
            HaulData Data = HaulEngineService.Generate(Project, Settings);

            HaulFlowFieldResolver.ResolvedHaulFlow? Resolved = HaulFlowFieldResolver.TryResolve(Data, Project, Settings);

            Assert.NotNull(Resolved);
            Assert.True(Resolved.HasVisibleArrows);
            Assert.True(CountVisibleArrowCells(Resolved) >= MIN_VISIBLE_ARROW_CELLS);
            Assert.Equal(HaulFlowFieldResolver.DEFAULT_ARROW_STRIDE, Resolved.ArrowStride);
        }

        /// <summary>
        /// Verifies persisted haul payloads without a live result can rebuild flow arrows lazily.
        /// </summary>
        [Fact]
        public void TryResolve_PersistedHaulWithoutLiveResult_RebuildsVisibleArrows
            (
            )
        {
            HaulFlowFieldResolver.ClearCache();
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();
            HaulData Live = HaulEngineService.Generate(Project, Settings);

            HaulData Persisted = new HaulData { Payload = Live.Payload };

            Assert.True(HaulFlowFieldResolver.HasFlowArrowData(Persisted));
            HaulFlowFieldResolver.ResolvedHaulFlow? Resolved = HaulFlowFieldResolver.TryResolve(Persisted, Project, Settings);

            Assert.NotNull(Resolved);
            Assert.True(Resolved.HasVisibleArrows);
            Assert.True(CountVisibleArrowCells(Resolved) >= MIN_VISIBLE_ARROW_CELLS);
        }

        /// <summary>
        /// Verifies flow resolution is cached per <see cref="HaulData"/> instance.
        /// </summary>
        [Fact]
        public void TryResolve_ReusesCache_ForSameHaulDataInstance
            (
            )
        {
            HaulFlowFieldResolver.ClearCache();
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();
            HaulData Data = HaulEngineService.Generate(Project, Settings);

            HaulFlowFieldResolver.ResolvedHaulFlow? First = HaulFlowFieldResolver.TryResolve(Data, Project, Settings);
            HaulFlowFieldResolver.ResolvedHaulFlow? Second = HaulFlowFieldResolver.TryResolve(Data, Project, Settings);

            Assert.NotNull(First);
            Assert.Same(First, Second);
        }

        /// <summary>
        /// Verifies <see cref="HaulFlowFieldResolver.ClearCache"/> forces a fresh resolve.
        /// </summary>
        [Fact]
        public void ClearCache_ForcesFreshResolve
            (
            )
        {
            HaulFlowFieldResolver.ClearCache();
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();
            HaulData Data = HaulEngineService.Generate(Project, Settings);

            HaulFlowFieldResolver.ResolvedHaulFlow? First = HaulFlowFieldResolver.TryResolve(Data, Project, Settings);
            HaulFlowFieldResolver.ClearCache();
            HaulFlowFieldResolver.ResolvedHaulFlow? Second = HaulFlowFieldResolver.TryResolve(Data, Project, Settings);

            Assert.NotNull(First);
            Assert.NotNull(Second);
            Assert.NotSame(First, Second);
            Assert.Same(First.Flow, Second.Flow);
        }

        /// <summary>
        /// Counts arrow-mask cells in the resolved flow field.
        /// </summary>
        private static int CountVisibleArrowCells
            (
            HaulFlowFieldResolver.ResolvedHaulFlow Resolved
            )
        {
            int Count = 0;
            for (int By = 0; By < Resolved.Grid.Height; By++)
            {
                for (int Bx = 0; Bx < Resolved.Grid.Width; Bx++)
                {
                    if (!Resolved.Grid.Valid[By, Bx])
                        continue;

                    if (Resolved.Flow.HasArrow[By, Bx])
                        Count++;
                }
            }

            return Count;
        }
    }
}

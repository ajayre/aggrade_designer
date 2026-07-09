using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.TestFixtures;
using AgGradeDesigner.Core.Units;
using AgGradeDesigner.Core.Water;

namespace AgGradeDesigner.Core.Tests.Water
{
    /// <summary>
    /// Unit tests for Water feature persistence and settings (WATER-SPEC §9).
    /// </summary>
    public sealed class WaterIntegrationTests
    {
        /// <summary>
        /// Verifies default water settings when the Water element is absent on open.
        /// </summary>
        [Fact]
        public void Open_AppliesDefaultWaterSettingsWhenElementMissing
            (
            )
        {
            string ProjectPath = TempFile.Path(".agproject");
            try
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
                AgDesignSerializer.Save(Project, ProjectPath);

                string Xml = AgProjectArchive.Read(ProjectPath);
                Xml = Xml.Replace("<Water", "<RemovedWater", StringComparison.Ordinal);
                AgProjectArchive.Write(ProjectPath, Xml);

                ProjectDocument Reloaded = AgDesignSerializer.Load(ProjectPath);
                Assert.Equal(WaterConstants.DEFAULT_PONDING_RAINFALL_MM, Reloaded.Water.Settings.PondingRainfallMm);
                Assert.Equal(WaterConstants.DEFAULT_PONDING_CURVE_NUMBER, Reloaded.Water.Settings.PondingCurveNumber);
            }
            finally
            {
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Verifies water settings and cache payloads round-trip through save and open.
        /// </summary>
        [Fact]
        public void SaveOpen_RoundTripsWaterSettingsAndCachePayloads
            (
            )
        {
            string ProjectPath = TempFile.Path(".agproject");
            try
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
                Project.Water.Settings.PondingRainfallMm = 75;
                Project.Water.Settings.PondingCurveNumber = 90;
                Project.Water.ExistingSurfaceCache = CreateSampleCache(WaterBasisSurfaceKind.Existing, 75, 90);
                Project.Water.ProposedSurfaceCache = CreateSampleCache(WaterBasisSurfaceKind.Proposed, 75, 90);

                AgDesignSerializer.Save(Project, ProjectPath);
                ProjectDocument Reloaded = AgDesignSerializer.Load(ProjectPath);

                Assert.Equal(75u, Reloaded.Water.Settings.PondingRainfallMm);
                Assert.Equal(90u, Reloaded.Water.Settings.PondingCurveNumber);
                Assert.NotNull(Reloaded.Water.ExistingSurfaceCache);
                Assert.NotNull(Reloaded.Water.ProposedSurfaceCache);
                Assert.True(Reloaded.Water.ExistingSurfaceCache!.IsValidForDisplay());
                Assert.True(Reloaded.Water.ProposedSurfaceCache!.IsValidForDisplay());
                Assert.True(WaterLayerCacheGeography.HasValidGeographicBounds(Reloaded.Water.ExistingSurfaceCache!));
                Assert.True(WaterLayerCacheGeography.HasValidGeographicBounds(Reloaded.Water.ProposedSurfaceCache!));
            }
            finally
            {
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Verifies imperial and metric rainfall display conversion defaults.
        /// </summary>
        [Fact]
        public void DisplayUnits_ConvertRainfallBetweenMetricAndImperial
            (
            )
        {
            Assert.Equal(50u, WaterDisplayUnitConverter.ToDisplayRainfall(50, DisplayUnitSystem.Metric));
            Assert.Equal(2u, WaterDisplayUnitConverter.ToDisplayRainfall(50, DisplayUnitSystem.Imperial));
            Assert.Equal(50u, WaterDisplayUnitConverter.FromDisplayRainfall(2, DisplayUnitSystem.Imperial));
        }

        /// <summary>
        /// Verifies BASE64 payload encoding is lossless.
        /// </summary>
        [Fact]
        public void BlobCodec_Base64RoundTripIsLossless
            (
            )
        {
            WaterLayerCacheEntry Entry = CreateSampleCache(WaterBasisSurfaceKind.Existing, 50, 85);
            byte[] Blob = WaterLayerBlobCodec.Encode(Entry);
            string Base64 = WaterLayerBlobCodec.EncodeBase64(Blob);
            Assert.True(WaterLayerBlobCodec.TryDecodeBase64(Base64, out byte[] Decoded));
            Assert.Equal(Blob, Decoded);
        }

        /// <summary>
        /// Verifies toggle enablement follows cache presence for the active view.
        /// </summary>
        [Fact]
        public void Resolver_HasWaterLayerToShowWhenCacheExists
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            Assert.False(WaterLayerResolver.HasWaterLayerToShow(Project, FieldContentView.Existing, 0));

            Project.Water.ExistingSurfaceCache = CreateSampleCache(WaterBasisSurfaceKind.Existing, 50, 85);
            Assert.True(WaterLayerResolver.HasWaterLayerToShow(Project, FieldContentView.Existing, 0));
            Assert.True(WaterLayerResolver.HasWaterLayerToShow(Project, FieldContentView.CutFill, 0));
            Assert.False(WaterLayerResolver.HasWaterLayerToShow(Project, FieldContentView.Proposed, 0));
        }

        /// <summary>
        /// Verifies basis surface mapping for proposed and haul views.
        /// </summary>
        [Fact]
        public void Resolver_MapsBasisSurfacesPerFieldContentView
            (
            )
        {
            Assert.Equal(WaterBasisSurfaceKind.Existing, WaterLayerResolver.ResolveBasisSurfaceKind(FieldContentView.Existing));
            Assert.Equal(WaterBasisSurfaceKind.Existing, WaterLayerResolver.ResolveBasisSurfaceKind(FieldContentView.CutFill));
            Assert.Equal(WaterBasisSurfaceKind.Proposed, WaterLayerResolver.ResolveBasisSurfaceKind(FieldContentView.Proposed));
            Assert.Equal(WaterBasisSurfaceKind.HaulCutFill, WaterLayerResolver.ResolveBasisSurfaceKind(FieldContentView.Haul));
        }

        /// <summary>
        /// Verifies haul cut/fill water caches require the Haul content view and matching route scrub index.
        /// </summary>
        [Fact]
        public void Resolver_HaulCutFillCacheRequiresHaulContentViewAndScrubIndex
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            WaterLayerCacheEntry HaulCache = CreateSampleCache(WaterBasisSurfaceKind.HaulCutFill, 50, 85);
            WaterSessionCache.StoreHaulCache(1791, HaulCache);

            Assert.Null(WaterLayerResolver.TryResolveCacheEntry(Project, FieldContentView.Existing, 1791));
            Assert.NotNull(WaterLayerResolver.TryResolveCacheEntry(Project, FieldContentView.Haul, 1791));
            Assert.Null(WaterLayerResolver.TryResolveCacheEntry(Project, FieldContentView.Haul, 0));
        }

        /// <summary>
        /// Verifies invalidation helpers discard the expected cache entries.
        /// </summary>
        [Fact]
        public void Invalidation_DiscardsPersistedAndSessionCaches
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            Project.Water.ExistingSurfaceCache = CreateSampleCache(WaterBasisSurfaceKind.Existing, 50, 85);
            Project.Water.ProposedSurfaceCache = CreateSampleCache(WaterBasisSurfaceKind.Proposed, 50, 85);
            WaterSessionCache.StoreHaulCache(0, CreateSampleCache(WaterBasisSurfaceKind.HaulCutFill, 50, 85));

            WaterLayerInvalidation.DiscardExistingSurfaceCache(Project);
            Assert.Null(Project.Water.ExistingSurfaceCache);

            WaterLayerInvalidation.DiscardProposedSurfaceCache(Project);
            Assert.Null(Project.Water.ProposedSurfaceCache);

            WaterLayerInvalidation.DiscardHaulCaches(Project);
            Assert.Null(WaterSessionCache.TryGetHaulCache(0));
        }

        /// <summary>
        /// Verifies unchanged settings do not mark an existing cache stale.
        /// </summary>
        [Fact]
        public void Resolver_CacheIsFreshWhenSettingsMatch
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            Project.Water.ExistingSurfaceCache = CreateSampleCache(WaterBasisSurfaceKind.Existing, 50, 85);
            Assert.False(WaterLayerResolver.IsCacheMissingOrStale(Project, FieldContentView.Existing, 0));
        }

        /// <summary>
        /// Verifies surface water layer calculation reports progress from a worker thread.
        /// </summary>
        [Fact]
        public async Task Calculator_ComputeAsyncRunsOffCallingThread
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            int CallingThreadId = Environment.CurrentManagedThreadId;
            bool SawWorkerThread = false;
            IProgress<int> Progress = new Progress<int>(_ =>
            {
                if (Environment.CurrentManagedThreadId != CallingThreadId)
                    SawWorkerThread = true;
            });

            await WaterLayerCalculator.ComputeAsync(
                Project,
                FieldContentView.Existing,
                RouteScrubIndex: 0,
                Progress).ConfigureAwait(true);

            Assert.NotNull(Project.Water.ExistingSurfaceCache);
            Assert.True(SawWorkerThread, "Expected ponding work to run off the calling thread.");
        }

        /// <summary>
        /// Builds a small valid cache entry for persistence tests.
        /// </summary>
        /// <param name="BasisSurface">Basis surface kind.</param>
        /// <param name="RainfallMm">Rainfall used for the cache.</param>
        /// <param name="CurveNumber">Curve number used for the cache.</param>
        /// <returns>Valid cache entry.</returns>
        private static WaterLayerCacheEntry CreateSampleCache
            (
            WaterBasisSurfaceKind BasisSurface,
            uint RainfallMm,
            uint CurveNumber
            )
        {
            float[] Depths = [0f, 0.5f, 0f, 0f];
            WaterLayerCacheEntry Entry = new WaterLayerCacheEntry
            {
                BasisSurface = BasisSurface,
                RouteScrubIndex = BasisSurface == WaterBasisSurfaceKind.HaulCutFill ? 0 : null,
                PondingRainfallMm = RainfallMm,
                PondingCurveNumber = CurveNumber,
                GridWidth = 2,
                GridHeight = 2,
                CellSizeM = FieldConstants.BIN_SIZE_M,
                MinLat = 40.0,
                MaxLat = 40.01,
                MinLon = -88.0,
                MaxLon = -87.99,
                PondDepthM = Depths,
                OverlayRgba = PondingAnalysisEngine.BuildOverlayRgba(Depths, 2, 2),
            };
            Entry.PayloadBlob = WaterLayerBlobCodec.Encode(Entry);
            return Entry;
        }
    }
}

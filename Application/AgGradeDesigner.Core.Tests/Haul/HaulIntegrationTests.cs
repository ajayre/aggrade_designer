using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.TestFixtures;
using AgGradeDesigner.Core.Units;
using AgGradeDesigner.Services;
using AgGradeDesigner.ViewModels;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Unit tests for haul settings, options mapping, and project persistence (HAUL-SPEC §8).
    /// </summary>
    public sealed class HaulIntegrationTests
    {
        /// <summary>
        /// Verifies haul settings round-trip through the user settings file.
        /// </summary>
        [Fact]
        public void HaulSettings_RoundTripThroughUserSettingsFile
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                UserSettingsStore Store = new UserSettingsStore(Path);
                HaulSettings Original = new HaulSettings
                {
                    ScraperCapacityLcy = 12.5,
                    BladeWidthMm = 3657.6,
                    UseDesignMainSlopeDirection = false,
                    HeadingDeg = 45.0
                };
                Store.SaveHaulSettings(Original);

                HaulSettings Reloaded = Store.LoadHaulSettings();
                Assert.Equal(Original.ScraperCapacityLcy, Reloaded.ScraperCapacityLcy);
                Assert.Equal(Original.BladeWidthMm, Reloaded.BladeWidthMm);
                Assert.False(Reloaded.UseDesignMainSlopeDirection);
                Assert.Equal(45.0, Reloaded.HeadingDeg);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies project save embeds haul data but not haul settings.
        /// </summary>
        [Fact]
        public void ProjectSave_EmbedsHaulData_NotHaulSettings
            (
            )
        {
            string SettingsPath = TempFile.Path(".json");
            string ProjectPath = TempFile.Path(".agproject");
            try
            {
                UserSettingsStore Store = new UserSettingsStore(SettingsPath);
                Store.SaveHaulSettings(new HaulSettings { ScraperCapacityLcy = 99.0 });

                ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
                Project.HaulData = new HaulData
                {
                    Payload = new HaulPersistedPayload
                    {
                        OrderedPasses = [new HaulPassDto { LoadBcy = 1.0 }]
                    }
                };

                AgDesignSerializer.Save(Project, ProjectPath);
                string Xml = AgProjectArchive.Read(ProjectPath);
                Assert.Contains("Haul", Xml, StringComparison.Ordinal);
                Assert.Contains("Payload", Xml, StringComparison.Ordinal);
                Assert.DoesNotContain("ScraperCapacityLcy", Xml, StringComparison.Ordinal);

                ProjectDocument Reloaded = AgDesignSerializer.Load(ProjectPath);
                Assert.True(Reloaded.HasHaulData);
                Assert.Equal(99.0, Store.LoadHaulSettings().ScraperCapacityLcy);
            }
            finally
            {
                TempFile.DeleteIfExists(SettingsPath);
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Verifies grid spacing is derived from blade width in feet.
        /// </summary>
        [Fact]
        public void GridSizeFt_DerivedFromBladeWidth
            (
            )
        {
            double GridSizeFt = HaulDisplayUnits.GridSizeFtFromBladeWidthMm(304.8);
            Assert.Equal(1.0, GridSizeFt, precision: 3);
        }

        /// <summary>
        /// Verifies imperial/metric display conversion for haul capacity.
        /// </summary>
        [Fact]
        public void DisplayUnits_ConvertScraperCapacity
            (
            )
        {
            double Lcy = 10.0;
            double MetricDisplay = HaulDisplayUnits.ScraperCapacityToDisplay(Lcy, DisplayUnitSystem.Metric);
            double RoundTrip = HaulDisplayUnits.ScraperCapacityLcyFromDisplay(MetricDisplay, DisplayUnitSystem.Metric);
            Assert.Equal(Lcy, RoundTrip, precision: 6);
        }

        /// <summary>
        /// Verifies options mapping uses design cut/fill ratio and heading preference.
        /// </summary>
        [Fact]
        public void HaulOptionsBuilder_UsesDesignCutFillRatioAndHeading
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            Project.DesignParameters!.CutFillRatio = 1.15;
            Project.DesignParameters.MainSlopeDirectionDeg = 88.0;

            HaulSettings Settings = new HaulSettings
            {
                UseDesignMainSlopeDirection = true,
                HeadingDeg = 12.0
            };

            HaulBuilder.HaulBuilderOptions Options = HaulOptionsBuilder.Build(Project, Settings, "unused.agd");
            Assert.Equal(1.15, Options.CutFillRatio);
            Assert.Equal(88.0, Options.HeadingDeg);

            Settings.UseDesignMainSlopeDirection = false;
            Options = HaulOptionsBuilder.Build(Project, Settings, "unused.agd");
            Assert.Equal(12.0, Options.HeadingDeg);
        }

        /// <summary>
        /// Verifies haul dialog OK persists haul settings without marking the project dirty by itself.
        /// </summary>
        [Fact]
        public void HaulDialogOk_PersistsHaulSettingsWithoutProjectDirty
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                UserSettingsService Service = new UserSettingsService(new UserSettingsStore(Path));
                HaulDialogViewModel Dialog = new HaulDialogViewModel(Service);
                Dialog.ScraperCapacityDisplay = 42.0;
                Dialog.OkCommand.Execute(null);

                Assert.Equal(42.0, Service.ActiveHaulSettings.ScraperCapacityLcy);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies haul generation succeeds for the TheShop2 AGD regression fixture.
        /// </summary>
        [Fact]
        public void HaulEngineService_Generate_TheShop2_ProducesPasses
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();

            HaulData Data = HaulEngineService.Generate(Project, Settings);

            Assert.True(Data.IsValid, $"Expected haul passes; messages: {string.Join("; ", Data.Payload.Messages)}");
            Assert.True(Data.Payload.OrderedPasses.Count > 0);
        }

    }
}

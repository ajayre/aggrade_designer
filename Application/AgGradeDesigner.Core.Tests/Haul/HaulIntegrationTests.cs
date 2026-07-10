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
        /// Verifies default haul settings when the Haul element is absent on open.
        /// </summary>
        [Fact]
        public void Open_AppliesDefaultHaulSettingsWhenElementMissing
            (
            )
        {
            string ProjectPath = TempFile.Path(".agproject");
            try
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
                AgDesignSerializer.Save(Project, ProjectPath);

                string Xml = AgProjectArchive.Read(ProjectPath);
                Xml = Xml.Replace("<Haul", "<RemovedHaul", StringComparison.Ordinal);
                AgProjectArchive.Write(ProjectPath, Xml);

                ProjectDocument Reloaded = AgDesignSerializer.Load(ProjectPath);
                HaulSettings Defaults = new HaulSettings();
                Assert.Equal(Defaults.ScraperCapacityLcy, Reloaded.HaulSettings.ScraperCapacityLcy);
                Assert.Equal(Defaults.BladeWidthMm, Reloaded.HaulSettings.BladeWidthMm);
                Assert.Equal(Defaults.UseDesignMainSlopeDirection, Reloaded.HaulSettings.UseDesignMainSlopeDirection);
            }
            finally
            {
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Verifies project save embeds haul data, haul settings, and flow-field cache.
        /// </summary>
        [Fact]
        public void ProjectSave_EmbedsHaulDataAndHaulSettings
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
                Project.HaulSettings.ScraperCapacityLcy = 42.0;
                Project.HaulSettings.HeadingDeg = 33.0;
                Project.HaulData = new HaulData
                {
                    Payload = new HaulPersistedPayload
                    {
                        OrderedPasses = [new HaulPassDto { LoadBcy = 1.0 }],
                        GuidancePoints = [new HaulGuidancePointDto { Seq = 1, PassNo = 1 }],
                        FlowField = new HaulFlowFieldDto
                        {
                            Width = 2,
                            Height = 2,
                            GridSizeM = 1.0,
                            Valid = [true, true, true, true],
                            Fx = [1.0, 0.0, 0.0, 1.0],
                            Fy = [0.0, 1.0, 1.0, 0.0],
                            Magnitude = [1.0, 1.0, 1.0, 1.0],
                            HasArrow = [true, false, false, true]
                        },
                        ArrowStride = 3
                    }
                };

                AgDesignSerializer.Save(Project, ProjectPath);
                string Xml = AgProjectArchive.Read(ProjectPath);
                Assert.Contains("Haul", Xml, StringComparison.Ordinal);
                Assert.Contains("Payload", Xml, StringComparison.Ordinal);
                Assert.Contains("scraperCapacityLcy=\"42", Xml, StringComparison.Ordinal);
                Assert.Contains("headingDeg=\"33", Xml, StringComparison.Ordinal);
                Assert.Contains("\"flowField\"", Xml, StringComparison.Ordinal);
                Assert.Contains("\"arrowStride\":3", Xml, StringComparison.Ordinal);

                ProjectDocument Reloaded = AgDesignSerializer.Load(ProjectPath);
                Assert.True(Reloaded.HasHaulData);
                Assert.Equal(42.0, Reloaded.HaulSettings.ScraperCapacityLcy);
                Assert.Equal(33.0, Reloaded.HaulSettings.HeadingDeg);
                Assert.Single(Reloaded.HaulData!.Payload.GuidancePoints);
                Assert.NotNull(Reloaded.HaulData.Payload.FlowField);
                Assert.Equal(3, Reloaded.HaulData.Payload.ArrowStride);
                Assert.True(HaulFlowFieldCodec.HasVisibleArrows(Reloaded.HaulData.Payload.FlowField));
                Assert.Equal(99.0, Store.LoadHaulSettings().ScraperCapacityLcy);
            }
            finally
            {
                TempFile.DeleteIfExists(SettingsPath);
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Verifies pivot bearings and move route numbers round-trip through project save/open.
        /// </summary>
        [Fact]
        public void ProjectSave_RoundTripsPivotOverlayAnglesAndMoves
            (
            )
        {
            string ProjectPath = TempFile.Path(".agproject");
            try
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
                Project.HaulData = new HaulData
                {
                    Payload = new HaulPersistedPayload
                    {
                        OrderedPasses =
                        [
                            new HaulPassDto { LoadBcy = 1.0 },
                            new HaulPassDto { LoadBcy = 2.0 },
                            new HaulPassDto { LoadBcy = 3.0 }
                        ],
                        PivotOverlay = new HaulPivotOverlayDto
                        {
                            RadiusM = 125.5,
                            BearingByPassDeg = [10.0, 10.0, 95.0],
                            Moves =
                            [
                                new HaulPivotMoveDto
                                {
                                    Index = 1,
                                    FirstRoute = 1,
                                    LastRoute = 2,
                                    BearingDeg = 10.0,
                                    MoveDeg = 0.0
                                },
                                new HaulPivotMoveDto
                                {
                                    Index = 2,
                                    FirstRoute = 3,
                                    LastRoute = 3,
                                    BearingDeg = 95.0,
                                    MoveDeg = 85.0
                                }
                            ]
                        }
                    }
                };

                AgDesignSerializer.Save(Project, ProjectPath);
                string Xml = AgProjectArchive.Read(ProjectPath);
                Assert.Contains("\"pivotOverlay\"", Xml, StringComparison.Ordinal);
                Assert.Contains("\"bearingByPassDeg\"", Xml, StringComparison.Ordinal);
                Assert.Contains("\"firstRoute\":3", Xml, StringComparison.Ordinal);
                Assert.Contains("\"moveDeg\":85", Xml, StringComparison.Ordinal);

                ProjectDocument Reloaded = AgDesignSerializer.Load(ProjectPath);
                HaulPivotOverlayDto? Overlay = Reloaded.HaulData?.ResolvePivotOverlay();
                Assert.NotNull(Overlay);
                Assert.Equal(125.5, Overlay.RadiusM, precision: 6);
                Assert.Equal(new[] { 10.0, 10.0, 95.0 }, Overlay.BearingByPassDeg);
                Assert.Equal(2, Overlay.Moves.Count);
                Assert.Equal(1, Overlay.Moves[0].FirstRoute);
                Assert.Equal(2, Overlay.Moves[0].LastRoute);
                Assert.Equal(10.0, Overlay.Moves[0].BearingDeg, precision: 6);
                Assert.Equal(3, Overlay.Moves[1].FirstRoute);
                Assert.Equal(95.0, Overlay.Moves[1].BearingDeg, precision: 6);
                Assert.Equal(85.0, Overlay.Moves[1].MoveDeg, precision: 6);

                Assert.True(HaulPivotOverlay.TryResolveBearingDeg(Overlay, 2, out double BearingDeg));
                Assert.Equal(95.0, BearingDeg, precision: 6);
            }
            finally
            {
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Verifies haul settings round-trip through save and open.
        /// </summary>
        [Fact]
        public void SaveOpen_RoundTripsHaulSettings
            (
            )
        {
            string ProjectPath = TempFile.Path(".agproject");
            try
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
                Project.HaulSettings.ScraperCapacityLcy = 12.5;
                Project.HaulSettings.BladeWidthMm = 3657.6;
                Project.HaulSettings.UseDesignMainSlopeDirection = false;
                Project.HaulSettings.HeadingDeg = 45.0;
                Project.HaulSettings.TransportMode = "mincostflow";
                Project.HaulSettings.Pivot = true;
                Project.HaulSettings.PivotLatitudeDeg = 40.5;

                AgDesignSerializer.Save(Project, ProjectPath);
                ProjectDocument Reloaded = AgDesignSerializer.Load(ProjectPath);

                Assert.Equal(12.5, Reloaded.HaulSettings.ScraperCapacityLcy);
                Assert.Equal(3657.6, Reloaded.HaulSettings.BladeWidthMm);
                Assert.False(Reloaded.HaulSettings.UseDesignMainSlopeDirection);
                Assert.Equal(45.0, Reloaded.HaulSettings.HeadingDeg);
                Assert.Equal("mincostflow", Reloaded.HaulSettings.TransportMode);
                Assert.True(Reloaded.HaulSettings.Pivot);
                Assert.Equal(40.5, Reloaded.HaulSettings.PivotLatitudeDeg);
            }
            finally
            {
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
        /// Verifies haul dialog OK returns accepted settings for project persistence.
        /// </summary>
        [Fact]
        public void HaulDialogOk_ReturnsAcceptedHaulSettings
            (
            )
        {
            HaulSettings ProjectSettings = new HaulSettings { ScraperCapacityLcy = 10.0 };
            HaulDialogViewModel Dialog = new HaulDialogViewModel(ProjectSettings, DisplayUnitSystem.Imperial);
            Dialog.ScraperCapacityDisplay = 42.0;
            Dialog.OkCommand.Execute(null);

            Assert.True(Dialog.Confirmed);
            Assert.False(Dialog.RunHaulAfterClose);
            Assert.Equal(42.0, Dialog.ToHaulSettings().ScraperCapacityLcy);
        }

        /// <summary>
        /// Verifies haul dialog OK &amp; Run requests haul generation after close.
        /// </summary>
        [Fact]
        public void HaulDialogOkAndRun_RequestsHaulGeneration
            (
            )
        {
            HaulDialogViewModel Dialog = new HaulDialogViewModel(new HaulSettings(), DisplayUnitSystem.Imperial);
            Dialog.OkAndRunCommand.Execute(null);

            Assert.True(Dialog.Confirmed);
            Assert.True(Dialog.RunHaulAfterClose);
        }

        /// <summary>
        /// Verifies OK &amp; Run refuses when pivot mode is on and the centre is outside the field.
        /// </summary>
        [Fact]
        public void HaulDialogOkAndRun_RefusesPivotCenterOutsideField
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings
            {
                Pivot = true,
                PivotLatitudeDeg = 0.0,
                PivotLongitudeDeg = 0.0
            };
            HaulDialogViewModel Dialog = new HaulDialogViewModel(Settings, DisplayUnitSystem.Imperial, Project);

            Dialog.OkAndRunCommand.Execute(null);

            Assert.False(Dialog.Confirmed);
            Assert.False(Dialog.RunHaulAfterClose);
        }

        /// <summary>
        /// Verifies OK still saves settings when pivot centre is outside the field.
        /// </summary>
        [Fact]
        public void HaulDialogOk_AllowsPivotCenterOutsideFieldWithoutRunning
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings
            {
                Pivot = true,
                PivotLatitudeDeg = 0.0,
                PivotLongitudeDeg = 0.0
            };
            HaulDialogViewModel Dialog = new HaulDialogViewModel(Settings, DisplayUnitSystem.Imperial, Project);

            Dialog.OkCommand.Execute(null);

            Assert.True(Dialog.Confirmed);
            Assert.False(Dialog.RunHaulAfterClose);
        }

        /// <summary>
        /// Verifies generated haul data persists passes, guidance, and flow-field cache.
        /// </summary>
        [Fact]
        public void FromResult_PersistsHaulPlanAndFlowField
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();

            HaulData Data = HaulEngineService.Generate(Project, Settings);

            Assert.True(Data.IsValid);
            Assert.True(Data.Payload.OrderedPasses.Count > 0);
            Assert.True(Data.Payload.GuidancePoints.Count > 0);
            Assert.NotNull(Data.Payload.FlowField);
            Assert.True(HaulFlowFieldCodec.IsStructurallyValid(Data.Payload.FlowField));
            Assert.True(HaulFlowFieldCodec.HasVisibleArrows(Data.Payload.FlowField));
            Assert.True(Data.Payload.ArrowStride >= 1);
            Assert.True(Data.Payload.CutFillPairs.Count > 0);
            Assert.Equal(Data.Payload.OrderedPasses.Count, Data.Payload.CutFillPairs.Count);
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

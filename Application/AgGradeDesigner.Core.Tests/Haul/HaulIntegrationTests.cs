using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Features;
using AgGradeDesigner.Core.Geo;
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
                    GridSizeFt = 9.0,
                    EquipmentLengthFt = 72.0,
                    EnableMinimumWorkLength = false,
                    MinimumWorkLengthFt = 18.0
                };
                Original.Guidance.ArcSpeedMph = 6.5;
                Original.Optimization.ClusterModeAverageWindow = 0;
                Original.Ponding.Audit = true;
                Store.SaveHaulSettings(Original);

                HaulSettings Reloaded = Store.LoadHaulSettings();
                Assert.Equal(Original.ScraperCapacityLcy, Reloaded.ScraperCapacityLcy);
                Assert.Equal(Original.BladeWidthMm, Reloaded.BladeWidthMm);
                Assert.Equal(Original.GridSizeFt, Reloaded.GridSizeFt);
                Assert.Equal(Original.EquipmentLengthFt, Reloaded.EquipmentLengthFt);
                Assert.False(Reloaded.EnableMinimumWorkLength);
                Assert.Equal(18.0, Reloaded.MinimumWorkLengthFt);
                Assert.Equal(6.5, Reloaded.Guidance.ArcSpeedMph);
                Assert.Equal(0, Reloaded.Optimization.ClusterModeAverageWindow);
                Assert.True(Reloaded.Ponding.Audit);
                Assert.Equal(HaulSettings.DEFAULT_MAX_FUEL_GAL_PER_HOUR, Reloaded.MaxFuelGalPerHour);
                Assert.Equal(HaulSettings.DEFAULT_MAX_DRAWBAR_HP, Reloaded.MaxDrawbarHp);
                Assert.Equal(HaulSettings.DEFAULT_DIESEL_PRICE_PER_GALLON, Reloaded.PricePerGallon);
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
                // intent: drop the whole Haul element, including Guidance/Optimization/Ponding children
                int Start = Xml.IndexOf("<Haul", StringComparison.Ordinal);
                int End = Xml.IndexOf("</Haul>", Start, StringComparison.Ordinal);
                Xml = End >= 0
                    ? Xml.Remove(Start, End + "</Haul>".Length - Start)
                    : Xml.Remove(Start, Xml.IndexOf("/>", Start, StringComparison.Ordinal) + 2 - Start);
                AgProjectArchive.Write(ProjectPath, Xml);

                ProjectDocument Reloaded = AgDesignSerializer.Load(ProjectPath);
                HaulSettings Defaults = new HaulSettings();
                Assert.Equal(Defaults.ScraperCapacityLcy, Reloaded.HaulSettings.ScraperCapacityLcy);
                Assert.Equal(Defaults.BladeWidthMm, Reloaded.HaulSettings.BladeWidthMm);
                Assert.Equal(Defaults.GridSizeFt, Reloaded.HaulSettings.GridSizeFt);
                Assert.Equal(Defaults.EquipmentLengthFt, Reloaded.HaulSettings.EquipmentLengthFt);
                Assert.Equal(Defaults.EnableMinimumWorkLength, Reloaded.HaulSettings.EnableMinimumWorkLength);
                Assert.Equal(Defaults.Guidance.ArcSpeedMph, Reloaded.HaulSettings.Guidance.ArcSpeedMph);
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
                Project.HaulSettings.MinimumWorkLengthFt = 33.0;
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
                Assert.Contains("minimumWorkLengthFt=\"33", Xml, StringComparison.Ordinal);
                Assert.Contains("\"flowField\"", Xml, StringComparison.Ordinal);
                Assert.Contains("\"arrowStride\":3", Xml, StringComparison.Ordinal);

                ProjectDocument Reloaded = AgDesignSerializer.Load(ProjectPath);
                Assert.True(Reloaded.HasHaulData);
                Assert.Equal(42.0, Reloaded.HaulSettings.ScraperCapacityLcy);
                Assert.Equal(33.0, Reloaded.HaulSettings.MinimumWorkLengthFt);
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
        /// Verifies diesel inputs round-trip, and that a cleared price stays unset.
        /// </summary>
        [Fact]
        public void ProjectSave_RoundTripsDieselInputsIncludingUnsetPrice
            (
            )
        {
            const double FUEL_GAL_PER_HOUR = 10.0;
            const double DRAWBAR_HP = 160.0;
            const double DIESEL_COST = 842.15;

            string ProjectPath = TempFile.Path(".agproject");
            try
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
                Project.HaulSettings.MaxFuelGalPerHour = FUEL_GAL_PER_HOUR;
                Project.HaulSettings.MaxDrawbarHp = DRAWBAR_HP;
                Project.HaulSettings.PricePerGallon = null;
                Project.HaulData = new HaulData
                {
                    Payload = new HaulPersistedPayload
                    {
                        OrderedPasses = [new HaulPassDto { LoadBcy = 1.0 }],
                        DieselCost = DIESEL_COST
                    }
                };

                AgDesignSerializer.Save(Project, ProjectPath);
                ProjectDocument Reloaded = AgDesignSerializer.Load(ProjectPath);

                Assert.Equal(FUEL_GAL_PER_HOUR, Reloaded.HaulSettings.MaxFuelGalPerHour);
                Assert.Equal(DRAWBAR_HP, Reloaded.HaulSettings.MaxDrawbarHp);
                Assert.Null(Reloaded.HaulSettings.PricePerGallon);
                Assert.Equal(DIESEL_COST, Reloaded.HaulData!.Payload.DieselCost);
            }
            finally
            {
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
                Project.HaulSettings.GridSizeFt = 9.0;
                Project.HaulSettings.EquipmentLengthFt = 72.0;
                Project.HaulSettings.EnableMinimumWorkLength = false;
                Project.HaulSettings.MinimumWorkLengthFt = 45.0;
                Project.HaulSettings.Guidance.SteerHeadingDeg = 22.0;
                Project.HaulSettings.Pivot = true;
                Project.HaulSettings.PivotLatitudeDeg = 40.5;

                AgDesignSerializer.Save(Project, ProjectPath);
                ProjectDocument Reloaded = AgDesignSerializer.Load(ProjectPath);

                Assert.Equal(12.5, Reloaded.HaulSettings.ScraperCapacityLcy);
                Assert.Equal(3657.6, Reloaded.HaulSettings.BladeWidthMm);
                Assert.Equal(9.0, Reloaded.HaulSettings.GridSizeFt);
                Assert.Equal(72.0, Reloaded.HaulSettings.EquipmentLengthFt);
                Assert.False(Reloaded.HaulSettings.EnableMinimumWorkLength);
                Assert.Equal(45.0, Reloaded.HaulSettings.MinimumWorkLengthFt);
                Assert.Equal(22.0, Reloaded.HaulSettings.Guidance.SteerHeadingDeg);
                Assert.True(Reloaded.HaulSettings.Pivot);
                Assert.Equal(40.5, Reloaded.HaulSettings.PivotLatitudeDeg);
            }
            finally
            {
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Verifies grid size is passed to the engine independently of blade width.
        /// </summary>
        [Fact]
        public void HaulOptionsBuilder_PassesGridSizeSeparateFromBladeWidth
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            HaulSettings Settings = new HaulSettings
            {
                BladeWidthMm = 3657.6,
                GridSizeFt = 8.5
            };

            HaulBuilder.HaulBuilderOptions Options = HaulOptionsBuilder.Build(Project, Settings, "unused.agd");
            Assert.Equal(8.5, Options.GridSizeFt);
            Assert.Equal(3657.6, Options.BladeWidthMm);
        }

        /// <summary>
        /// Verifies equipment length is passed through to HaulBuilder options.
        /// </summary>
        [Fact]
        public void HaulOptionsBuilder_PassesEquipmentLengthFt
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            HaulSettings Settings = new HaulSettings { EquipmentLengthFt = 72.0 };

            HaulBuilder.HaulBuilderOptions Options = HaulOptionsBuilder.Build(Project, Settings, "unused.agd");
            Assert.Equal(72.0, Options.EquipmentLengthFt);
        }

        /// <summary>
        /// Verifies default blade width and grid size are both 8.5 ft.
        /// </summary>
        [Fact]
        public void HaulSettings_DefaultBladeWidthAndGridSize
            (
            )
        {
            HaulSettings Defaults = new HaulSettings();
            Assert.Equal(8.5, HaulDisplayUnits.BladeWidthToDisplay(Defaults.BladeWidthMm, DisplayUnitSystem.Imperial), precision: 3);
            Assert.Equal(8.5, Defaults.GridSizeFt);
        }

        /// <summary>
        /// Verifies default equipment length is 43 ft.
        /// </summary>
        [Fact]
        public void HaulSettings_DefaultEquipmentLength
            (
            )
        {
            HaulSettings Defaults = new HaulSettings();
            Assert.Equal(43.0, Defaults.EquipmentLengthFt);
        }

        /// <summary>
        /// Verifies default turning diameter matches the HaulBuilder production default.
        /// </summary>
        [Fact]
        public void HaulSettings_DefaultTurningCircle
            (
            )
        {
            HaulSettings Defaults = new HaulSettings();
            Assert.Equal(new HaulBuilder.HaulBuilderOptions().MinimumTurningDiameterFt, Defaults.TurningCircleFt);
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
        /// Verifies inch depth entry keeps five decimal places through store, options, and display.
        /// </summary>
        [Fact]
        public void DisplayUnits_DepthInches_PreservesFiveDecimalPlaces
            (
            )
        {
            const double EnteredInches = 1.12345;
            double StoredMm = HaulDisplayUnits.DepthMmFromDisplay(EnteredInches, DisplayUnitSystem.Imperial);
            double Displayed = HaulDisplayUnits.DepthToDisplay(StoredMm, DisplayUnitSystem.Imperial);
            Assert.Equal(EnteredInches, Displayed);

            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            HaulSettings Settings = new HaulSettings { MaxDepthMm = StoredMm, ToleranceMm = StoredMm };
            HaulBuilder.HaulBuilderOptions Options = HaulOptionsBuilder.Build(Project, Settings, "unused.agd");
            Assert.Equal(StoredMm, Options.MaxDepthMm);
            Assert.Equal(StoredMm, Options.ToleranceMm);
        }

        /// <summary>
        /// Verifies millimetre depth entry is stored and displayed as whole millimetres.
        /// </summary>
        [Fact]
        public void DisplayUnits_DepthMillimetres_UsesZeroDecimalPlaces
            (
            )
        {
            const double EnteredMm = 28.6;
            double StoredMm = HaulDisplayUnits.DepthMmFromDisplay(EnteredMm, DisplayUnitSystem.Metric);
            Assert.Equal(29.0, StoredMm);
            Assert.Equal(29.0, HaulDisplayUnits.DepthToDisplay(StoredMm, DisplayUnitSystem.Metric));
        }

        /// <summary>
        /// Verifies foot length entry keeps five decimal places through store and display.
        /// </summary>
        [Fact]
        public void DisplayUnits_LengthFeet_PreservesFiveDecimalPlaces
            (
            )
        {
            const double EnteredFeet = 7.12345;
            double StoredFt = HaulDisplayUnits.LengthFtFromDisplay(EnteredFeet, DisplayUnitSystem.Imperial);
            Assert.Equal(EnteredFeet, StoredFt);
            Assert.Equal(EnteredFeet, HaulDisplayUnits.LengthToDisplay(StoredFt, DisplayUnitSystem.Imperial));
        }

        /// <summary>
        /// Verifies blade width feet entry keeps five decimal places through millimetre storage.
        /// </summary>
        [Fact]
        public void DisplayUnits_BladeWidthFeet_PreservesFiveDecimalPlaces
            (
            )
        {
            const double EnteredFeet = 8.12345;
            double StoredMm = HaulDisplayUnits.BladeWidthMmFromDisplay(EnteredFeet, DisplayUnitSystem.Imperial);
            Assert.Equal(EnteredFeet, HaulDisplayUnits.BladeWidthToDisplay(StoredMm, DisplayUnitSystem.Imperial));
        }

        /// <summary>
        /// Verifies haul dialog OK preserves five-decimal distance precision into project settings.
        /// </summary>
        [Fact]
        public void HaulDialogOk_PreservesDistancePrecision
            (
            )
        {
            const double EnteredInches = 6.12345;
            const double EnteredFeet = 7.12345;
            HaulDialogViewModel Dialog = new HaulDialogViewModel(new HaulSettings(), DisplayUnitSystem.Imperial);
            Dialog.MaxDepthDisplay = EnteredInches;
            Dialog.ToleranceDisplay = EnteredInches;
            Dialog.GridSizeDisplay = EnteredFeet;
            Dialog.BladeWidthDisplay = EnteredFeet;
            Dialog.MinimumWorkLengthDisplay = EnteredFeet;
            Dialog.EquipmentWidthDisplay = EnteredFeet;
            Dialog.EquipmentLengthDisplay = EnteredFeet;
            Dialog.OkCommand.Execute(null);

            HaulSettings Accepted = Dialog.ToHaulSettings();
            Assert.Equal(EnteredInches, HaulDisplayUnits.DepthToDisplay(Accepted.MaxDepthMm, DisplayUnitSystem.Imperial));
            Assert.Equal(EnteredInches, HaulDisplayUnits.DepthToDisplay(Accepted.ToleranceMm, DisplayUnitSystem.Imperial));
            Assert.Equal(EnteredFeet, Accepted.GridSizeFt);
            Assert.Equal(EnteredFeet, HaulDisplayUnits.BladeWidthToDisplay(Accepted.BladeWidthMm, DisplayUnitSystem.Imperial));
            Assert.Equal(EnteredFeet, Accepted.MinimumWorkLengthFt);
            Assert.Equal(EnteredFeet, Accepted.EquipmentWidthFt);
            Assert.Equal(EnteredFeet, Accepted.EquipmentLengthFt);
        }

        /// <summary>
        /// Verifies a fresh haul setup matches the proof flow-ribbon search and trip guidance.
        /// </summary>
        [Fact]
        public void HaulSettings_DefaultsMatchProofRun
            (
            )
        {
            HaulSettings Settings = new HaulSettings();

            Assert.Equal(8.5, Settings.GridSizeFt);
            Assert.Equal(10.0, Settings.ToleranceMm);
            Assert.Equal(1.3, Settings.SwellFactor);
            Assert.Equal(61.0, Settings.MaxDepthMm);
            Assert.Equal(20.0, Settings.ScraperCapacityLcy);
            Assert.Equal(2590.8, Settings.BladeWidthMm, precision: 6);
            Assert.Equal(45.0, Settings.TurningCircleFt);
            Assert.Equal(4.0, Settings.LoadingSpeedMph);
            Assert.Equal(10.0, Settings.HaulSpeedMph);
            Assert.Equal(5.0, Settings.DumpingSpeedMph);
            Assert.Equal(13.0, Settings.ReturnSpeedMph);
            Assert.Equal(12.0, Settings.AccelTimeS);
            Assert.Equal(12.0, Settings.DecelTimeS);
            Assert.Equal(0.8, Settings.FieldEfficiency);
            Assert.Equal(3, Settings.Optimization.ClusterModeAverageWindow);
            Assert.Equal(0.5, Settings.Optimization.ClusterModeLoadFraction);
            Assert.False(Settings.Optimization.EnableRouteConsolidation);
            Assert.Equal(0, Settings.Optimization.PairRefinementRounds);
            Assert.Equal(0, Settings.Optimization.RouteReorderWindow);
            Assert.False(Settings.Optimization.EnableEarlyFullStripBanding);
            Assert.Equal(4.0, Settings.Guidance.ShortJointRadii);
            Assert.Equal(0.02, Settings.Guidance.ConnectorTieFraction);
            Assert.Equal(0.5, Settings.Guidance.ConnectorTieRadii);
            Assert.Equal(30.0, Settings.Guidance.SteerHeadingDeg);
            Assert.Equal(3, Settings.Guidance.MaxCorrectingLines);
            Assert.Equal(10.0, Settings.Guidance.LeftoverRatio);
            Assert.Equal(2.0, Settings.Guidance.MergeRouteRadii);
            Assert.Equal(1.0, Settings.Guidance.EndFragmentSpanRadii);
            Assert.Equal(4.0, Settings.Guidance.EndFragmentGapRadii);
            Assert.Equal(0.5, Settings.Guidance.NearEmptyRadii);
            Assert.Equal(0.01, Settings.Guidance.NearEmptyBowlFraction);
            Assert.Equal(5.0, Settings.Guidance.ArcSpeedMph);
            Assert.Equal(5.0, Settings.Guidance.GradeLaneSpeedMph);
        }

        /// <summary>
        /// Verifies options mapping uses the design cut/fill ratio and the current HaulBuilder groups.
        /// </summary>
        [Fact]
        public void HaulOptionsBuilder_UsesDesignCutFillRatioAndCurrentOptionGroups
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            Project.DesignParameters!.CutFillRatio = 1.15;

            HaulSettings Settings = new HaulSettings
            {
                EnableMinimumWorkLength = true,
                MinimumWorkLengthFt = 12.0
            };
            Settings.Guidance.ArcSpeedMph = 7.0;
            Settings.Optimization.EnableCycleChainOptimization = true;
            Settings.Ponding.Optimize = true;
            Settings.Ponding.ObjectiveMode = HaulBuilder.PondingObjectiveMode.AverageDepth;
            Settings.Ponding.CheckpointInterval = 0;
            Settings.Ponding.OutletSpecs.Add("40.1,-105.2");

            HaulBuilder.HaulBuilderOptions Options = HaulOptionsBuilder.Build(Project, Settings, "unused.agd");
            Assert.Equal(1.15, Options.CutFillRatio);
            Assert.False(Options.SkipGuidance);
            Assert.False(Options.IncludeGuidanceCsv);
            Assert.True(Options.EnableMinimumWorkLength);
            Assert.Equal(12.0, Options.MinimumWorkLengthFt);
            Assert.Equal(7.0, Options.Guidance.ArcSpeedMph);
            Assert.True(Options.Optimization.EnableCycleChainOptimization);
            Assert.True(Options.Ponding.Optimize);
            Assert.Equal(HaulBuilder.PondingObjectiveMode.AverageDepth, Options.Ponding.ObjectiveMode);
            Assert.Null(Options.Ponding.CheckpointInterval);
            Assert.Equal("40.1,-105.2", Assert.Single(Options.Ponding.OutletSpecs));
            Assert.Equal(HaulSettings.DEFAULT_MAX_FUEL_GAL_PER_HOUR, Options.Diesel.MaxFuelGalPerHour);
            Assert.Equal(HaulSettings.DEFAULT_MAX_DRAWBAR_HP, Options.Diesel.MaxDrawbarHp);
            Assert.Equal(HaulSettings.DEFAULT_DIESEL_PRICE_PER_GALLON, Options.Diesel.PricePerGallon);
        }

        /// <summary>
        /// Verifies a missing fuel rate, horsepower, or price leaves every diesel input unset.
        /// </summary>
        [Fact]
        public void HaulOptionsBuilder_LeavesDieselUnsetWhenAnyInputIsMissing
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            HaulSettings Settings = new HaulSettings
            {
                MaxFuelGalPerHour = HaulSettings.DEFAULT_MAX_FUEL_GAL_PER_HOUR,
                MaxDrawbarHp = HaulSettings.DEFAULT_MAX_DRAWBAR_HP,
                PricePerGallon = null
            };

            HaulBuilder.HaulBuilderOptions Options = HaulOptionsBuilder.Build(Project, Settings, "unused.agd");

            Assert.Null(Options.Diesel.MaxFuelGalPerHour);
            Assert.Null(Options.Diesel.MaxDrawbarHp);
            Assert.Null(Options.Diesel.PricePerGallon);
        }

        /// <summary>
        /// Verifies custom diesel inputs are the values sent to the engine.
        /// </summary>
        [Fact]
        public void HaulOptionsBuilder_PassesDieselInputsWhenAllThreeAreSet
            (
            )
        {
            const double FUEL_GAL_PER_HOUR = 11.0;
            const double DRAWBAR_HP = 150.0;
            const double PRICE_PER_GALLON = 5.25;

            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            HaulSettings Settings = new HaulSettings
            {
                MaxFuelGalPerHour = FUEL_GAL_PER_HOUR,
                MaxDrawbarHp = DRAWBAR_HP,
                PricePerGallon = PRICE_PER_GALLON
            };

            HaulBuilder.HaulBuilderOptions Options = HaulOptionsBuilder.Build(Project, Settings, "unused.agd");

            Assert.Equal(FUEL_GAL_PER_HOUR, Options.Diesel.MaxFuelGalPerHour);
            Assert.Equal(DRAWBAR_HP, Options.Diesel.MaxDrawbarHp);
            Assert.Equal(PRICE_PER_GALLON, Options.Diesel.PricePerGallon);
        }

        /// <summary>
        /// Verifies a project with no placed wells or roads sends none, and that crown, grade,
        /// slope-away, and casing exclusion stay at the engine defaults.
        /// </summary>
        [Fact]
        public void HaulOptionsBuilder_OmitsRoadsAndPadsWhenNoneArePlaced
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            HaulBuilder.HaulBuilderOptions Options = HaulOptionsBuilder.Build(Project, new HaulSettings(), "unused.agd");

            Assert.Empty(Options.Roads);
            Assert.Empty(Options.WellPads);
            Assert.Equal(2.0, Options.RoadCrossSlopePct);
            Assert.Equal(8.0, Options.RoadMaxGradePct);
            Assert.Equal(1.5, Options.PadSlopeAwayPct);
            Assert.Equal(10.0, Options.PadExclusionRadiusFt);
        }

        /// <summary>Latitude of the first placed feature in the haul-options test.</summary>
        private const double PLACED_FEATURE_LATITUDE = 40.0;

        /// <summary>Longitude of the first placed feature in the haul-options test.</summary>
        private const double PLACED_FEATURE_LONGITUDE = -105.0;

        /// <summary>Degrees added between road vertices and between the two wells.</summary>
        private const double PLACED_FEATURE_OFFSET_DEG = 0.01;

        /// <summary>Finished bed of the placed road, in feet. An 8.5 ft blade and a 3:1 slope give 14 ft.</summary>
        private const double PLACED_ROAD_BED_WIDTH_FT = 14.0;

        /// <summary>Pad diameter smaller than twice the engine clearance, so the whole radius is the turn.</summary>
        private const double SMALL_PAD_DIAMETER_FT = 4.0;

        /// <summary>How close a projected metre or a foot value must match.</summary>
        private const int PLACED_FEATURE_PRECISION = 6;

        /// <summary>Half of a pad diameter. The haul top radius is the turn plus clearance.</summary>
        private const double PAD_RADIUS_OF_DIAMETER = 0.5;

        /// <summary>
        /// Verifies saved wells and roads are sent in project metres, and a reference point is not.
        /// </summary>
        [Fact]
        public void HaulOptionsBuilder_SendsPlacedWellsAndRoads
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            double BedM = PLACED_ROAD_BED_WIDTH_FT * FieldConstants.METERS_PER_FOOT;
            double HeightM = RoadCrossSection.DEFAULT_HEIGHT_FT * FieldConstants.METERS_PER_FOOT;
            double MidLatitude = PLACED_FEATURE_LATITUDE + PLACED_FEATURE_OFFSET_DEG;
            double MidLongitude = PLACED_FEATURE_LONGITUDE + PLACED_FEATURE_OFFSET_DEG;
            double EndLatitude = MidLatitude + PLACED_FEATURE_OFFSET_DEG;
            double EndLongitude = MidLongitude + PLACED_FEATURE_OFFSET_DEG;
            Project.Features.AddRoad(
                new FeatureVertex[]
                {
                    new FeatureVertex(PLACED_FEATURE_LATITUDE, PLACED_FEATURE_LONGITUDE),
                    new FeatureVertex(MidLatitude, MidLongitude),
                    new FeatureVertex(EndLatitude, EndLongitude)
                },
                BedM,
                HeightM,
                RoadCrossSection.DEFAULT_SIDE_SLOPE);
            // One vertex is not a road. It must not be sent.
            Project.Features.AddRoad(
                new FeatureVertex[] { new FeatureVertex(EndLatitude, EndLongitude) },
                BedM,
                HeightM,
                RoadCrossSection.DEFAULT_SIDE_SLOPE);
            Project.Features.AddWell(
                PLACED_FEATURE_LATITUDE,
                PLACED_FEATURE_LONGITUDE,
                FieldFeatureSet.DEFAULT_PAD_DIAMETER_FT * FieldConstants.METERS_PER_FOOT);
            Project.Features.AddWell(
                MidLatitude,
                MidLongitude,
                SMALL_PAD_DIAMETER_FT * FieldConstants.METERS_PER_FOOT);
            Project.Features.AddReferencePoint(EndLatitude, EndLongitude, "North gate");

            HaulBuilder.HaulBuilderOptions Options = HaulOptionsBuilder.Build(Project, new HaulSettings(), "unused.agd");

            HaulBuilder.RoadCentreline Road = Assert.Single(Options.Roads);
            Assert.Equal(3, Road.Vertices.Count);
            AssertProjected(PLACED_FEATURE_LATITUDE, PLACED_FEATURE_LONGITUDE, Road.Vertices[0].X, Road.Vertices[0].Y);
            AssertProjected(MidLatitude, MidLongitude, Road.Vertices[1].X, Road.Vertices[1].Y);
            AssertProjected(EndLatitude, EndLongitude, Road.Vertices[2].X, Road.Vertices[2].Y);
            Assert.Equal(1, Road.MinLanes);
            Assert.Equal(PLACED_ROAD_BED_WIDTH_FT, Road.MinRoadbedFt!.Value, PLACED_FEATURE_PRECISION);
            Assert.Equal(RoadCrossSection.DEFAULT_HEIGHT_FT, Road.HeightFt!.Value, PLACED_FEATURE_PRECISION);
            Assert.Equal(RoadCrossSection.DEFAULT_SIDE_SLOPE, Road.SideSlope);
            Assert.Null(Road.CrossSlopePct);
            Assert.Null(Road.MaxGradePct);

            Assert.Equal(2, Options.WellPads.Count);
            HaulBuilder.WellPad FullPad = Options.WellPads[0];
            AssertProjected(PLACED_FEATURE_LATITUDE, PLACED_FEATURE_LONGITUDE, FullPad.X, FullPad.Y);
            Assert.Equal(
                FieldFeatureSet.DEFAULT_PAD_DIAMETER_FT * PAD_RADIUS_OF_DIAMETER,
                PadTopRadiusFt(Options, FullPad),
                PLACED_FEATURE_PRECISION);
            Assert.Null(FullPad.HeightFt);
            Assert.Null(FullPad.SideSlope);
            Assert.Null(FullPad.SlopeAwayPct);
            Assert.Null(FullPad.ExclusionRadiusFt);

            HaulBuilder.WellPad SmallPad = Options.WellPads[1];
            AssertProjected(MidLatitude, MidLongitude, SmallPad.X, SmallPad.Y);
            Assert.Equal(0.0, SmallPad.ClearanceFt);
            Assert.Equal(
                SMALL_PAD_DIAMETER_FT * PAD_RADIUS_OF_DIAMETER,
                SmallPad.DesignTurnRadiusFt!.Value,
                PLACED_FEATURE_PRECISION);
        }

        /// <summary>
        /// Checks that a haul coordinate is the UTM easting and northing of a latitude and longitude.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        /// <param name="Easting">Easting sent to the haul, in metres.</param>
        /// <param name="Northing">Northing sent to the haul, in metres.</param>
        private static void AssertProjected
            (
            double Latitude,
            double Longitude,
            double Easting,
            double Northing
            )
        {
            UTM.UTMCoordinate Projected = UTM.FromLatLon(Latitude, Longitude);
            Assert.Equal(Projected.Easting, Easting, PLACED_FEATURE_PRECISION);
            Assert.Equal(Projected.Northing, Northing, PLACED_FEATURE_PRECISION);
        }

        /// <summary>
        /// Pad top radius the haul will build: the design-vehicle turn plus clearance.
        /// An unset override uses the engine default.
        /// </summary>
        /// <param name="Options">Options that hold the defaults.</param>
        /// <param name="Pad">Pad whose overrides are applied.</param>
        /// <returns>Top radius in feet.</returns>
        private static double PadTopRadiusFt
            (
            HaulBuilder.HaulBuilderOptions Options,
            HaulBuilder.WellPad Pad
            )
        {
            double TurnFt = Pad.DesignTurnRadiusFt ?? Options.PadDesignTurnRadiusFt;
            double ClearanceFt = Pad.ClearanceFt ?? Options.PadClearanceFt;
            return TurnFt + ClearanceFt;
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
            Assert.NotNull(Data.Payload.FlowField);
            Assert.True(HaulFlowFieldCodec.IsStructurallyValid(Data.Payload.FlowField));
            Assert.True(HaulFlowFieldCodec.HasVisibleArrows(Data.Payload.FlowField));
            Assert.True(Data.Payload.ArrowStride >= 1);
            Assert.True(Data.Payload.CutFillPairs.Count > 0);
            Assert.Equal(Data.Payload.OrderedPasses.Count, Data.Payload.CutFillPairs.Count);
            Assert.NotNull(Data.Payload.GuidanceTime);
            Assert.True(Data.Payload.GuidanceTime.TotalSeconds > 0.0);
            Assert.NotNull(Data.ResolveGuidanceTime());
            Assert.Equal(
                Data.Payload.GuidanceTime.TotalSeconds,
                Data.ResolveGuidanceTime()!.TotalSeconds,
                precision: 6);
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

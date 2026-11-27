using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Tests.TestFixtures;
using AgGradeDesigner.Core.Units;
using AgGradeDesigner.Services;
using AgGradeDesigner.ViewModels;

namespace AgGradeDesigner.Core.Tests.Units
{
    /// <summary>
    /// Unit tests for equipment values edited in the settings dialog.
    /// </summary>
    public sealed class SettingsDialogEquipmentTests
    {
        /// <summary>
        /// Verifies OK writes equipment onto the open project and the user defaults,
        /// and leaves travel speeds on each copy alone.
        /// </summary>
        [Fact]
        public void Ok_PersistsEquipmentOntoProjectAndUserDefaults
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                const double USER_HAUL_SPEED_MPH = 4.0;
                const double PROJECT_HAUL_SPEED_MPH = 9.0;
                const double EDITED_BLADE_WIDTH_FT = 10.0;

                UserSettingsStore Store = new UserSettingsStore(Path);
                Store.SaveDisplayUnitsSettings(new DisplayUnitsSettings { System = DisplayUnitSystem.Imperial });
                HaulSettings UserDefaults = new HaulSettings { HaulSpeedMph = USER_HAUL_SPEED_MPH };
                Store.SaveHaulSettings(UserDefaults);

                UserSettingsService Service = new UserSettingsService(Store);
                HaulSettings Project = new HaulSettings { HaulSpeedMph = PROJECT_HAUL_SPEED_MPH };
                SettingsDialogViewModel Dialog = new SettingsDialogViewModel(Service, Project);
                Dialog.Equipment.BladeWidthDisplay = EDITED_BLADE_WIDTH_FT;
                Dialog.OkCommand.Execute(null);

                double ExpectedBladeMm = HaulDisplayUnits.BladeWidthMmFromDisplay(
                    EDITED_BLADE_WIDTH_FT,
                    DisplayUnitSystem.Imperial);
                Assert.True(Dialog.Confirmed);
                Assert.Equal(ExpectedBladeMm, Project.BladeWidthMm);
                Assert.Equal(ExpectedBladeMm, Store.LoadHaulSettings().BladeWidthMm);
                Assert.Equal(PROJECT_HAUL_SPEED_MPH, Project.HaulSpeedMph);
                Assert.Equal(USER_HAUL_SPEED_MPH, Store.LoadHaulSettings().HaulSpeedMph);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies cancel leaves the project and the user defaults unchanged.
        /// </summary>
        [Fact]
        public void Cancel_DiscardsEquipmentEdits
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                const double EDITED_BLADE_WIDTH_FT = 10.0;

                UserSettingsStore Store = new UserSettingsStore(Path);
                Store.SaveDisplayUnitsSettings(new DisplayUnitsSettings { System = DisplayUnitSystem.Imperial });
                HaulSettings UserDefaults = new HaulSettings();
                Store.SaveHaulSettings(UserDefaults);

                UserSettingsService Service = new UserSettingsService(Store);
                HaulSettings Project = new HaulSettings();
                double OriginalBladeMm = Project.BladeWidthMm;
                SettingsDialogViewModel Dialog = new SettingsDialogViewModel(Service, Project);
                Dialog.Equipment.BladeWidthDisplay = EDITED_BLADE_WIDTH_FT;
                Dialog.CancelCommand.Execute(null);

                Assert.False(Dialog.Confirmed);
                Assert.Equal(OriginalBladeMm, Project.BladeWidthMm);
                Assert.Equal(OriginalBladeMm, Store.LoadHaulSettings().BladeWidthMm);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies changing display units redisplays the blade width in the new system
        /// without dropping the value the user already typed.
        /// </summary>
        [Fact]
        public void UnitChange_RedisplaysBladeWidthInTheNewSystem
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                const double EDITED_BLADE_WIDTH_FT = 10.0;

                UserSettingsStore Store = new UserSettingsStore(Path);
                Store.SaveDisplayUnitsSettings(new DisplayUnitsSettings { System = DisplayUnitSystem.Imperial });
                UserSettingsService Service = new UserSettingsService(Store);
                SettingsDialogViewModel Dialog = new SettingsDialogViewModel(Service, new HaulSettings());
                Dialog.Equipment.BladeWidthDisplay = EDITED_BLADE_WIDTH_FT;
                Dialog.SelectedDisplayUnitOption = Dialog.DisplayUnitOptions.First(O => O.System == DisplayUnitSystem.Metric);

                double ExpectedMetres = HaulDisplayUnits.BladeWidthToDisplay(
                    HaulDisplayUnits.BladeWidthMmFromDisplay(EDITED_BLADE_WIDTH_FT, DisplayUnitSystem.Imperial),
                    DisplayUnitSystem.Metric);
                Assert.Equal(ExpectedMetres, Dialog.Equipment.BladeWidthDisplay);
                Assert.Equal("m", Dialog.Equipment.BladeWidthUnitSuffix);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies a new dialog shows the diesel defaults and lists a Diesel section.
        /// </summary>
        [Fact]
        public void Load_ShowsDieselDefaults
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                UserSettingsStore Store = new UserSettingsStore(Path);
                Store.SaveDisplayUnitsSettings(new DisplayUnitsSettings { System = DisplayUnitSystem.Imperial });
                UserSettingsService Service = new UserSettingsService(Store);
                SettingsDialogViewModel Dialog = new SettingsDialogViewModel(Service, new HaulSettings());

                Assert.Contains(Dialog.Sections, Section => Section.Title == "Diesel");
                Assert.Equal(
                    DieselInputFormat.Format(HaulSettings.DEFAULT_MAX_FUEL_GAL_PER_HOUR),
                    Dialog.Equipment.MaxFuelGalPerHourText);
                Assert.Equal(
                    DieselInputFormat.Format(HaulSettings.DEFAULT_MAX_DRAWBAR_HP),
                    Dialog.Equipment.MaxDrawbarHpText);
                Assert.Equal(
                    DieselInputFormat.Format(HaulSettings.DEFAULT_DIESEL_PRICE_PER_GALLON),
                    Dialog.Diesel.PricePerGallonText);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies OK writes the tractor inputs and the diesel price onto the project and the user defaults.
        /// </summary>
        [Fact]
        public void Ok_PersistsDieselInputs
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                const double FUEL_GAL_PER_HOUR = 9.5;
                const double DRAWBAR_HP = 140.0;
                const double PRICE_PER_GALLON = 5.10;
                const double USER_HAUL_SPEED_MPH = 4.0;

                UserSettingsStore Store = new UserSettingsStore(Path);
                Store.SaveDisplayUnitsSettings(new DisplayUnitsSettings { System = DisplayUnitSystem.Imperial });
                Store.SaveHaulSettings(new HaulSettings { HaulSpeedMph = USER_HAUL_SPEED_MPH });

                UserSettingsService Service = new UserSettingsService(Store);
                HaulSettings Project = new HaulSettings { HaulSpeedMph = 9.0 };
                SettingsDialogViewModel Dialog = new SettingsDialogViewModel(Service, Project);
                Dialog.Equipment.MaxFuelGalPerHourText = DieselInputFormat.Format(FUEL_GAL_PER_HOUR);
                Dialog.Equipment.MaxDrawbarHpText = DieselInputFormat.Format(DRAWBAR_HP);
                Dialog.Diesel.PricePerGallonText = DieselInputFormat.Format(PRICE_PER_GALLON);
                Dialog.OkCommand.Execute(null);

                Assert.Equal(FUEL_GAL_PER_HOUR, Project.MaxFuelGalPerHour);
                Assert.Equal(DRAWBAR_HP, Project.MaxDrawbarHp);
                Assert.Equal(PRICE_PER_GALLON, Project.PricePerGallon);
                HaulSettings Saved = Store.LoadHaulSettings();
                Assert.Equal(FUEL_GAL_PER_HOUR, Saved.MaxFuelGalPerHour);
                Assert.Equal(DRAWBAR_HP, Saved.MaxDrawbarHp);
                Assert.Equal(PRICE_PER_GALLON, Saved.PricePerGallon);
                Assert.Equal(USER_HAUL_SPEED_MPH, Saved.HaulSpeedMph);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies clearing one diesel box leaves that input unset.
        /// </summary>
        [Fact]
        public void Ok_ClearedPriceStaysUnset
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                UserSettingsStore Store = new UserSettingsStore(Path);
                Store.SaveDisplayUnitsSettings(new DisplayUnitsSettings { System = DisplayUnitSystem.Imperial });
                UserSettingsService Service = new UserSettingsService(Store);
                HaulSettings Project = new HaulSettings();
                SettingsDialogViewModel Dialog = new SettingsDialogViewModel(Service, Project);
                Dialog.Diesel.PricePerGallonText = string.Empty;
                Dialog.OkCommand.Execute(null);

                Assert.Null(Project.PricePerGallon);
                Assert.Equal(HaulSettings.DEFAULT_MAX_FUEL_GAL_PER_HOUR, Project.MaxFuelGalPerHour);
                Assert.Equal(HaulSettings.DEFAULT_MAX_DRAWBAR_HP, Project.MaxDrawbarHp);
                Assert.Null(Store.LoadHaulSettings().PricePerGallon);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}

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
    }
}

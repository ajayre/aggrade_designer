using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Units;
using AgGradeDesigner.Core.Tests.TestFixtures;
using AgGradeDesigner.Services;
using AgGradeDesigner.ViewModels;

namespace AgGradeDesigner.Core.Tests.Units
{
    /// <summary>
    /// Unit tests for settings dialog display unit persistence.
    /// </summary>
    public sealed class SettingsDialogUnitsSettingsTests
    {
        /// <summary>
        /// Verifies cancel discards display unit edits and leaves the user settings file unchanged.
        /// </summary>
        [Fact]
        public void Cancel_DiscardsDisplayUnitEditsAndLeavesFileUnchanged
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                UserSettingsStore Store = new UserSettingsStore(Path);
                Store.SaveDisplayUnitsSettings(new DisplayUnitsSettings { System = DisplayUnitSystem.Imperial });

                UserSettingsService Service = new UserSettingsService(Store);
                SettingsDialogViewModel Dialog = new SettingsDialogViewModel(Service);
                Dialog.SelectedDisplayUnitOption = Dialog.DisplayUnitOptions.First(O => O.System == DisplayUnitSystem.Metric);
                Dialog.CancelCommand.Execute(null);

                Assert.Equal(DisplayUnitSystem.Imperial, Store.LoadDisplayUnitsSettings().System);
                Assert.False(Dialog.Confirmed);

                SettingsDialogViewModel Reopened = new SettingsDialogViewModel(Service);
                Assert.Equal(DisplayUnitSystem.Imperial, Reopened.SelectedDisplayUnitSystem);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies OK persists display unit edits to the user settings file.
        /// </summary>
        [Fact]
        public void Ok_PersistsDisplayUnitEdits
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                UserSettingsStore Store = new UserSettingsStore(Path);
                UserSettingsService Service = new UserSettingsService(Store);
                SettingsDialogViewModel Dialog = new SettingsDialogViewModel(Service);
                Dialog.SelectedDisplayUnitOption = Dialog.DisplayUnitOptions.First(O => O.System == DisplayUnitSystem.Metric);
                Dialog.OkCommand.Execute(null);

                Assert.True(Dialog.Confirmed);
                Assert.Equal(DisplayUnitSystem.Metric, Store.LoadDisplayUnitsSettings().System);
                Assert.Equal(DisplayUnitSystem.Metric, Service.ActiveDisplayUnitsSettings.System);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}

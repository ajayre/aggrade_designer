using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.Chat;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;
using AgGradeDesigner.Core.Tests.TestFixtures;
using AgGradeDesigner.Services;
using AgGradeDesigner.ViewModels;

namespace AgGradeDesigner.Core.Tests.Ai
{
    /// <summary>
    /// Unit tests for settings dialog cancel behaviour and AI settings persistence.
    /// </summary>
    public sealed class SettingsDialogAiSettingsTests
    {
        /// <summary>
        /// Verifies cancel discards session edits and leaves the user settings file unchanged.
        /// </summary>
        [Fact]
        public void Cancel_DiscardsSessionEditsAndLeavesFileUnchanged
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                UserSettingsStore Store = new UserSettingsStore(Path);
                Store.SaveAiSettings(new AiSettings
                {
                    Provider = AiProviderKind.OpenAi,
                    ApiKey = "saved-key",
                    ModelOverride = "saved-model"
                });

                UserSettingsService Service = new UserSettingsService(Store);
                SettingsDialogViewModel Dialog = new SettingsDialogViewModel(Service);
                Dialog.ApiKey = "edited-key";
                Dialog.ModelOverride = "edited-model";
                Dialog.CancelCommand.Execute(null);

                AiSettings Reloaded = Store.LoadAiSettings();
                Assert.Equal("saved-key", Reloaded.ApiKey);
                Assert.Equal("saved-model", Reloaded.ModelOverride);
                Assert.False(Dialog.Confirmed);

                SettingsDialogViewModel Reopened = new SettingsDialogViewModel(Service);
                Assert.Equal("saved-key", Reopened.ApiKey);
                Assert.Equal("saved-model", Reopened.ModelOverride);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies OK persists session edits to the user settings file.
        /// </summary>
        [Fact]
        public void Ok_PersistsSessionEdits
            (
            )
        {
            string Path = TempFile.Path(".json");
            try
            {
                UserSettingsStore Store = new UserSettingsStore(Path);
                UserSettingsService Service = new UserSettingsService(Store);
                SettingsDialogViewModel Dialog = new SettingsDialogViewModel(Service);
                Dialog.ApiKey = "new-key";
                Dialog.OkCommand.Execute(null);

                Assert.True(Dialog.Confirmed);
                Assert.Equal("new-key", Store.LoadAiSettings().ApiKey);
                Assert.Equal("new-key", Service.ActiveAiSettings.ApiKey);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}

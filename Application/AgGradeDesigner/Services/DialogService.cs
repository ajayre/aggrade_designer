using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Units;
using AgGradeDesigner.Core.Water;
using AgGradeDesigner.ViewModels;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace AgGradeDesigner.Services
{
    /// <summary>
    /// Shows modal dialogs owned by the main window.
    /// </summary>
    static public class DialogService
    {
        /// <summary>
        /// Resolves the main window as dialog owner when running on desktop lifetime.
        /// </summary>
        /// <returns>Main window, or null when no desktop host is active.</returns>
        static public Window? GetOwner
            (
            )
        {
            // Desktop lifetime exposes a single main window suitable for modal ownership.
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime Desktop)
                return Desktop.MainWindow;
            return null;
        }

        /// <summary>
        /// Shows a yes/no confirmation dialog.
        /// </summary>
        /// <param name="Title">Dialog title text.</param>
        /// <param name="Message">Body message shown to the user.</param>
        /// <returns>True when the user confirms; false when cancelled or owner is missing.</returns>
        static public async Task<bool> ShowConfirmAsync
            (
            string Title,
            string Message
            )
        {
            Window? Owner = GetOwner();
            if (Owner == null)
                return false;

            ConfirmDialogViewModel Vm = new ConfirmDialogViewModel
            {
                Title = Title,
                Message = Message,
                IsQuestion = true
            };
            Views.ConfirmDialog Dialog = new Views.ConfirmDialog { DataContext = Vm };
            await Dialog.ShowDialog(Owner);
            return Vm.Confirmed;
        }

        /// <summary>
        /// Shows an OK-only message dialog for errors and notifications.
        /// </summary>
        /// <param name="Title">Dialog title text.</param>
        /// <param name="Message">Body message shown to the user.</param>
        static public async Task ShowMessageAsync
            (
            string Title,
            string Message
            )
        {
            Window? Owner = GetOwner();
            if (Owner == null)
                return;

            ConfirmDialogViewModel Vm = new ConfirmDialogViewModel
            {
                Title = Title,
                Message = Message,
                IsQuestion = false
            };
            Views.ConfirmDialog Dialog = new Views.ConfirmDialog { DataContext = Vm };
            await Dialog.ShowDialog(Owner);
        }

        /// <summary>
        /// Shows the design parameters dialog and returns accepted values.
        /// </summary>
        /// <param name="Existing">Current design parameters to pre-fill, if any.</param>
        /// <returns>Accepted view model, or null when cancelled.</returns>
        static public async Task<DesignDialogViewModel?> ShowDesignDialogAsync
            (
            AgGradeDesigner.Core.Models.DesignParameters? Existing
            )
        {
            Window? Owner = GetOwner();
            if (Owner == null)
                return null;

            DesignDialogViewModel Vm = new DesignDialogViewModel();
            Vm.LoadFrom(Existing, UserSettingsService.Instance.ActiveDisplayUnitsSettings.System);
            Views.DesignDialog Dialog = new Views.DesignDialog { DataContext = Vm };
            await Dialog.ShowDialog(Owner);
            return Vm.Confirmed ? Vm : null;
        }

        /// <summary>
        /// Shows the haul configuration dialog and returns accepted settings.
        /// </summary>
        /// <param name="ProjectSettings">Current project haul settings.</param>
        /// <returns>Accepted view model, or null when cancelled.</returns>
        static public async Task<HaulDialogViewModel?> ShowHaulDialogAsync
            (
            HaulSettings ProjectSettings
            )
        {
            Window? Owner = GetOwner();
            if (Owner == null)
                return null;

            HaulDialogViewModel Vm = new HaulDialogViewModel(
                ProjectSettings,
                UserSettingsService.Instance.ActiveDisplayUnitsSettings.System);
            Views.HaulDialog Dialog = new Views.HaulDialog { DataContext = Vm };
            await Dialog.ShowDialog(Owner);
            return Vm.Confirmed ? Vm : null;
        }

        /// <summary>
        /// Shows the water settings dialog and returns accepted settings.
        /// </summary>
        /// <param name="ProjectSettings">Current project water settings.</param>
        /// <returns>Accepted view model, or null when cancelled.</returns>
        static public async Task<WaterDialogViewModel?> ShowWaterDialogAsync
            (
            WaterSettings ProjectSettings
            )
        {
            Window? Owner = GetOwner();
            if (Owner == null)
                return null;

            WaterDialogViewModel Vm = new WaterDialogViewModel(
                ProjectSettings,
                UserSettingsService.Instance.ActiveDisplayUnitsSettings.System);
            Views.WaterDialog Dialog = new Views.WaterDialog { DataContext = Vm };
            await Dialog.ShowDialog(Owner);
            return Vm.Confirmed ? Vm : null;
        }

        /// <summary>
        /// Shows the settings dialog.
        /// </summary>
        /// <returns>True when the user clicks OK; false when cancelled.</returns>
        static public async Task<bool> ShowSettingsDialogAsync
            (
            )
        {
            Window? Owner = GetOwner();
            if (Owner == null)
                return false;

            SettingsDialogViewModel Vm = new SettingsDialogViewModel();
            Views.SettingsDialog Dialog = new Views.SettingsDialog { DataContext = Vm };
            await Dialog.ShowDialog(Owner);
            return Vm.Confirmed;
        }

        /// <summary>
        /// Shows read-only survey statistics for the loaded survey.
        /// </summary>
        /// <param name="Survey">Survey to summarize, or null when none is loaded.</param>
        static public async Task ShowSurveyStatisticsAsync
            (
            AgGradeDesigner.Core.Survey.SurveyModel? Survey
            )
        {
            Window? Owner = GetOwner();
            if (Owner == null)
                return;

            SurveyStatisticsViewModel Vm = new SurveyStatisticsViewModel();
            Vm.LoadFrom(Survey, UserSettingsService.Instance.ActiveDisplayUnitsSettings.System);
            Views.SurveyStatisticsDialog Dialog = new Views.SurveyStatisticsDialog { DataContext = Vm };
            await Dialog.ShowDialog(Owner);
        }

        /// <summary>
        /// Shows the import kind chooser (survey vs design).
        /// </summary>
        /// <returns>Selected import kind, or null when cancelled.</returns>
        static public async Task<ImportChoiceViewModel.ImportKind?> ShowImportChoiceAsync
            (
            )
        {
            Window? Owner = GetOwner();
            if (Owner == null)
                return null;

            ImportChoiceViewModel Vm = new ImportChoiceViewModel();
            Views.ImportChoiceDialog Dialog = new Views.ImportChoiceDialog { DataContext = Vm };
            await Dialog.ShowDialog(Owner);
            return Vm.Confirmed ? Vm.SelectedKind : null;
        }

        /// <summary>
        /// Shows the export kind chooser filtered by loaded content.
        /// </summary>
        /// <param name="HasSurvey">Whether survey data is available to export.</param>
        /// <param name="HasDesign">Whether design data is available to export.</param>
        /// <returns>Selected export kind, or null when cancelled.</returns>
        static public async Task<ExportChoiceViewModel.ExportKind?> ShowExportChoiceAsync
            (
            bool HasSurvey,
            bool HasDesign
            )
        {
            Window? Owner = GetOwner();
            if (Owner == null)
                return null;

            ExportChoiceViewModel Vm = new ExportChoiceViewModel
            {
                HasSurvey = HasSurvey,
                HasDesign = HasDesign
            };
            Views.ExportChoiceDialog Dialog = new Views.ExportChoiceDialog { DataContext = Vm };
            await Dialog.ShowDialog(Owner);
            return Vm.Confirmed ? Vm.SelectedKind : null;
        }

        /// <summary>
        /// Opens a single-file picker with the given title and patterns.
        /// </summary>
        /// <param name="Title">Picker title and file type label.</param>
        /// <param name="Patterns">Glob patterns for allowed file types.</param>
        /// <returns>Selected local path, or null when cancelled.</returns>
        static public async Task<string?> PickOpenFileAsync
            (
            string Title,
            params string[] Patterns
            )
        {
            Window? Owner = GetOwner();
            if (Owner == null)
                return null;

            IReadOnlyList<IStorageFile> Files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Title,
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType(Title) { Patterns = Patterns.ToList() }
                }
            });

            return Files.Count > 0 ? Files[0].Path.LocalPath : null;
        }

        /// <summary>
        /// Opens a save-file picker with suggested name and patterns.
        /// </summary>
        /// <param name="Title">Picker title and file type label.</param>
        /// <param name="SuggestedName">Default file name in the dialog.</param>
        /// <param name="Patterns">Glob patterns for allowed file types.</param>
        /// <returns>Selected local path, or null when cancelled.</returns>
        static public async Task<string?> PickSaveFileAsync
            (
            string Title,
            string SuggestedName,
            params string[] Patterns
            )
        {
            Window? Owner = GetOwner();
            if (Owner == null)
                return null;

            IStorageFile? File = await Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Title,
                SuggestedFileName = SuggestedName,
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType(Title) { Patterns = Patterns.ToList() }
                }
            });

            return File?.Path.LocalPath;
        }
    }
}

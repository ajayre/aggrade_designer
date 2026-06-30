using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Export sub-choice: survey or design (§7.3.3).
    /// </summary>
    public sealed partial class ExportChoiceViewModel : ViewModelBase
    {
        /// <summary>
        /// Supported export formats selected from the dialog.
        /// </summary>
        public enum ExportKind
        {
            None,
            SurveyAgs,
            SurveyMultiplane,
            DesignAgd
        }

        /// <summary>
        /// Export format chosen by the user, or none when cancelled.
        /// </summary>
        [ObservableProperty]
        private ExportKind _SelectedKind = ExportKind.None;

        /// <summary>
        /// Whether survey data is available for export options.
        /// </summary>
        [ObservableProperty]
        private bool _HasSurvey;

        /// <summary>
        /// Whether design data is available for export options.
        /// </summary>
        [ObservableProperty]
        private bool _HasDesign;

        /// <summary>
        /// Whether the user confirmed an export kind selection.
        /// </summary>
        [ObservableProperty]
        private bool _Confirmed;

        /// <summary>
        /// Incremented to signal the view to close the dialog.
        /// </summary>
        [ObservableProperty]
        private int _CloseToken;

        /// <summary>
        /// Selects AGS survey export and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void ExportSurveyAgs
            (
            )
        {
            // Select AGS survey export and close the chooser.
            SelectedKind = ExportKind.SurveyAgs;
            Confirmed = true;
            CloseToken++;
        }

        /// <summary>
        /// Selects multiplane text survey export and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void ExportSurveyTxt
            (
            )
        {
            // Select multiplane text export and close the chooser.
            SelectedKind = ExportKind.SurveyMultiplane;
            Confirmed = true;
            CloseToken++;
        }

        /// <summary>
        /// Selects AGD design export and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void ExportDesign
            (
            )
        {
            // Select AGD design export and close the chooser.
            SelectedKind = ExportKind.DesignAgd;
            Confirmed = true;
            CloseToken++;
        }

        /// <summary>
        /// Cancels export selection and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void Cancel
            (
            )
        {
            // Clear selection so callers treat the dialog as cancelled.
            SelectedKind = ExportKind.None;
            Confirmed = false;
            CloseToken++;
        }
    }
}

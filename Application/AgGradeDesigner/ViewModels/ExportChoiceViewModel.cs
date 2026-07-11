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
            DesignAgd,
            AgControl
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
        /// Helper text explaining why AgGrade Control export is disabled, or empty when enabled.
        /// </summary>
        public string AgControlDisabledHint => HasDesign
            ? string.Empty
            : "AgGrade Control export requires a proposed surface (create a design first).";

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
        /// Notifies dependents when design availability (and the AgControl hint) changes.
        /// </summary>
        /// <param name="Value">New HasDesign value.</param>
        partial void OnHasDesignChanged
            (
            bool Value
            )
        {
            OnPropertyChanged(nameof(AgControlDisabledHint));
        }

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
        /// Selects AgGrade Control <c>.agcontrol</c> export and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void ExportAgControl
            (
            )
        {
            // Select Control SQLite export; enabled only when a proposed surface exists (§7.1).
            SelectedKind = ExportKind.AgControl;
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

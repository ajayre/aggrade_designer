using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Import sub-choice: survey or design (§7.3.3).
    /// </summary>
    public sealed partial class ImportChoiceViewModel : ViewModelBase
    {
        /// <summary>
        /// Kind of data the user chose to import.
        /// </summary>
        public enum ImportKind
        {
            None,
            Survey,
            Design
        }

        /// <summary>
        /// Import kind chosen by the user, or none when cancelled.
        /// </summary>
        [ObservableProperty]
        private ImportKind _SelectedKind = ImportKind.None;

        /// <summary>
        /// Whether the user confirmed an import kind selection.
        /// </summary>
        [ObservableProperty]
        private bool _Confirmed;

        /// <summary>
        /// Incremented to signal the view to close the dialog.
        /// </summary>
        [ObservableProperty]
        private int _CloseToken;

        /// <summary>
        /// Selects survey import and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void ImportSurvey
            (
            )
        {
            // Route the workflow to survey import and close the chooser.
            SelectedKind = ImportKind.Survey;
            Confirmed = true;
            CloseToken++;
        }

        /// <summary>
        /// Selects design import and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void ImportDesign
            (
            )
        {
            // Route the workflow to AGD design import and close the chooser.
            SelectedKind = ImportKind.Design;
            Confirmed = true;
            CloseToken++;
        }

        /// <summary>
        /// Cancels import selection and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void Cancel
            (
            )
        {
            // Clear selection so callers treat the dialog as cancelled.
            SelectedKind = ImportKind.None;
            Confirmed = false;
            CloseToken++;
        }
    }
}

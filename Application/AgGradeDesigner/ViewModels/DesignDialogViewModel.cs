using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Parameters for the design dialog (§5.2).
    /// </summary>
    public sealed partial class DesignDialogViewModel : ViewModelBase
    {
        /// <summary>
        /// Main slope azimuth in degrees (0 = north, 90 = east).
        /// </summary>
        [ObservableProperty]
        private double _MainSlopeDirectionDeg = 90.0;

        /// <summary>
        /// Main slope grade as a percent rise over run.
        /// </summary>
        [ObservableProperty]
        private double _MainSlopePercent;

        /// <summary>
        /// Cross slope grade as a percent rise over run.
        /// </summary>
        [ObservableProperty]
        private double _CrossSlopePercent;

        /// <summary>
        /// Target cut-to-fill volume ratio for the design solver.
        /// </summary>
        [ObservableProperty]
        private double _CutFillRatio = 1.2;

        /// <summary>
        /// Import earthwork volume in cubic yards brought onto the field.
        /// </summary>
        [ObservableProperty]
        private double _ImportToFieldCy;

        /// <summary>
        /// Export earthwork volume in cubic yards removed from the field.
        /// </summary>
        [ObservableProperty]
        private double _ExportFromFieldCy;

        /// <summary>
        /// Whether the user accepted the edited parameters.
        /// </summary>
        [ObservableProperty]
        private bool _Confirmed;

        /// <summary>
        /// Incremented to signal the view to close the dialog.
        /// </summary>
        [ObservableProperty]
        private int _CloseToken;

        /// <summary>
        /// Accepts edited parameters and signals the view to close.
        /// </summary>
        [RelayCommand]
        private void Ok
            (
            )
        {
            // Record acceptance before bumping the close token for the view.
            Confirmed = true;
            CloseToken++;
        }

        /// <summary>
        /// Discards edits and signals the view to close.
        /// </summary>
        [RelayCommand]
        private void Cancel
            (
            )
        {
            // Record cancellation before bumping the close token for the view.
            Confirmed = false;
            CloseToken++;
        }

        /// <summary>
        /// Builds core design parameters from the current dialog fields.
        /// </summary>
        /// <returns>Design parameters snapshot.</returns>
        public AgGradeDesigner.Core.Models.DesignParameters ToParameters
            (
            )
        {
            // Snapshot dialog fields into the core parameter model for persistence and design runs.
            return new AgGradeDesigner.Core.Models.DesignParameters
            {
                MainSlopeDirectionDeg = MainSlopeDirectionDeg,
                MainSlopePercent = MainSlopePercent,
                CrossSlopePercent = CrossSlopePercent,
                CutFillRatio = CutFillRatio,
                ImportToFieldCy = ImportToFieldCy,
                ExportFromFieldCy = ExportFromFieldCy
            };
        }

        /// <summary>
        /// Pre-fills dialog fields from existing design parameters.
        /// </summary>
        /// <param name="Parameters">Parameters to copy, or null to leave defaults.</param>
        public void LoadFrom
            (
            AgGradeDesigner.Core.Models.DesignParameters? Parameters
            )
        {
            if (Parameters == null)
                return;

            // Copy each editable slope and volume field from the persisted parameter bag.
            MainSlopeDirectionDeg = Parameters.MainSlopeDirectionDeg;
            MainSlopePercent = Parameters.MainSlopePercent;
            CrossSlopePercent = Parameters.CrossSlopePercent;
            CutFillRatio = Parameters.CutFillRatio;
            ImportToFieldCy = Parameters.ImportToFieldCy;
            ExportFromFieldCy = Parameters.ExportFromFieldCy;
        }
    }
}

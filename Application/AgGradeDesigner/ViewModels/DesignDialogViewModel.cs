using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Units;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Parameters for the design dialog (§5.2).
    /// </summary>
    public sealed partial class DesignDialogViewModel : ViewModelBase
    {
        /// <summary>Display unit system used for volume field labels and values.</summary>
        private DisplayUnitSystem _DisplayUnitSystem = DisplayUnitSystem.Imperial;

        /// <summary>
        /// Label for the import volume field including the active unit abbreviation.
        /// </summary>
        public string ImportVolumeLabel => DisplayUnitFormatter.DesignVolumeFieldLabel(true, _DisplayUnitSystem);

        /// <summary>
        /// Label for the export volume field including the active unit abbreviation.
        /// </summary>
        public string ExportVolumeLabel => DisplayUnitFormatter.DesignVolumeFieldLabel(false, _DisplayUnitSystem);
        /// <summary>
        /// Main slope azimuth in degrees (0 = north, 90 = east).
        /// </summary>
        [ObservableProperty]
        private double _MainSlopeDirectionDeg = DesignParameters.DEFAULT_MAIN_SLOPE_DIRECTION_DEG;

        /// <summary>
        /// Main slope grade as a percent rise over run.
        /// </summary>
        [ObservableProperty]
        private double _MainSlopePercent = DesignParameters.DEFAULT_MAIN_SLOPE_PERCENT;

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
        /// When true, proposed surface matches existing topology and slope/volume fields are ignored.
        /// </summary>
        [ObservableProperty]
        private bool _UseExistingTopology;

        /// <summary>
        /// Whether slope, cut/fill ratio, and import/export fields are editable.
        /// </summary>
        public bool AreDesignParametersEnabled => !UseExistingTopology;

        /// <summary>
        /// Refreshes dependent enablement when the existing-topology toggle changes.
        /// </summary>
        /// <param name="value">New toggle state.</param>
        partial void OnUseExistingTopologyChanged
            (
            bool value
            )
        {
            OnPropertyChanged(nameof(AreDesignParametersEnabled));
        }

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
                ImportToFieldCy = DisplayUnitFormatter.DisplayVolumeToCubicYards(ImportToFieldCy, _DisplayUnitSystem),
                ExportFromFieldCy = DisplayUnitFormatter.DisplayVolumeToCubicYards(ExportFromFieldCy, _DisplayUnitSystem),
                UseExistingTopology = UseExistingTopology
            };
        }

        /// <summary>
        /// Pre-fills dialog fields from existing design parameters.
        /// </summary>
        /// <param name="Parameters">Parameters to copy, or null to leave defaults.</param>
        /// <param name="DisplayUnits">Display unit system for volume labels and values.</param>
        public void LoadFrom
            (
            AgGradeDesigner.Core.Models.DesignParameters? Parameters,
            DisplayUnitSystem DisplayUnits = DisplayUnitSystem.Imperial
            )
        {
            _DisplayUnitSystem = DisplayUnits;
            OnPropertyChanged(nameof(ImportVolumeLabel));
            OnPropertyChanged(nameof(ExportVolumeLabel));

            if (Parameters == null)
                return;

            // Copy each editable slope and volume field from the persisted parameter bag.
            MainSlopeDirectionDeg = Parameters.MainSlopeDirectionDeg;
            MainSlopePercent = Parameters.MainSlopePercent;
            CrossSlopePercent = Parameters.CrossSlopePercent;
            CutFillRatio = Parameters.CutFillRatio;
            ImportToFieldCy = DisplayUnitFormatter.CubicYardsToDisplayVolume(Parameters.ImportToFieldCy, _DisplayUnitSystem);
            ExportFromFieldCy = DisplayUnitFormatter.CubicYardsToDisplayVolume(Parameters.ExportFromFieldCy, _DisplayUnitSystem);
            UseExistingTopology = Parameters.UseExistingTopology;
        }
    }
}

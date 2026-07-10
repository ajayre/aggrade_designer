using AgGradeDesigner.Core.Units;
using AgGradeDesigner.Core.Water;
using AgGradeDesigner.Services;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// View model for the Water dialog (WATER-SPEC §7.1).
    /// </summary>
    public sealed partial class WaterDialogViewModel : ViewModelBase
    {
        /// <summary>Working copy of water settings edited in the dialog session.</summary>
        private WaterSettings _SessionSettings = new WaterSettings();

        /// <summary>Display unit system used for rainfall labels and conversions.</summary>
        private DisplayUnitSystem _DisplayUnitSystem;

        /// <summary>
        /// Initializes water settings bindings from the active project.
        /// </summary>
        /// <param name="ProjectSettings">Project water settings to edit.</param>
        /// <param name="DisplayUnitSystem">Active display unit system.</param>
        public WaterDialogViewModel
            (
            WaterSettings ProjectSettings,
            DisplayUnitSystem DisplayUnitSystem
            )
        {
            _DisplayUnitSystem = DisplayUnitSystem;
            _SessionSettings = ProjectSettings.Clone();
            ApplySessionToBindings();
        }

        /// <summary>True when the user clicked OK.</summary>
        [ObservableProperty]
        private bool _Confirmed;

        /// <summary>Incremented to request dialog close from the view.</summary>
        [ObservableProperty]
        private int _CloseToken;

        /// <summary>Ponding rainfall shown in display units.</summary>
        [ObservableProperty]
        private string _PondingRainfallDisplay = "0";

        /// <summary>Ponding curve number shown as a whole number.</summary>
        [ObservableProperty]
        private string _PondingCurveNumberDisplay = "0";

        /// <summary>Minimum pond depth shown in display units.</summary>
        [ObservableProperty]
        private string _MinimumPondDepthDisplay = "0";

        /// <summary>Unit suffix for ponding rainfall labels.</summary>
        public string RainfallUnitSuffix => WaterDisplayUnitConverter.RainfallUnitSuffix(_DisplayUnitSystem);

        /// <summary>Unit suffix for minimum pond depth labels.</summary>
        public string DepthUnitSuffix => WaterDisplayUnitConverter.DepthUnitSuffix(_DisplayUnitSystem);

        /// <summary>
        /// Loads dialog controls from the active project's water settings.
        /// </summary>
        /// <param name="ProjectSettings">Project water settings.</param>
        /// <param name="DisplayUnitSystem">Active display unit system.</param>
        public void LoadFrom
            (
            WaterSettings ProjectSettings,
            DisplayUnitSystem DisplayUnitSystem
            )
        {
            _DisplayUnitSystem = DisplayUnitSystem;
            _SessionSettings = ProjectSettings.Clone();
            ApplySessionToBindings();
        }

        /// <summary>
        /// Returns accepted water settings after successful validation.
        /// </summary>
        /// <returns>Canonical water settings written to the project on OK.</returns>
        public WaterSettings ToWaterSettings
            (
            )
        {
            return _SessionSettings.Clone();
        }

        /// <summary>
        /// Validates inputs and closes the dialog with accepted settings.
        /// </summary>
        [RelayCommand]
        private void Ok
            (
            )
        {
            if (!TryReadAndValidateInputs(out string ErrorMessage))
            {
                _ = DialogService.ShowMessageAsync("Water settings", ErrorMessage);
                return;
            }

            Confirmed = true;
            CloseToken++;
        }

        /// <summary>
        /// Closes the dialog without applying session edits.
        /// </summary>
        [RelayCommand]
        private void Cancel
            (
            )
        {
            Confirmed = false;
            CloseToken++;
        }

        /// <summary>
        /// Copies session settings into bound display strings.
        /// </summary>
        private void ApplySessionToBindings
            (
            )
        {
            uint DisplayRainfall = WaterDisplayUnitConverter.ToDisplayRainfall(
                _SessionSettings.PondingRainfallMm,
                _DisplayUnitSystem);
            PondingRainfallDisplay = DisplayRainfall.ToString(System.Globalization.CultureInfo.InvariantCulture);
            PondingCurveNumberDisplay = _SessionSettings.PondingCurveNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
            uint DisplayDepth = WaterDisplayUnitConverter.ToDisplayDepth(
                _SessionSettings.MinimumPondDepthMm,
                _DisplayUnitSystem);
            MinimumPondDepthDisplay = DisplayDepth.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses display controls and validates unsigned integer inputs.
        /// </summary>
        /// <param name="ErrorMessage">Validation message when parsing fails.</param>
        /// <returns>True when both controls contain valid whole numbers.</returns>
        private bool TryReadAndValidateInputs
            (
            out string ErrorMessage
            )
        {
            ErrorMessage = string.Empty;
            if (!uint.TryParse(PondingRainfallDisplay.Trim(), out uint DisplayRainfall))
            {
                ErrorMessage = "Ponding rainfall must be a whole number greater than or equal to zero.";
                return false;
            }

            if (!uint.TryParse(PondingCurveNumberDisplay.Trim(), out uint CurveNumber))
            {
                ErrorMessage = "Ponding curve number must be a whole number greater than or equal to zero.";
                return false;
            }

            if (!uint.TryParse(MinimumPondDepthDisplay.Trim(), out uint DisplayDepth))
            {
                ErrorMessage = "Minimum pond depth must be a whole number greater than or equal to zero.";
                return false;
            }

            _SessionSettings.PondingRainfallMm = WaterDisplayUnitConverter.FromDisplayRainfall(DisplayRainfall, _DisplayUnitSystem);
            _SessionSettings.PondingCurveNumber = CurveNumber;
            _SessionSettings.MinimumPondDepthMm = WaterDisplayUnitConverter.FromDisplayDepth(DisplayDepth, _DisplayUnitSystem);
            return true;
        }
    }
}

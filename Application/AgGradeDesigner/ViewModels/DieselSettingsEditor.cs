using AgGradeDesigner.Core.Haul;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Editable diesel price for the settings dialog.
    /// The price stays in dollars per gallon. An empty box leaves it unset.
    /// </summary>
    public sealed partial class DieselSettingsEditor : ViewModelBase
    {
        /// <summary>
        /// Price in dollars per gallon, independent of the text box.
        /// </summary>
        private double? _PricePerGallon;

        /// <summary>
        /// True while the text box is being filled from <see cref="_PricePerGallon"/>.
        /// </summary>
        private bool _SuppressWrite;

        /// <summary>
        /// True after the user edits the box. OK then reads the box.
        /// An untouched section keeps the stored value.
        /// </summary>
        private bool _Edited;

        /// <summary>
        /// Diesel price text. Empty means the price is unset.
        /// </summary>
        [ObservableProperty]
        private string _PricePerGallonText = string.Empty;

        /// <summary>
        /// Loads the diesel price into the text box.
        /// </summary>
        /// <param name="Source">Project or user haul settings.</param>
        public void Load
            (
            HaulSettings Source
            )
        {
            _PricePerGallon = Source.PricePerGallon;
            _Edited = false;
            PushToDisplay();
        }

        /// <summary>
        /// Writes the text box onto a haul settings instance.
        /// Only the diesel price is changed.
        /// </summary>
        /// <param name="Target">Project or user settings to update.</param>
        public void ApplyTo
            (
            HaulSettings Target
            )
        {
            if (_Edited)
                PullFromDisplay();

            Target.PricePerGallon = _PricePerGallon;
        }

        /// <summary>
        /// Records a user edit of the price box.
        /// </summary>
        /// <param name="Value">New price text.</param>
        partial void OnPricePerGallonTextChanged
            (
            string Value
            )
        {
            if (!_SuppressWrite)
                _Edited = true;
        }

        /// <summary>
        /// Copies the stored price into the text box.
        /// </summary>
        private void PushToDisplay
            (
            )
        {
            _SuppressWrite = true;
            PricePerGallonText = DieselInputFormat.Format(_PricePerGallon);
            _SuppressWrite = false;
        }

        /// <summary>
        /// Copies the text box into the stored price.
        /// </summary>
        private void PullFromDisplay
            (
            )
        {
            if (_SuppressWrite)
                return;

            _PricePerGallon = DieselInputFormat.Parse(PricePerGallonText, _PricePerGallon);
        }
    }
}

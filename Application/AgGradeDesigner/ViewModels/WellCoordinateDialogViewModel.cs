using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AgGradeDesigner.Helpers;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Latitude and longitude prompt for placing a well without clicking the map.
    /// </summary>
    public sealed partial class WellCoordinateDialogViewModel : ViewModelBase
    {
        /// <summary>Returns true when the coordinate is inside the survey.</summary>
        private readonly Func<double, double, bool> _IsInsideSurvey;

        /// <summary>Words above the latitude and longitude boxes.</summary>
        public string Instruction { get; }

        /// <summary>Default prompt when the dialog is placing a well.</summary>
        public const string DEFAULT_INSTRUCTION = "Enter the well location in decimal degrees.";

        /// <summary>
        /// Creates the prompt.
        /// </summary>
        /// <param name="IsInsideSurvey">Returns true when a parsed coordinate may be placed.</param>
        /// <param name="Instruction">Prompt text. A well prompt is used when this is omitted.</param>
        public WellCoordinateDialogViewModel
            (
            Func<double, double, bool> IsInsideSurvey,
            string? Instruction = null
            )
        {
            _IsInsideSurvey = IsInsideSurvey;
            this.Instruction = string.IsNullOrWhiteSpace(Instruction) ? DEFAULT_INSTRUCTION : Instruction;
        }

        /// <summary>Latitude typed by the user, in decimal degrees.</summary>
        [ObservableProperty]
        private string _LatitudeText = "";

        /// <summary>Longitude typed by the user, in decimal degrees.</summary>
        [ObservableProperty]
        private string _LongitudeText = "";

        /// <summary>Why Add was refused. Empty when the boxes are acceptable.</summary>
        [ObservableProperty]
        private string _ErrorText = "";

        /// <summary>True when <see cref="ErrorText"/> should be shown.</summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorText);

        /// <summary>True when Add accepted a location. Cancel leaves this false.</summary>
        [ObservableProperty]
        private bool _Confirmed;

        /// <summary>Latitude accepted by Add, in decimal degrees.</summary>
        public double Latitude { get; private set; }

        /// <summary>Longitude accepted by Add, in decimal degrees.</summary>
        public double Longitude { get; private set; }

        /// <summary>
        /// Incremented to signal the view to close the dialog.
        /// </summary>
        [ObservableProperty]
        private int _CloseToken;

        /// <summary>
        /// Shows or hides the error line when the message changes.
        /// </summary>
        /// <param name="Value">New error message.</param>
        partial void OnErrorTextChanged
            (
            string Value
            )
        {
            OnPropertyChanged(nameof(HasError));
        }

        /// <summary>
        /// Places the well when both coordinates are legal and inside the survey.
        /// An invalid entry stays in the dialog so the request is not lost.
        /// </summary>
        [RelayCommand]
        private void Add
            (
            )
        {
            if (!CoordinateFormat.TryParseLatitude(LatitudeText, out double Latitude))
            {
                ErrorText = RangeMessage("latitude", CoordinateFormat.MIN_LATITUDE_DEG, CoordinateFormat.MAX_LATITUDE_DEG);
                return;
            }

            if (!CoordinateFormat.TryParseLongitude(LongitudeText, out double Longitude))
            {
                ErrorText = RangeMessage("longitude", CoordinateFormat.MIN_LONGITUDE_DEG, CoordinateFormat.MAX_LONGITUDE_DEG);
                return;
            }

            // A coordinate off the survey would not be a map click, so Add refuses it.
            if (!_IsInsideSurvey(Latitude, Longitude))
            {
                ErrorText = "That location is outside the survey.";
                return;
            }

            this.Latitude = Latitude;
            this.Longitude = Longitude;
            ErrorText = "";
            Confirmed = true;
            CloseToken++;
        }

        /// <summary>
        /// Closes the dialog without a location.
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
        /// Builds the message shown when a coordinate is missing or out of range.
        /// </summary>
        /// <param name="Name">Latitude or longitude.</param>
        /// <param name="Minimum">Lowest accepted value.</param>
        /// <param name="Maximum">Highest accepted value.</param>
        /// <returns>Error text for the dialog.</returns>
        static private string RangeMessage
            (
            string Name,
            double Minimum,
            double Maximum
            )
        {
            return "Enter a " + Name + " between " +
                Minimum.ToString(CultureInfo.InvariantCulture) + " and " +
                Maximum.ToString(CultureInfo.InvariantCulture) + ".";
        }
    }
}

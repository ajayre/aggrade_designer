using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AgGradeDesigner.Helpers;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// One reference-point row in the Features palette.
    /// </summary>
    public sealed partial class ReferencePointRowViewModel : ViewModelBase
    {
        /// <summary>Called when the user edits the name.</summary>
        private readonly Action<string> _NameEdited;

        /// <summary>Called when the user edits the latitude or longitude text.</summary>
        private readonly Action<string, string> _LocationEdited;

        /// <summary>Called when the user clicks Edit for this point.</summary>
        private readonly Action<string> _Edit;

        /// <summary>Called when the user confirms Delete for this point.</summary>
        private readonly Func<string, Task> _Delete;

        /// <summary>True while the view model is assigning the name, so the write-back is skipped.</summary>
        private bool _SuppressNameWrite;

        /// <summary>True while the view model is assigning latitude or longitude, so the write-back is skipped.</summary>
        private bool _SuppressLocationWrite;

        /// <summary>
        /// Creates a reference-point row.
        /// </summary>
        /// <param name="Id">Committed id, or a draft token.</param>
        /// <param name="NameText">Name shown beside the marker.</param>
        /// <param name="CoordinateText">Latitude and longitude label for a draft row.</param>
        /// <param name="LatitudeText">Latitude in decimal degrees.</param>
        /// <param name="LongitudeText">Longitude in decimal degrees.</param>
        /// <param name="ShowEditButton">True when Edit should be shown.</param>
        /// <param name="IsLiveEdit">True when this is the reference point being edited. The row is highlighted.</param>
        /// <param name="IsAlternate">True for the second, fourth, and later stripe.</param>
        /// <param name="CanEdit">True only while this point itself is being edited.</param>
        /// <param name="ShowSettings">True when the name and coordinates are listed as editors.</param>
        /// <param name="Edit">Edit command handler.</param>
        /// <param name="Delete">Delete command handler. Runs after the user confirms.</param>
        /// <param name="NameEdited">Name text handler.</param>
        /// <param name="LocationEdited">Latitude and longitude text handler.</param>
        public ReferencePointRowViewModel
            (
            string Id,
            string NameText,
            string CoordinateText,
            string LatitudeText,
            string LongitudeText,
            bool ShowEditButton,
            bool IsLiveEdit,
            bool IsAlternate,
            bool CanEdit,
            bool ShowSettings,
            Action<string> Edit,
            Func<string, Task> Delete,
            Action<string> NameEdited,
            Action<string, string> LocationEdited
            )
        {
            this.Id = Id;
            this.ShowEditButton = ShowEditButton;
            this.IsLiveEdit = IsLiveEdit;
            this.IsAlternate = IsAlternate;
            this.CanEdit = CanEdit;
            this.ShowSettings = ShowSettings;
            _CoordinateText = CoordinateText;
            _Edit = Edit;
            _Delete = Delete;
            _NameEdited = NameEdited;
            _LocationEdited = LocationEdited;
            _SuppressNameWrite = true;
            _SuppressLocationWrite = true;
            this.NameText = NameText;
            this.LatitudeText = LatitudeText;
            this.LongitudeText = LongitudeText;
            _SuppressNameWrite = false;
            _SuppressLocationWrite = false;
        }

        /// <summary>Committed id, or a draft token.</summary>
        public string Id { get; }

        /// <summary>True when the Edit button is shown.</summary>
        public bool ShowEditButton { get; }

        /// <summary>True when this is the reference point being edited. The row is highlighted, and map drags refresh its coordinates.</summary>
        public bool IsLiveEdit { get; }

        /// <summary>True when this row uses the alternate list stripe.</summary>
        public bool IsAlternate { get; }

        /// <summary>True when the name, latitude, and longitude accept typing.</summary>
        public bool CanEdit { get; }

        /// <summary>True when the name and coordinates are editors. A point being added uses the header name instead.</summary>
        public bool ShowSettings { get; }

        /// <summary>Latitude and longitude label for a point that is still being added.</summary>
        [ObservableProperty]
        private string _CoordinateText;

        /// <summary>Name shown beside the marker.</summary>
        [ObservableProperty]
        private string _NameText;

        /// <summary>Latitude in decimal degrees.</summary>
        [ObservableProperty]
        private string _LatitudeText;

        /// <summary>Longitude in decimal degrees.</summary>
        [ObservableProperty]
        private string _LongitudeText;

        /// <summary>
        /// Writes a user-edited name back to the point being edited.
        /// </summary>
        /// <param name="Value">New text box contents.</param>
        partial void OnNameTextChanged
            (
            string Value
            )
        {
            if (_SuppressNameWrite || !CanEdit)
                return;

            _NameEdited(Value);
        }

        /// <summary>
        /// Writes a user-edited latitude back to the point being edited.
        /// </summary>
        /// <param name="Value">New text box contents.</param>
        partial void OnLatitudeTextChanged
            (
            string Value
            )
        {
            PublishLocation();
        }

        /// <summary>
        /// Writes a user-edited longitude back to the point being edited.
        /// </summary>
        /// <param name="Value">New text box contents.</param>
        partial void OnLongitudeTextChanged
            (
            string Value
            )
        {
            PublishLocation();
        }

        /// <summary>
        /// Shows a latitude and longitude chosen by a map drag without treating that as typing.
        /// Text that already rounds to the same coordinate is left alone so typing is not rewritten.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        public void SetLocation
            (
            double Latitude,
            double Longitude
            )
        {
            _SuppressLocationWrite = true;
            string NextLatitude = CoordinateFormat.FormatLatitude(Latitude);
            string NextLongitude = CoordinateFormat.FormatLongitude(Longitude);
            if (!ShowsSameCoordinate(LatitudeText, NextLatitude, true))
                LatitudeText = NextLatitude;
            if (!ShowsSameCoordinate(LongitudeText, NextLongitude, false))
                LongitudeText = NextLongitude;
            _SuppressLocationWrite = false;
        }

        /// <summary>
        /// Sends the current latitude and longitude text when the user typed it.
        /// </summary>
        private void PublishLocation
            (
            )
        {
            if (_SuppressLocationWrite || !CanEdit)
                return;

            _LocationEdited(LatitudeText, LongitudeText);
        }

        /// <summary>
        /// True when the box already shows the formatted coordinate, even with fewer decimal places.
        /// </summary>
        /// <param name="Current">Text currently in the box.</param>
        /// <param name="Formatted">Text the map location would show.</param>
        /// <param name="Latitude">True to parse a latitude. False parses a longitude.</param>
        /// <returns>True when both texts are the same coordinate at display precision.</returns>
        static private bool ShowsSameCoordinate
            (
            string Current,
            string Formatted,
            bool Latitude
            )
        {
            if (string.Equals(Current, Formatted, StringComparison.Ordinal))
                return true;

            bool CurrentParsed;
            double Typed;
            bool FormattedParsed;
            double Shown;
            if (Latitude)
            {
                CurrentParsed = CoordinateFormat.TryParseLatitude(Current, out Typed);
                FormattedParsed = CoordinateFormat.TryParseLatitude(Formatted, out Shown);
            }
            else
            {
                CurrentParsed = CoordinateFormat.TryParseLongitude(Current, out Typed);
                FormattedParsed = CoordinateFormat.TryParseLongitude(Formatted, out Shown);
            }

            if (!CurrentParsed || !FormattedParsed)
                return false;

            return CoordinateFormat.RoundCoordinate(Typed) == CoordinateFormat.RoundCoordinate(Shown);
        }

        /// <summary>
        /// Starts edit mode for this reference point.
        /// </summary>
        [RelayCommand]
        private void Edit
            (
            )
        {
            _Edit(Id);
        }

        /// <summary>
        /// Deletes this reference point after the palette asks for confirmation.
        /// </summary>
        [RelayCommand]
        private Task Delete
            (
            )
        {
            return _Delete(Id);
        }
    }
}

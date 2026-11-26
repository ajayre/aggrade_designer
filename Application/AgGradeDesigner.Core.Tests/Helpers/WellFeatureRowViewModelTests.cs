using AgGradeDesigner.ViewModels;

namespace AgGradeDesigner.Core.Tests.Helpers
{
    /// <summary>
    /// Tests for editing a saved well's latitude and longitude from its row.
    /// </summary>
    public sealed class WellFeatureRowViewModelTests
    {
        /// <summary>Latitude used for the row.</summary>
        private const double SAMPLE_LATITUDE = 40.1;

        /// <summary>Longitude used for the row.</summary>
        private const double SAMPLE_LONGITUDE = -93.4;

        /// <summary>Latitude a typed edit moves the well to.</summary>
        private const double EDITED_LATITUDE = 40.2;

        /// <summary>
        /// Typing is reported only while this well is being edited, and a map move does not rewrite an equivalent value.
        /// </summary>
        [Fact]
        public void LocationText_ReportsEditsAndKeepsEquivalentTyping
            (
            )
        {
            int Reports = 0;
            string ReportedLatitude = "";
            string ReportedLongitude = "";
            WellFeatureRowViewModel Row = CreateRow(
                CanEditSize: true,
                LatitudeText: "40.1",
                LongitudeText: "-93.4",
                LocationEdited: (Latitude, Longitude) =>
                {
                    Reports++;
                    ReportedLatitude = Latitude;
                    ReportedLongitude = Longitude;
                });

            Row.SetLocation(SAMPLE_LATITUDE, SAMPLE_LONGITUDE);
            Assert.Equal("40.1", Row.LatitudeText);
            Assert.Equal(0, Reports);

            Row.LatitudeText = "40.2";
            Assert.Equal(1, Reports);
            Assert.Equal("40.2", ReportedLatitude);
            Assert.Equal("-93.4", ReportedLongitude);

            WellFeatureRowViewModel Locked = CreateRow(
                CanEditSize: false,
                LatitudeText: "40.100000",
                LongitudeText: "-93.400000",
                LocationEdited: (_, _) => Reports++);
            Locked.LatitudeText = EDITED_LATITUDE.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(1, Reports);
        }

        /// <summary>
        /// Builds one well row with the location callback under test.
        /// </summary>
        /// <param name="CanEditSize">True when the row is the well being edited.</param>
        /// <param name="LatitudeText">Starting latitude text.</param>
        /// <param name="LongitudeText">Starting longitude text.</param>
        /// <param name="LocationEdited">Called when the user types a coordinate.</param>
        /// <returns>The row.</returns>
        static private WellFeatureRowViewModel CreateRow
            (
            bool CanEditSize,
            string LatitudeText,
            string LongitudeText,
            Action<string, string> LocationEdited
            )
        {
            return new WellFeatureRowViewModel(
                "well_1",
                LatitudeText + ", " + LongitudeText,
                LatitudeText,
                LongitudeText,
                "90",
                "ft",
                ShowEditButton: !CanEditSize,
                IsLiveEdit: CanEditSize,
                IsAlternate: false,
                CanEditSize: CanEditSize,
                ShowDiameter: true,
                Edit: _ => { },
                Delete: _ => Task.CompletedTask,
                DiameterEdited: _ => { },
                LocationEdited: LocationEdited);
        }
    }
}

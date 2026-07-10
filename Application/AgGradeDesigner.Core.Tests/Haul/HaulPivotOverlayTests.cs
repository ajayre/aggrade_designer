using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Unit tests for center-pivot overlay bearing resolution and field-fit radius.
    /// </summary>
    public sealed class HaulPivotOverlayTests
    {
        /// <summary>
        /// Verifies the unset 0,0 pivot location sentinel is detected.
        /// </summary>
        [Fact]
        public void IsUnsetPivotLocation_DetectsZeroZero
            (
            )
        {
            Assert.True(HaulPivotOverlay.IsUnsetPivotLocation(0.0, 0.0));
            Assert.False(HaulPivotOverlay.IsUnsetPivotLocation(40.0, -100.0));
        }

        /// <summary>
        /// Verifies pivot drawing is suppressed when the centre is the 0,0 sentinel.
        /// </summary>
        [Fact]
        public void ShouldDraw_ReturnsFalseForUnsetLocation
            (
            )
        {
            HaulSettings Settings = new HaulSettings
            {
                Pivot = true,
                PivotLatitudeDeg = 0.0,
                PivotLongitudeDeg = 0.0
            };
            HaulData Data = new HaulData
            {
                Payload =
                {
                    PivotOverlay = new HaulPivotOverlayDto
                    {
                        RadiusM = 100.0,
                        BearingByPassDeg = [0.0, 45.0]
                    }
                }
            };

            Assert.False(HaulPivotOverlay.ShouldDraw(Settings, Data));
        }

        /// <summary>
        /// Verifies scrub index selects the matching held bearing from the pivot schedule.
        /// </summary>
        [Fact]
        public void TryResolveBearingDeg_UsesScrubIndex
            (
            )
        {
            HaulPivotOverlayDto Overlay = new HaulPivotOverlayDto
            {
                RadiusM = 50.0,
                BearingByPassDeg = [10.0, 95.0, 180.0]
            };

            Assert.True(HaulPivotOverlay.TryResolveBearingDeg(Overlay, 1, out double BearingDeg));
            Assert.Equal(95.0, BearingDeg, precision: 6);
        }

        /// <summary>
        /// Verifies bearings wrap into the [0, 360) compass range.
        /// </summary>
        [Fact]
        public void NormalizeBearingDeg_WrapsNegativeAndOver360
            (
            )
        {
            Assert.Equal(350.0, HaulPivotOverlay.NormalizeBearingDeg(-10.0), precision: 6);
            Assert.Equal(20.0, HaulPivotOverlay.NormalizeBearingDeg(380.0), precision: 6);
        }

        /// <summary>
        /// Verifies the inscribed radius equals the distance to the nearest square side.
        /// </summary>
        [Fact]
        public void ComputeInscribedRadiusM_FitsInsideSquareField
            (
            )
        {
            // Rough 200 m square around a mid-latitude point using degree offsets.
            const double CenterLat = 40.0;
            const double CenterLon = -100.0;
            const double HalfSideDeg = 0.001;
            List<(double Lat, double Lon)> Boundary =
            [
                (CenterLat - HalfSideDeg, CenterLon - HalfSideDeg),
                (CenterLat - HalfSideDeg, CenterLon + HalfSideDeg),
                (CenterLat + HalfSideDeg, CenterLon + HalfSideDeg),
                (CenterLat + HalfSideDeg, CenterLon - HalfSideDeg)
            ];

            double RadiusM = HaulPivotOverlay.ComputeInscribedRadiusM(CenterLat, CenterLon, Boundary);

            Assert.True(RadiusM > 50.0);
            Assert.True(RadiusM < 150.0);
        }

        /// <summary>
        /// Verifies a north bearing tip lies north of the pivot centre.
        /// </summary>
        [Fact]
        public void TipLatLon_NorthBearingIncreasesLatitude
            (
            )
        {
            const double CenterLat = 40.0;
            const double CenterLon = -100.0;
            HaulPivotOverlay.TipLatLon(CenterLat, CenterLon, 100.0, 0.0, out double TipLat, out double TipLon);

            Assert.True(TipLat > CenterLat);
            Assert.True(Math.Abs(TipLon - CenterLon) < 0.001);
        }

        /// <summary>
        /// Verifies pivot angle labels include a degree suffix.
        /// </summary>
        [Fact]
        public void FormatBearingLabel_IncludesDegreeSuffix
            (
            )
        {
            Assert.Equal("45.0°", HaulPivotOverlay.FormatBearingLabel(45.0));
        }

        /// <summary>
        /// Verifies haul payload JSON round-trips pivot overlay fields used after project reload.
        /// </summary>
        [Fact]
        public void HaulDataSerializer_RoundTripsPivotOverlay
            (
            )
        {
            HaulPersistedPayload Payload = new HaulPersistedPayload
            {
                OrderedPasses = [new HaulPassDto { LoadBcy = 1.0 }],
                PivotOverlay = new HaulPivotOverlayDto
                {
                    RadiusM = 50.0,
                    BearingByPassDeg = [0.0, 45.0],
                    Moves =
                    [
                        new HaulPivotMoveDto
                        {
                            Index = 1,
                            FirstRoute = 1,
                            LastRoute = 1,
                            BearingDeg = 0.0,
                            MoveDeg = 0.0
                        },
                        new HaulPivotMoveDto
                        {
                            Index = 2,
                            FirstRoute = 2,
                            LastRoute = 2,
                            BearingDeg = 45.0,
                            MoveDeg = 45.0
                        }
                    ]
                }
            };

            string Json = HaulDataSerializer.SerializePayload(Payload);
            HaulPersistedPayload? Restored = HaulDataSerializer.DeserializePayload(Json);

            Assert.NotNull(Restored);
            Assert.NotNull(Restored.PivotOverlay);
            Assert.Equal(50.0, Restored.PivotOverlay.RadiusM, precision: 6);
            Assert.Equal(new[] { 0.0, 45.0 }, Restored.PivotOverlay.BearingByPassDeg);
            Assert.Equal(2, Restored.PivotOverlay.Moves.Count);
            Assert.Equal(2, Restored.PivotOverlay.Moves[1].FirstRoute);
            Assert.Equal(45.0, Restored.PivotOverlay.Moves[1].MoveDeg, precision: 6);
        }

        /// <summary>
        /// Verifies pivot-centre validation passes when pivot mode is disabled.
        /// </summary>
        [Fact]
        public void TryValidatePivotCenterForRun_AllowsWhenPivotDisabled
            (
            )
        {
            HaulSettings Settings = new HaulSettings
            {
                Pivot = false,
                PivotLatitudeDeg = 0.0,
                PivotLongitudeDeg = 0.0
            };

            Assert.True(HaulPivotOverlay.TryValidatePivotCenterForRun(Settings, Project: null, out string Error));
            Assert.Equal(string.Empty, Error);
        }

        /// <summary>
        /// Verifies pivot-centre validation fails for a centre outside the field when pivot is enabled.
        /// </summary>
        [Fact]
        public void TryValidatePivotCenterForRun_RejectsOutsideField
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings
            {
                Pivot = true,
                PivotLatitudeDeg = 0.0,
                PivotLongitudeDeg = 0.0
            };

            Assert.False(HaulPivotOverlay.TryValidatePivotCenterForRun(Settings, Project, out string Error));
            Assert.Contains("inside the field", Error, StringComparison.OrdinalIgnoreCase);
        }
    }
}

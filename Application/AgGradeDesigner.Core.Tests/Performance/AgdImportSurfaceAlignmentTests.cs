using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Verifies AGD surface alignment with the 3D satellite datum and survey boundary.
    /// </summary>
    public sealed class AgdImportSurfaceAlignmentTests
    {
        /// <summary>
        /// The rasterized surface minimum must appear on a valid cell so the 3D mesh meets the satellite plane.
        /// </summary>
        [Fact]
        public void FakeField01_AgdImport_MinElevationExistsOnValidRaster
            (
            )
        {
            FieldSurfaceSampler Surface = BuildFakeField01AgdSurface();
            double MinOnRaster = double.MaxValue;

            for (int Idx = 0; Idx < Surface.Valid.Length; Idx++)
            {
                if (!Surface.Valid[Idx])
                    continue;

                MinOnRaster = Math.Min(MinOnRaster, Surface.Existing[Idx]);
            }

            Assert.True(Math.Abs(MinOnRaster - Surface.MinElevation) < 1e-6,
                $"MinElevation {Surface.MinElevation:F6} does not match valid raster minimum {MinOnRaster:F6}.");
        }

        /// <summary>
        /// Expanded AGD surfaces must not render outside the imported 2PER boundary polygon.
        /// </summary>
        [Fact]
        public void FakeField01_AgdImport_SurfaceStaysInsideBoundary
            (
            )
        {
            AgdImportResult Import = AgdService.Import(TestDataPaths.FakeField01Agd);
            FieldSurfaceSampler Surface = BuildFakeField01AgdSurface();
            List<(double Lat, double Lon)> Boundary = Import.Survey.BoundaryPoints
                .ConvertAll(Pt => (Pt.Latitude, Pt.Longitude));
            Boundary = BoundaryMask.OrderBoundaryLatLon(Boundary);

            int OutsideCount = 0;
            for (int Sy = 0; Sy < Surface.SampleHeight; Sy++)
            {
                double Lat = Surface.MaxLat -
                    (Surface.MaxLat - Surface.MinLat) * Sy / Math.Max(1, Surface.SampleHeight - 1);
                for (int Sx = 0; Sx < Surface.SampleWidth; Sx++)
                {
                    int Idx = Sy * Surface.SampleWidth + Sx;
                    if (!Surface.Valid[Idx])
                        continue;

                    double Lon = Surface.MinLon +
                        (Surface.MaxLon - Surface.MinLon) * Sx / Math.Max(1, Surface.SampleWidth - 1);
                    if (!BoundaryMask.IsInsideBoundaryLatLon(Lat, Lon, Boundary))
                        OutsideCount++;
                }
            }

            Assert.Equal(0, OutsideCount);
        }

        /// <summary>
        /// In Existing view the displayed minimum must match the satellite datum so the mesh meets the imagery plane.
        /// </summary>
        [Fact]
        public void FakeField01_AgdImport_ExistingViewTouchesSatelliteDatum
            (
            )
        {
            FieldSurfaceSampler Surface = BuildFakeField01AgdSurface();
            Surface.ComputeScalarRange(FieldContentView.Existing, out double VMin, out double _);

            Assert.True(Math.Abs(VMin - Surface.MinElevation) < 1e-6,
                $"Existing minimum {VMin:F6} m does not match satellite datum {Surface.MinElevation:F6} m.");
        }

        static private FieldSurfaceSampler BuildFakeField01AgdSurface
            (
            )
        {
            AgdImportResult Import = AgdService.Import(TestDataPaths.FakeField01Agd);
            ProjectDocument Project = new ProjectDocument
            {
                Survey = Import.Survey,
                DesignParameters = Import.Parameters,
                DesignGrid = Import.Grid,
                SurveySourceFormat = SurveySourceFormat.Agd
            };

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);
            return Surface;
        }
    }
}

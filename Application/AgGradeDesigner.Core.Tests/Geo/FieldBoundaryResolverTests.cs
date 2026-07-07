using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Verifies field boundary resolution and inference for surface clipping.
    /// </summary>
    public sealed class FieldBoundaryResolverTests
    {
        /// <summary>Minimum inferred boundary vertices expected for The Shop 2.</summary>
        private const int MIN_INFERRED_VERTEX_COUNT = 8;

        /// <summary>
        /// Verifies The Shop 2 resolves an explicit or inferred boundary ring.
        /// </summary>
        [Fact]
        public void Resolve_TheShop2_ReturnsOrderedBoundaryRing
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            List<(double Lat, double Lon)>? Ring = FieldBoundaryResolver.Resolve(Project.Survey, Project.DesignGrid);

            Assert.NotNull(Ring);
            Assert.True(Ring!.Count >= MIN_INFERRED_VERTEX_COUNT);
        }

        /// <summary>
        /// Verifies a project without 2PER can still infer a boundary from observed samples.
        /// </summary>
        [Fact]
        public void Resolve_SurveyWithoutTwoPer_InfersBoundaryFromObservedPoints
            (
            )
        {
            SurveyModel Survey = new SurveyModel();
            Survey.InteriorPoints.Add(new TopologyPoint
            {
                Latitude = 36.0,
                Longitude = -90.0,
                ExistingElevation = 100.0,
                Code = "3GRD"
            });
            Survey.InteriorPoints.Add(new TopologyPoint
            {
                Latitude = 36.0,
                Longitude = -89.999,
                ExistingElevation = 100.0,
                Code = "3GRD"
            });
            Survey.InteriorPoints.Add(new TopologyPoint
            {
                Latitude = 35.999,
                Longitude = -90.0,
                ExistingElevation = 100.0,
                Code = "3GRD"
            });
            Survey.InteriorPoints.Add(new TopologyPoint
            {
                Latitude = 35.999,
                Longitude = -89.999,
                ExistingElevation = 100.0,
                Code = "3GRD"
            });

            List<(double Lat, double Lon)>? Ring = FieldBoundaryResolver.Resolve(Survey, null);

            Assert.NotNull(Ring);
            Assert.True(Ring!.Count >= GeoConstants.MIN_POLYGON_VERTICES);
        }

        /// <summary>
        /// Verifies clipped project surfaces do not fill the full padded raster rectangle on FakeField.
        /// </summary>
        [Fact]
        public void BuildFromProject_FakeFieldAgs_DoesNotFillFullRasterRectangle
            (
            )
        {
            string? Path = FindFakeFieldAgs();
            if (Path == null)
                return;

            SurveyModel Survey = new SurveyModel();
            Survey.LoadAuto(Path);

            FieldSurfaceSampler Sampler = new FieldSurfaceSampler();
            Sampler.BuildFromProject(new ProjectDocument { Survey = Survey });

            int ValidCount = Sampler.Valid.Count(V => V);
            int Total = Sampler.Valid.Length;
            Assert.True(ValidCount < Total, $"Expected boundary clipping, got valid={ValidCount} total={Total}.");
        }

        /// <summary>
        /// Walks parent directories from the test base path to locate FakeField_01.ags.
        /// </summary>
        /// <returns>Absolute path when found; otherwise null.</returns>
        private static string? FindFakeFieldAgs
            (
            )
        {
            string? Dir = AppContext.BaseDirectory;
            for (int Iter = 0; Iter < 8 && Dir != null; Iter++)
            {
                string Candidate = System.IO.Path.Combine(Dir, "Test Data", "FakeField_01.ags");
                if (File.Exists(Candidate))
                    return Candidate;

                Dir = Directory.GetParent(Dir)?.FullName;
            }

            return null;
        }
    }
}

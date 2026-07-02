using System.IO.Compression;

using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Persistence
{
    /// <summary>
    /// Regression tests for AGD import → save → load → surface rendering round-trips.
    /// </summary>

    public sealed class AgDesignSerializerAgdRoundTripTests
    {
        /// <summary>
        /// Surface resolution used by this component.
        /// </summary>

        private const int SURFACE_RESOLUTION = 256;
        /// <summary>
        /// Centroid precision used by this component.
        /// </summary>

        private const int CENTROID_PRECISION = 8;
        /// <summary>
        /// Min valid surface samples used by this component.
        /// </summary>

        private const int MIN_VALID_SURFACE_SAMPLES = 1000;
        /// <summary>
        /// Min reloaded valid fraction used by this component.
        /// </summary>

        private const double MIN_RELOADED_VALID_FRACTION = 0.85;
        /// <summary>
        /// Min elevation range m used by this component.
        /// </summary>

        private const double MIN_ELEVATION_RANGE_M = 0.05;

        static private string Shop2Path => Path.Combine(TestDataPaths.TestDataDirectory, "TheShop2_2ft.agd");

        /// <summary>
        /// Verifies count valid samples.
        /// </summary>

        static private int CountValidSamples(FieldSurfaceSampler Surface)
        {
            return Surface.Valid.Count(V => V);
        }

        /// <summary>
        /// Verifies grid bin centroids survive save/load so large AGD designs still render topo.
        /// </summary>
        [Fact]
        public void SaveAndLoad_ImportedAgd_PreservesBinCentroids
            (
            )
        {
            if (!File.Exists(Shop2Path))
                return;

            AgdImportResult Imported = AgdService.Import(Shop2Path);
            ProjectDocument Project = new ProjectDocument
            {
                Survey = Imported.Survey,
                DesignParameters = Imported.Parameters,
                DesignGrid = Imported.Grid,
                SurveySourceFormat = SurveySourceFormat.Agd
            };

            GridBinData OriginalSample = Imported.Grid.Bins.First(B => B.Valid);
            string ProjectPath = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                AgDesignSerializer.Save(Project, ProjectPath);
                ProjectDocument Loaded = AgDesignSerializer.Load(ProjectPath);

                Assert.True(Loaded.HasDesign);
                Assert.NotNull(Loaded.DesignGrid);
                GridBinData LoadedSample = Loaded.DesignGrid!.Bins.First(B =>
                    B.X == OriginalSample.X && B.Y == OriginalSample.Y);

                Assert.Equal(OriginalSample.CentroidLat, LoadedSample.CentroidLat, precision: CENTROID_PRECISION);
                Assert.Equal(OriginalSample.CentroidLon, LoadedSample.CentroidLon, precision: CENTROID_PRECISION);
            }
            finally
            {
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Verifies imported AGD projects still produce a visible surface after save/load.
        /// </summary>
        [Fact]
        public void SaveAndLoad_ImportedAgd_RendersSurfaceAfterReload
            (
            )
        {
            if (!File.Exists(Shop2Path))
                return;

            AgdImportResult Imported = AgdService.Import(Shop2Path);
            ProjectDocument OriginalProject = new ProjectDocument
            {
                Survey = Imported.Survey,
                DesignParameters = Imported.Parameters,
                DesignGrid = Imported.Grid,
                SurveySourceFormat = SurveySourceFormat.Agd
            };

            FieldSurfaceSampler OriginalSurface = new FieldSurfaceSampler();
            OriginalSurface.BuildFromProject(OriginalProject, Resolution: SURFACE_RESOLUTION);
            int OriginalValidCount = CountValidSamples(OriginalSurface);
            Assert.True(OriginalValidCount > MIN_VALID_SURFACE_SAMPLES,
                $"Expected rich surface before save, got {OriginalValidCount} valid cells.");

            string ProjectPath = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                AgDesignSerializer.Save(OriginalProject, ProjectPath);
                ProjectDocument Loaded = AgDesignSerializer.Load(ProjectPath);

                FieldSurfaceSampler ReloadedSurface = new FieldSurfaceSampler();
                ReloadedSurface.BuildFromProject(Loaded, Resolution: SURFACE_RESOLUTION);
                int ReloadedValidCount = CountValidSamples(ReloadedSurface);

                Assert.True(ReloadedSurface.SampleWidth > 0);
                Assert.True(ReloadedValidCount > MIN_VALID_SURFACE_SAMPLES,
                    $"Expected rich surface after reload, got {ReloadedValidCount} valid cells.");
                Assert.InRange(ReloadedValidCount, (int)(OriginalValidCount * MIN_RELOADED_VALID_FRACTION), OriginalValidCount);
                Assert.True(ReloadedSurface.MaxElevation - ReloadedSurface.MinElevation > MIN_ELEVATION_RANGE_M);
            }
            finally
            {
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Verifies projects missing centroid attributes in data.xml still render after UTM restore.
        /// </summary>
        [Fact]
        public void Load_ProjectWithoutCentroidsInDataXml_RestoresSurfaceFromUtm
            (
            )
        {
            if (!File.Exists(Shop2Path))
                return;

            AgdImportResult Imported = AgdService.Import(Shop2Path);
            ProjectDocument Project = new ProjectDocument
            {
                Survey = Imported.Survey,
                DesignParameters = Imported.Parameters,
                DesignGrid = Imported.Grid,
                SurveySourceFormat = SurveySourceFormat.Agd
            };

            string ProjectPath = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                AgDesignSerializer.Save(Project, ProjectPath);

                string Xml = AgProjectArchive.Read(ProjectPath);
                Xml = Xml.Replace(" centroidLat=\"", " strippedCentroidLat=\"");
                Xml = Xml.Replace(" centroidLon=\"", " strippedCentroidLon=\"");
                AgProjectArchive.Write(ProjectPath, Xml);

                ProjectDocument Loaded = AgDesignSerializer.Load(ProjectPath);
                FieldSurfaceSampler Surface = new FieldSurfaceSampler();
                Surface.BuildFromProject(Loaded, Resolution: SURFACE_RESOLUTION);

                int ValidCount = CountValidSamples(Surface);
                Assert.True(ValidCount > MIN_VALID_SURFACE_SAMPLES,
                    $"Expected project to render after UTM centroid restore, got {ValidCount} valid cells.");
            }
            finally
            {
                TempFile.DeleteIfExists(ProjectPath);
            }
        }
    }
}

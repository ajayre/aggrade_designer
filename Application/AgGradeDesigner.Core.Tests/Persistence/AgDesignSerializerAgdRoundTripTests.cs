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
        static private string Shop2Path => Path.Combine(TestDataPaths.TestDataDirectory, "TheShop2_2ft.agd");

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

                Assert.Equal(OriginalSample.CentroidLat, LoadedSample.CentroidLat, precision: 8);
                Assert.Equal(OriginalSample.CentroidLon, LoadedSample.CentroidLon, precision: 8);
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
            OriginalSurface.BuildFromProject(OriginalProject, Resolution: 256);
            int OriginalValidCount = CountValidSamples(OriginalSurface);
            Assert.True(OriginalValidCount > 1000, $"Expected rich surface before save, got {OriginalValidCount} valid cells.");

            string ProjectPath = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                AgDesignSerializer.Save(OriginalProject, ProjectPath);
                ProjectDocument Loaded = AgDesignSerializer.Load(ProjectPath);

                FieldSurfaceSampler ReloadedSurface = new FieldSurfaceSampler();
                ReloadedSurface.BuildFromProject(Loaded, Resolution: 256);
                int ReloadedValidCount = CountValidSamples(ReloadedSurface);

                Assert.True(ReloadedSurface.SampleWidth > 0);
                Assert.True(ReloadedValidCount > 1000,
                    $"Expected rich surface after reload, got {ReloadedValidCount} valid cells.");
                Assert.InRange(ReloadedValidCount, (int)(OriginalValidCount * 0.85), OriginalValidCount);
                Assert.True(ReloadedSurface.MaxElevation - ReloadedSurface.MinElevation > 0.05);
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
                Surface.BuildFromProject(Loaded, Resolution: 256);

                int ValidCount = CountValidSamples(Surface);
                Assert.True(ValidCount > 1000,
                    $"Expected project to render after UTM centroid restore, got {ValidCount} valid cells.");
            }
            finally
            {
                TempFile.DeleteIfExists(ProjectPath);
            }
        }
    }
}

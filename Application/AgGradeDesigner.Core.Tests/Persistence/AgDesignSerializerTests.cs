using System.IO.Compression;

using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Persistence
{
    /// <summary>
    /// Unit tests for <see cref="AgDesignSerializer"/> project save and load round-trips.
    /// </summary>
    public sealed class AgDesignSerializerTests
    {
        /// <summary>
        /// Verifies an empty project round-trips through save and load.
        /// </summary>
        [Fact]
        public void SaveAndLoad_EmptyProject_RoundTrips
            (
            )
        {
            string Path = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                ProjectDocument Original = new ProjectDocument();
                AgDesignSerializer.Save(Original, Path);
                ProjectDocument Loaded = AgDesignSerializer.Load(Path);

                // Fresh project XML must deserialize back to an empty shell.
                Assert.False(Loaded.HasSurvey);
                Assert.False(Loaded.HasDesign);
                Assert.False(Loaded.IsModified);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies survey-only projects round-trip benchmark and interior counts.
        /// </summary>
        [Fact]
        public void SaveAndLoad_SurveyOnly_RoundTrips
            (
            )
        {
            string AgsPath = TempFile.Write(".ags", SurveySamples.MINIMAL_AGS);
            string ProjectPath = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadFromAgs(AgsPath);

                ProjectDocument Project = new ProjectDocument
                {
                    Survey = Survey,
                    SurveySourceFormat = SurveySourceFormat.Ags
                };
                Project.ViewerState.FieldContentView = FieldContentView.Existing;

                AgDesignSerializer.Save(Project, ProjectPath);
                ProjectDocument Loaded = AgDesignSerializer.Load(ProjectPath);

                // Survey topology and viewer field content must survive XML persistence.
                Assert.True(Loaded.HasSurvey);
                Assert.NotNull(Loaded.Survey);
                Assert.Equal(Survey.Benchmarks.Count, Loaded.Survey!.Benchmarks.Count);
                Assert.Equal(Survey.InteriorPoints.Count, Loaded.Survey.InteriorPoints.Count);
                Assert.Equal(FieldContentView.Existing, Loaded.ViewerState.FieldContentView);
            }
            finally
            {
                TempFile.DeleteIfExists(AgsPath);
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Verifies survey plus design projects round-trip grid dimensions and bin counts.
        /// </summary>
        [Fact]
        public void SaveAndLoad_SurveyAndDesign_RoundTripsGrid
            (
            )
        {
            string AgsPath = TempFile.Write(".ags", SurveySamples.MINIMAL_AGS);
            string ProjectPath = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadFromAgs(AgsPath);
                FieldCreator Creator = new FieldCreator();
                (DesignGrid Grid, _) = Creator.CreateDesign(Survey, new DesignParameters
                {
                    MainSlopeDirectionDeg = 90,
                    MainSlopePercent = 0.0,
                    CrossSlopePercent = 0.0,
                    CutFillRatio = 1.2
                });

                ProjectDocument Project = new ProjectDocument
                {
                    Survey = Survey,
                    SurveySourceFormat = SurveySourceFormat.Ags,
                    DesignParameters = new DesignParameters
                    {
                        MainSlopeDirectionDeg = 90,
                        MainSlopePercent = 0.0,
                        CrossSlopePercent = 0.0,
                        CutFillRatio = 1.2
                    },
                    DesignGrid = Grid
                };

                AgDesignSerializer.Save(Project, ProjectPath);
                ProjectDocument Loaded = AgDesignSerializer.Load(ProjectPath);

                // Grid topology must match the pre-save design dimensions and bin list.
                Assert.True(Loaded.HasDesign);
                Assert.NotNull(Loaded.DesignGrid);
                Assert.Equal(Grid.Width, Loaded.DesignGrid!.Width);
                Assert.Equal(Grid.Height, Loaded.DesignGrid.Height);
                Assert.Equal(Grid.Bins.Count, Loaded.DesignGrid.Bins.Count);
            }
            finally
            {
                TempFile.DeleteIfExists(AgsPath);
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Verifies saved project files are zip archives containing data.xml.
        /// </summary>
        [Fact]
        public void Save_CreatesZipArchiveWithDataXml
            (
            )
        {
            string Path = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                ProjectDocument Project = new ProjectDocument();
                AgDesignSerializer.Save(Project, Path);

                using ZipArchive Archive = ZipFile.OpenRead(Path);
                Assert.NotNull(Archive.GetEntry(ProjectDocument.ARCHIVE_DATA_ENTRY));
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies unsupported schema versions are rejected on load.
        /// </summary>
        [Fact]
        public void Load_RejectsInvalidSchemaVersion
            (
            )
        {
            string Path = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                AgProjectArchive.Write(Path, """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <AgDesignProject xmlns="https://aggrade.com/schemas/agdesign/1.0" schemaVersion="99.0">
                      <Metadata>
                        <CreatedUtc>2026-06-29T12:00:00Z</CreatedUtc>
                      </Metadata>
                    </AgDesignProject>
                    """);

                // Future schema versions must fail closed until explicitly supported.
                Assert.Throws<InvalidDataException>(() => AgDesignSerializer.Load(Path));
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}

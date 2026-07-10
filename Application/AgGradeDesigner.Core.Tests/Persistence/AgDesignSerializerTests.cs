using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Rendering;
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
        /// Main slope direction deg used by this component.
        /// </summary>

        private const double MAIN_SLOPE_DIRECTION_DEG = 90;
        /// <summary>
        /// Zero slope percent used by this component.
        /// </summary>

        private const double ZERO_SLOPE_PERCENT = 0.0;
        /// <summary>
        /// Default cut fill ratio used by this component.
        /// </summary>

        private const double DEFAULT_CUT_FILL_RATIO = FieldConstants.CUT_FILL_RATIO;
        /// <summary>
        /// Unsupported schema version used by this component.
        /// </summary>

        private const double UNSUPPORTED_SCHEMA_VERSION = 99.0;

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
                    MainSlopeDirectionDeg = MAIN_SLOPE_DIRECTION_DEG,
                    MainSlopePercent = ZERO_SLOPE_PERCENT,
                    CrossSlopePercent = ZERO_SLOPE_PERCENT,
                    CutFillRatio = DEFAULT_CUT_FILL_RATIO
                });

                ProjectDocument Project = new ProjectDocument
                {
                    Survey = Survey,
                    SurveySourceFormat = SurveySourceFormat.Ags,
                    DesignParameters = new DesignParameters
                    {
                        MainSlopeDirectionDeg = MAIN_SLOPE_DIRECTION_DEG,
                        MainSlopePercent = ZERO_SLOPE_PERCENT,
                        CrossSlopePercent = ZERO_SLOPE_PERCENT,
                        CutFillRatio = DEFAULT_CUT_FILL_RATIO
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
                Assert.Equal(Grid.MinX, Loaded.DesignGrid.MinX, precision: 6);
                Assert.Equal(Grid.MaxX, Loaded.DesignGrid.MaxX, precision: 6);
                Assert.Equal(Grid.MinY, Loaded.DesignGrid.MinY, precision: 6);
                Assert.Equal(Grid.MaxY, Loaded.DesignGrid.MaxY, precision: 6);
                Assert.Equal(Grid.CompositeSlopePercent, Loaded.DesignGrid.CompositeSlopePercent, precision: 6);
                Assert.Equal(Grid.CompositeSlopeHeadingDeg, Loaded.DesignGrid.CompositeSlopeHeadingDeg, precision: 6);
                Assert.Equal(Grid.TotalCutCy, Loaded.DesignGrid.TotalCutCy, precision: 6);
                Assert.Equal(Grid.TotalFillCy, Loaded.DesignGrid.TotalFillCy, precision: 6);
                Assert.Equal(Grid.MeanLat, Loaded.DesignGrid.MeanLat, precision: 6);
                Assert.Equal(Grid.MeanLon, Loaded.DesignGrid.MeanLon, precision: 6);
            }
            finally
            {
                TempFile.DeleteIfExists(AgsPath);
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Verifies legacy projects missing derived grid metadata restore aggregates on load.
        /// </summary>
        [Fact]
        public void Load_LegacyGridWithoutDerivedMetadata_RestoresAggregates
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
                    MainSlopeDirectionDeg = MAIN_SLOPE_DIRECTION_DEG,
                    MainSlopePercent = 0.5,
                    CrossSlopePercent = ZERO_SLOPE_PERCENT,
                    CutFillRatio = DEFAULT_CUT_FILL_RATIO
                });

                ProjectDocument Project = new ProjectDocument
                {
                    Survey = Survey,
                    SurveySourceFormat = SurveySourceFormat.Ags,
                    DesignParameters = new DesignParameters
                    {
                        MainSlopeDirectionDeg = MAIN_SLOPE_DIRECTION_DEG,
                        MainSlopePercent = 0.5,
                        CrossSlopePercent = ZERO_SLOPE_PERCENT,
                        CutFillRatio = DEFAULT_CUT_FILL_RATIO
                    },
                    DesignGrid = Grid
                };

                AgDesignSerializer.Save(Project, ProjectPath);

                // Strip derived-metadata attributes to simulate a pre-fix project file.
                string Xml = AgProjectArchive.Read(ProjectPath);
                Xml = StripGridAttribute(Xml, "minX");
                Xml = StripGridAttribute(Xml, "minY");
                Xml = StripGridAttribute(Xml, "maxX");
                Xml = StripGridAttribute(Xml, "maxY");
                Xml = StripGridAttribute(Xml, "meanLat");
                Xml = StripGridAttribute(Xml, "meanLon");
                Xml = StripGridAttribute(Xml, "totalCutCy");
                Xml = StripGridAttribute(Xml, "totalFillCy");
                Xml = StripGridAttribute(Xml, "compositeSlopeHeadingDeg");
                Xml = StripGridAttribute(Xml, "compositeSlopePercent");
                AgProjectArchive.Write(ProjectPath, Xml);

                ProjectDocument Loaded = AgDesignSerializer.Load(ProjectPath);
                Assert.NotNull(Loaded.DesignGrid);
                Assert.Equal(Grid.OriginEastingM, Loaded.DesignGrid!.MinX, precision: 6);
                Assert.Equal(Grid.OriginNorthingM, Loaded.DesignGrid.MinY, precision: 6);
                Assert.True(Loaded.DesignGrid.MaxX > Loaded.DesignGrid.MinX);
                Assert.True(Loaded.DesignGrid.MaxY > Loaded.DesignGrid.MinY);
                Assert.Equal(Grid.CompositeSlopePercent, Loaded.DesignGrid.CompositeSlopePercent, precision: 6);
                Assert.Equal(Grid.CompositeSlopeHeadingDeg, Loaded.DesignGrid.CompositeSlopeHeadingDeg, precision: 6);
                Assert.True(Loaded.DesignGrid.MeanLat != 0.0 || Loaded.DesignGrid.MeanLon != 0.0);

                DisplaySettings Display = new DisplaySettings { Contours = true };
                Assert.True(ContourVisibility.ShouldDrawContours(Display, Loaded, FieldContentView.Proposed));
            }
            finally
            {
                TempFile.DeleteIfExists(AgsPath);
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Removes one Grid attribute from project XML for legacy-load simulation.
        /// </summary>
        /// <param name="Xml">Project data.xml contents.</param>
        /// <param name="AttributeName">Attribute local name to strip.</param>
        /// <returns>XML with the attribute removed from Grid elements.</returns>
        static private string StripGridAttribute
            (
            string Xml,
            string AttributeName
            )
        {
            // Match space-prefixed attributes written by the serializer (G17 invariant doubles).
            string Pattern = " " + AttributeName + "=\"";
            int Start = Xml.IndexOf(Pattern, StringComparison.Ordinal);
            if (Start < 0)
                return Xml;

            int ValueStart = Start + Pattern.Length;
            int EndQuote = Xml.IndexOf('"', ValueStart);
            if (EndQuote < 0)
                return Xml;

            return Xml.Remove(Start, EndQuote - Start + 1);
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

                using System.IO.Compression.ZipArchive Archive = System.IO.Compression.ZipFile.OpenRead(Path);
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
                AgProjectArchive.Write(Path, $"""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <AgDesignProject xmlns="https://aggrade.com/schemas/agdesign/1.0" schemaVersion="{UNSUPPORTED_SCHEMA_VERSION}">
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

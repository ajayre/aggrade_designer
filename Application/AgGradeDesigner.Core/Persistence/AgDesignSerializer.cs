using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Persistence
{
    /// <summary>
    /// Reads and writes AgGrade Designer project documents as zip archives containing XML.
    /// </summary>

    static public class AgDesignSerializer
    {
        /// <summary>Legacy default 3D orbit pitch when older project files omit the attribute.</summary>
        private const double LEGACY_DEFAULT_ORBIT_PITCH_DEG = 30.0;

        /// <summary>XML namespace for AgDesign project schema elements.</summary>
        static private readonly XNamespace Ns = ProjectDocument.SCHEMA_NAMESPACE;

        /// <summary>
        /// Serializes a project document to the given file path and marks it clean.
        /// </summary>
        /// <param name="Project">Project to persist.</param>
        /// <param name="FilePath">Destination file path.</param>
        static public void Save
            (
            ProjectDocument Project,
            string FilePath
            )
        {
            // Stamp modification time before writing so metadata reflects this save.
            Project.ModifiedUtc = DateTime.UtcNow;
            XDocument Doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                BuildRoot(Project));
            AgProjectArchive.Write(FilePath, SerializeXml(Doc));
            Project.FilePath = FilePath;
            Project.MarkClean();
        }

        /// <summary>
        /// Loads a project document from a project archive file.
        /// </summary>
        /// <param name="FilePath">Source file path.</param>
        /// <returns>Deserialized project with survey, design, and viewer state when present.</returns>
        /// <exception cref="InvalidDataException">Root element, schema version, or structure is invalid.</exception>
        static public ProjectDocument Load
            (
            string FilePath
            )
        {
            string Xml = AgProjectArchive.Read(FilePath);
            return ParseProjectXml(Xml, FilePath);
        }

        /// <summary>
        /// Parses project XML into a <see cref="ProjectDocument"/>.
        /// </summary>
        /// <param name="Xml">Project document XML.</param>
        /// <param name="FilePath">Source file path recorded on the loaded project.</param>
        static private ProjectDocument ParseProjectXml
            (
            string Xml,
            string FilePath
            )
        {
            XDocument Doc = XDocument.Parse(Xml, LoadOptions.None);
            XElement Root = Doc.Root ?? throw new InvalidDataException("Missing root element.");
            if (Root.Name.LocalName != "AgDesignProject")
                throw new InvalidDataException("Root element must be AgDesignProject.");

            // Reject files from unsupported schema versions before parsing body content.
            string Version = Root.Attribute("schemaVersion")?.Value ?? "";
            if (Version != ProjectDocument.SCHEMA_VERSION)
                throw new InvalidDataException($"Unsupported schema version '{Version}'.");

            ProjectDocument Project = new ProjectDocument { FilePath = FilePath, IsModified = false };

            XElement? Metadata = Root.Element(Ns + "Metadata");
            if (Metadata != null)
            {
                Project.CreatedUtc = ParseUtc(Metadata.Element(Ns + "CreatedUtc")?.Value) ?? DateTime.UtcNow;
                Project.ModifiedUtc = ParseUtc(Metadata.Element(Ns + "ModifiedUtc")?.Value) ?? DateTime.UtcNow;
            }

            XElement? SurveyEl = Root.Element(Ns + "Survey");
            if (SurveyEl != null)
            {
                Project.Survey = ReadSurvey(SurveyEl);
                string? Fmt = SurveyEl.Attribute("sourceFormat")?.Value;
                Project.SurveySourceFormat = Fmt switch
                {
                    "Ags" => SurveySourceFormat.Ags,
                    "Multiplane" => SurveySourceFormat.Multiplane,
                    "Agd" => SurveySourceFormat.Agd,
                    _ => SurveySourceFormat.None
                };
            }

            XElement? DesignEl = Root.Element(Ns + "Design");
            if (DesignEl != null)
                ReadDesign(DesignEl, Project);

            XElement? HaulEl = Root.Element(Ns + "Haul");
            if (HaulEl != null)
            {
                string? PayloadJson = HaulEl.Element(Ns + "Payload")?.Value;
                Project.HaulData = HaulDataSerializer.FromPayload(HaulDataSerializer.DeserializePayload(PayloadJson ?? string.Empty));
            }

            XElement? ViewerEl = Root.Element(Ns + "ViewerState");
            if (ViewerEl != null)
                Project.ViewerState = ReadViewerState(ViewerEl);

            return Project;
        }

        /// <summary>
        /// Serializes an XML document to a UTF-8 string without a byte-order mark.
        /// </summary>
        static private string SerializeXml
            (
            XDocument Doc
            )
        {
            using MemoryStream Stream = new MemoryStream();
            using (XmlWriter Writer = XmlWriter.Create(Stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = false,
                OmitXmlDeclaration = false
            }))
            {
                Doc.Save(Writer);
            }

            return Encoding.UTF8.GetString(Stream.ToArray());
        }

        /// <summary>
        /// Builds the root AgDesignProject element for serialization.
        /// </summary>
        /// <param name="Project">Project whose survey, design, and viewer state are written.</param>
        /// <returns>Root XML element ready to embed in an XDocument.</returns>
        static private XElement BuildRoot
            (
            ProjectDocument Project
            )
        {
            XElement Root = new XElement(Ns + "AgDesignProject",
                new XAttribute("schemaVersion", ProjectDocument.SCHEMA_VERSION),
                new XAttribute(XNamespace.Xmlns + "ag", Ns),
                new XAttribute("xmlns", Ns),
                new XElement(Ns + "Metadata",
                    new XElement(Ns + "CreatedUtc", Project.CreatedUtc.ToString("o", CultureInfo.InvariantCulture)),
                    new XElement(Ns + "ModifiedUtc", Project.ModifiedUtc.ToString("o", CultureInfo.InvariantCulture)),
                    new XElement(Ns + "Application", ProjectDocument.APPLICATION_NAME),
                    new XElement(Ns + "ApplicationVersion", ProjectDocument.APPLICATION_VERSION)));

            if (Project.HasSurvey && Project.Survey != null)
                Root.Add(WriteSurvey(Project.Survey, Project.SurveySourceFormat));

            if (Project.HasDesign && Project.DesignParameters != null && Project.DesignGrid != null)
                Root.Add(WriteDesign(Project.DesignParameters, Project.DesignGrid));

            if (Project.HasHaulData && Project.HaulData != null)
                Root.Add(WriteHaul(Project.HaulData));

            Root.Add(WriteViewerState(Project.ViewerState));
            return Root;
        }

        /// <summary>
        /// Serializes survey benchmarks and topology points to XML.
        /// </summary>
        /// <param name="Survey">Survey model to write.</param>
        /// <param name="Format">Original survey source format for the sourceFormat attribute.</param>
        /// <returns>Survey XML element.</returns>
        static private XElement WriteSurvey
            (
            SurveyModel Survey,
            SurveySourceFormat Format
            )
        {
            XElement El = new XElement(Ns + "Survey",
                new XAttribute("sourceFormat", Format.ToString()));
            if (Survey.MasterAbsoluteElevationM.HasValue)
                El.Add(new XAttribute("masterAbsoluteElevationM",
                    Survey.MasterAbsoluteElevationM.Value.ToString("G17", CultureInfo.InvariantCulture)));

            foreach (Benchmark Bm in Survey.Benchmarks)
            {
                El.Add(new XElement(Ns + "Benchmark",
                    new XAttribute("name", Bm.Name),
                    new XAttribute("latitude", Bm.Location.Latitude.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("longitude", Bm.Location.Longitude.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("elevationM", Bm.Elevation.ToString("G17", CultureInfo.InvariantCulture))));
            }

            foreach (TopologyPoint Pt in Survey.BoundaryPoints)
            {
                El.Add(new XElement(Ns + "Point",
                    new XAttribute("role", "Boundary"),
                    new XAttribute("latitude", Pt.Latitude.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("longitude", Pt.Longitude.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("elevationM", Pt.ExistingElevation.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("code", Pt.Code ?? "2PER")));
            }

            foreach (TopologyPoint Pt in Survey.InteriorPoints)
            {
                El.Add(new XElement(Ns + "Point",
                    new XAttribute("role", "Interior"),
                    new XAttribute("latitude", Pt.Latitude.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("longitude", Pt.Longitude.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("elevationM", Pt.ExistingElevation.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("code", Pt.Code ?? "3GRD")));
            }

            return El;
        }

        /// <summary>
        /// Deserializes survey benchmarks and topology points from XML.
        /// </summary>
        /// <param name="El">Survey XML element.</param>
        /// <returns>Populated survey model.</returns>
        static private SurveyModel ReadSurvey
            (
            XElement El
            )
        {
            SurveyModel Survey = new SurveyModel();
            string? Anchor = El.Attribute("masterAbsoluteElevationM")?.Value;
            if (double.TryParse(Anchor, NumberStyles.Float, CultureInfo.InvariantCulture, out double AnchorM))
                Survey.SetMasterAbsoluteElevationM(AnchorM);

            foreach (XElement Child in El.Elements())
            {
                if (Child.Name.LocalName == "Benchmark")
                {
                    Survey.Benchmarks.Add(new Benchmark(
                        ParseDouble(Child, "latitude"),
                        ParseDouble(Child, "longitude"),
                        Child.Attribute("name")?.Value ?? "BM",
                        ParseDouble(Child, "elevationM")));
                }
                else if (Child.Name.LocalName == "Point")
                {
                    TopologyPoint Pt = new TopologyPoint
                    {
                        Latitude = ParseDouble(Child, "latitude"),
                        Longitude = ParseDouble(Child, "longitude"),
                        ExistingElevation = ParseDouble(Child, "elevationM"),
                        Code = Child.Attribute("code")?.Value ?? ""
                    };
                    // Route topology points to boundary or interior lists by persisted role attribute.
                    string Role = Child.Attribute("role")?.Value ?? "Interior";
                    if (Role == "Boundary")
                        Survey.BoundaryPoints.Add(Pt);
                    else
                        Survey.InteriorPoints.Add(Pt);
                }
            }

            return Survey;
        }

        /// <summary>
        /// Serializes design parameters and grid bins to XML.
        /// </summary>
        /// <param name="Params">Design slope and volume parameters.</param>
        /// <param name="Grid">Bin grid with elevations and plane coefficients.</param>
        /// <returns>Design XML element.</returns>
        static private XElement WriteDesign
            (
            DesignParameters Params,
            DesignGrid Grid
            )
        {
            // Persist import/export volumes in cubic metres per schema; convert from cubic yards.
            double ImportM3 = Params.ImportToFieldCy / FieldConstants.CUBIC_YARDS_PER_CUBIC_METER;
            double ExportM3 = Params.ExportFromFieldCy / FieldConstants.CUBIC_YARDS_PER_CUBIC_METER;

            XElement DesignEl = new XElement(Ns + "Design",
                new XElement(Ns + "Parameters",
                    new XAttribute("mainSlopeDirectionDeg", Params.MainSlopeDirectionDeg.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("mainSlopePercent", Params.MainSlopePercent.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("crossSlopePercent", Params.CrossSlopePercent.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("cutFillRatio", Params.CutFillRatio.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("importToFieldM3", ImportM3.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("exportFromFieldM3", ExportM3.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("useExistingTopology", Params.UseExistingTopology ? "true" : "false")));

            XElement GridEl = new XElement(Ns + "Grid",
                new XAttribute("binSizeM", Grid.BinSizeM.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("width", Grid.Width),
                new XAttribute("height", Grid.Height),
                new XAttribute("utmZone", Grid.UtmZone),
                new XAttribute("utmNorth", Grid.UtmNorth ? "true" : "false"),
                new XAttribute("originEastingM", Grid.OriginEastingM.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("originNorthingM", Grid.OriginNorthingM.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("planeA", Grid.PlaneA.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("planeB", Grid.PlaneB.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("planeC", Grid.PlaneC.ToString("G17", CultureInfo.InvariantCulture)));

            foreach (GridBinData Bin in Grid.Bins)
            {
                GridEl.Add(new XElement(Ns + "Bin",
                    new XAttribute("x", Bin.X),
                    new XAttribute("y", Bin.Y),
                    new XAttribute("existingM", Bin.ExistingM.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("proposedM", Bin.ProposedM.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("centroidLat", Bin.CentroidLat.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("centroidLon", Bin.CentroidLon.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("valid", Bin.Valid ? "true" : "false")));
            }

            DesignEl.Add(GridEl);
            return DesignEl;
        }

        /// <summary>
        /// Serializes embedded haul data to XML.
        /// </summary>
        /// <param name="HaulData">Haul data to write.</param>
        /// <returns>Haul XML element.</returns>
        static private XElement WriteHaul
            (
            HaulData HaulData
            )
        {
            return new XElement(Ns + "Haul",
                new XElement(Ns + "Payload", HaulDataSerializer.SerializePayload(HaulData.Payload)));
        }

        /// <summary>
        /// Deserializes design parameters and grid bins into a project document.
        /// </summary>
        /// <param name="DesignEl">Design XML element.</param>
        /// <param name="Project">Project receiving design parameters and grid.</param>
        static private void ReadDesign
            (
            XElement DesignEl,
            ProjectDocument Project
            )
        {
            XElement? ParamsEl = DesignEl.Element(Ns + "Parameters");
            if (ParamsEl == null)
                return;

            DesignParameters Params = new DesignParameters
            {
                MainSlopeDirectionDeg = ParseAttrDouble(ParamsEl, "mainSlopeDirectionDeg", DesignParameters.DEFAULT_MAIN_SLOPE_DIRECTION_DEG),
                MainSlopePercent = ParseAttrDouble(ParamsEl, "mainSlopePercent", DesignParameters.DEFAULT_MAIN_SLOPE_PERCENT),
                CrossSlopePercent = ParseAttrDouble(ParamsEl, "crossSlopePercent"),
                CutFillRatio = ParseAttrDouble(ParamsEl, "cutFillRatio", FieldConstants.CUT_FILL_RATIO),
                ImportToFieldCy = ParseAttrDouble(ParamsEl, "importToFieldM3") * FieldConstants.CUBIC_YARDS_PER_CUBIC_METER,
                ExportFromFieldCy = ParseAttrDouble(ParamsEl, "exportFromFieldM3") * FieldConstants.CUBIC_YARDS_PER_CUBIC_METER,
                UseExistingTopology = ParseBoolAttr(ParamsEl, "useExistingTopology", false)
            };
            Project.DesignParameters = Params;

            XElement? GridEl = DesignEl.Element(Ns + "Grid");
            if (GridEl == null)
                return;

            DesignGrid Grid = new DesignGrid
            {
                BinSizeM = ParseAttrDouble(GridEl, "binSizeM", FieldConstants.BIN_SIZE_M),
                Width = ParseAttrInt(GridEl, "width"),
                Height = ParseAttrInt(GridEl, "height"),
                UtmZone = ParseAttrInt(GridEl, "utmZone"),
                UtmNorth = string.Equals(GridEl.Attribute("utmNorth")?.Value, "true", StringComparison.OrdinalIgnoreCase),
                OriginEastingM = ParseAttrDouble(GridEl, "originEastingM"),
                OriginNorthingM = ParseAttrDouble(GridEl, "originNorthingM"),
                PlaneA = ParseAttrDouble(GridEl, "planeA"),
                PlaneB = ParseAttrDouble(GridEl, "planeB"),
                PlaneC = ParseAttrDouble(GridEl, "planeC")
            };

            foreach (XElement BinEl in GridEl.Elements(Ns + "Bin"))
            {
                Grid.Bins.Add(new GridBinData
                {
                    X = ParseAttrInt(BinEl, "x"),
                    Y = ParseAttrInt(BinEl, "y"),
                    ExistingM = ParseAttrDouble(BinEl, "existingM"),
                    ProposedM = ParseAttrDouble(BinEl, "proposedM"),
                    CentroidLat = ParseOptionalAttrDouble(BinEl, "centroidLat"),
                    CentroidLon = ParseOptionalAttrDouble(BinEl, "centroidLon"),
                    Valid = string.Equals(BinEl.Attribute("valid")?.Value, "true", StringComparison.OrdinalIgnoreCase)
                });
            }

            Grid.EnsureCentroidsFromUtm();
            Project.DesignGrid = Grid;
        }

        /// <summary>
        /// Serializes viewer camera, display toggles, and view mode to XML.
        /// </summary>
        /// <param name="State">Viewer state to write.</param>
        /// <returns>ViewerState XML element.</returns>
        static private XElement WriteViewerState
            (
            ViewerState State
            )
        {
            return new XElement(Ns + "ViewerState",
                new XElement(Ns + "ViewMode", new XAttribute("mode", State.ViewMode == ViewMode.ThreeD ? "3D" : "2D")),
                new XElement(Ns + "FieldContentView", new XAttribute("view", State.FieldContentView.ToString())),
                new XElement(Ns + "DisplayToggles",
                    new XAttribute("surveyPoints", State.Display.SurveyPoints ? "true" : "false"),
                    new XAttribute("contours", State.Display.Contours ? "true" : "false"),
                    new XAttribute("hillshade", State.Display.Hillshade ? "true" : "false"),
                    new XAttribute("benchmarks", State.Display.Benchmarks ? "true" : "false"),
                    new XAttribute("haulFlow", State.Display.HaulFlow ? "true" : "false"),
                    new XAttribute("designOverlay", State.Display.DesignOverlay ? "true" : "false"),
                    new XAttribute("satellite", State.Display.Satellite ? "true" : "false")),
                new XElement(Ns + "VerticalExaggeration",
                    new XAttribute("value", State.Display.VerticalExaggeration.ToString("G17", CultureInfo.InvariantCulture))),
                new XElement(Ns + "TopoCutFillOpacity",
                    new XAttribute("percent", State.Display.TopoCutFillOpacityPercent.ToString("G17", CultureInfo.InvariantCulture))),
                new XElement(Ns + "Camera2D",
                    new XAttribute("centreLat", State.Camera2D.CentreLat.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("centreLon", State.Camera2D.CentreLon.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("zoom", State.Camera2D.Zoom.ToString("G17", CultureInfo.InvariantCulture))),
                new XElement(Ns + "Camera3D",
                    new XAttribute("orbitHeadingDeg", State.Camera3D.OrbitHeadingDeg.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("orbitPitchDeg", State.Camera3D.OrbitPitchDeg.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("distanceM", State.Camera3D.DistanceM.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("orbitCentreLat", State.Camera3D.OrbitCentreLat.ToString("G17", CultureInfo.InvariantCulture)),
                    new XAttribute("orbitCentreLon", State.Camera3D.OrbitCentreLon.ToString("G17", CultureInfo.InvariantCulture))));
        }

        /// <summary>
        /// Deserializes viewer camera, display toggles, and view mode from XML.
        /// </summary>
        /// <param name="El">ViewerState XML element.</param>
        /// <returns>Populated viewer state with defaults for missing sections.</returns>
        static private ViewerState ReadViewerState
            (
            XElement El
            )
        {
            ViewerState State = new ViewerState();
            string? Mode = El.Element(Ns + "ViewMode")?.Attribute("mode")?.Value;
            State.ViewMode = Mode == "3D" ? ViewMode.ThreeD : ViewMode.TwoD;

            string? View = El.Element(Ns + "FieldContentView")?.Attribute("view")?.Value;
            if (Enum.TryParse<FieldContentView>(View, out FieldContentView Parsed))
                State.FieldContentView = Parsed;

            XElement? Toggles = El.Element(Ns + "DisplayToggles");
            if (Toggles != null)
            {
                State.Display.SurveyPoints = ParseBoolAttr(Toggles, "surveyPoints", true);
                State.Display.Contours = ParseBoolAttr(Toggles, "contours", true);
                State.Display.Hillshade = ParseBoolAttr(Toggles, "hillshade", true);
                State.Display.Benchmarks = ParseBoolAttr(Toggles, "benchmarks", true);
                State.Display.HaulFlow = ParseBoolAttr(Toggles, "haulFlow", false);
                State.Display.DesignOverlay = ParseBoolAttr(Toggles, "designOverlay", false);
                State.Display.Satellite = ParseBoolAttr(Toggles, "satellite", false);
            }

            string? Vex = El.Element(Ns + "VerticalExaggeration")?.Attribute("value")?.Value;
            if (double.TryParse(Vex, NumberStyles.Float, CultureInfo.InvariantCulture, out double VexVal))
                State.Display.VerticalExaggeration = VexVal;

            string? TopoOpacity = El.Element(Ns + "TopoCutFillOpacity")?.Attribute("percent")?.Value;
            if (double.TryParse(TopoOpacity, NumberStyles.Float, CultureInfo.InvariantCulture, out double TopoOpacityVal))
                State.Display.TopoCutFillOpacityPercent = Math.Clamp(TopoOpacityVal, DisplaySettings.OPACITY_PERCENT_MIN, DisplaySettings.OPACITY_PERCENT_MAX);

            XElement? Cam2 = El.Element(Ns + "Camera2D");
            if (Cam2 != null)
            {
                // Default centre matches typical US mid-latitude when legacy files omit camera data.
                State.Camera2D.CentreLat = ParseAttrDouble(Cam2, "centreLat", Camera2DState.DEFAULT_CENTRE_LAT);
                State.Camera2D.CentreLon = ParseAttrDouble(Cam2, "centreLon", Camera2DState.DEFAULT_CENTRE_LON);
                State.Camera2D.Zoom = ParseAttrDouble(Cam2, "zoom", Camera2DState.DEFAULT_ZOOM);
            }

            XElement? Cam3 = El.Element(Ns + "Camera3D");
            if (Cam3 != null)
            {
                State.Camera3D.OrbitHeadingDeg = ParseAttrDouble(Cam3, "orbitHeadingDeg", Camera3DState.DEFAULT_ORBIT_HEADING_DEG);
                State.Camera3D.OrbitPitchDeg = ParseAttrDouble(Cam3, "orbitPitchDeg", LEGACY_DEFAULT_ORBIT_PITCH_DEG);
                State.Camera3D.DistanceM = ParseAttrDouble(Cam3, "distanceM", Camera3DState.DEFAULT_DISTANCE_M);
                State.Camera3D.OrbitCentreLat = ParseAttrDouble(Cam3, "orbitCentreLat", double.NaN);
                State.Camera3D.OrbitCentreLon = ParseAttrDouble(Cam3, "orbitCentreLon", double.NaN);
            }

            return State;
        }

        /// <summary>
        /// Parses an ISO-style UTC timestamp string.
        /// </summary>
        /// <param name="Value">Timestamp text from XML metadata.</param>
        /// <returns>Parsed UTC time, or null when the value is missing or invalid.</returns>
        static private DateTime? ParseUtc
            (
            string? Value
            )
        {
            if (DateTime.TryParse(Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime Parsed))
                return Parsed.ToUniversalTime();
            return null;
        }

        /// <summary>
        /// Parses a required floating-point attribute on an XML element.
        /// </summary>
        /// <param name="El">Element carrying the attribute.</param>
        /// <param name="Name">Attribute local name.</param>
        /// <returns>Parsed double value.</returns>
        static private double ParseDouble
            (
            XElement El,
            string Name
            )
        {
            return double.Parse(El.Attribute(Name)!.Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses an optional floating-point attribute, returning NaN when absent or invalid.
        /// </summary>
        /// <param name="El">Element carrying the attribute.</param>
        /// <param name="Name">Attribute local name.</param>
        /// <returns>Parsed double, or NaN when the attribute is missing or not parseable.</returns>
        static private double ParseOptionalAttrDouble
            (
            XElement El,
            string Name
            )
        {
            string? V = El.Attribute(Name)?.Value;
            return double.TryParse(V, NumberStyles.Float, CultureInfo.InvariantCulture, out double Parsed)
                ? Parsed
                : double.NaN;
        }

        /// <summary>
        /// Parses an optional floating-point attribute, returning a default when absent or invalid.
        /// </summary>
        /// <param name="El">Element carrying the attribute.</param>
        /// <param name="Name">Attribute local name.</param>
        /// <param name="Default">Value used when the attribute is missing or not parseable.</param>
        /// <returns>Parsed double or the default.</returns>
        static private double ParseAttrDouble
            (
            XElement El,
            string Name,
            double Default = 0
            )
        {
            string? V = El.Attribute(Name)?.Value;
            return double.TryParse(V, NumberStyles.Float, CultureInfo.InvariantCulture, out double Parsed) ? Parsed : Default;
        }

        /// <summary>
        /// Parses an optional integer attribute, returning a default when absent or invalid.
        /// </summary>
        /// <param name="El">Element carrying the attribute.</param>
        /// <param name="Name">Attribute local name.</param>
        /// <param name="Default">Value used when the attribute is missing or not parseable.</param>
        /// <returns>Parsed integer or the default.</returns>
        static private int ParseAttrInt
            (
            XElement El,
            string Name,
            int Default = 0
            )
        {
            string? V = El.Attribute(Name)?.Value;
            return int.TryParse(V, NumberStyles.Integer, CultureInfo.InvariantCulture, out int Parsed) ? Parsed : Default;
        }

        /// <summary>
        /// Parses a boolean attribute stored as "true" or "false" text.
        /// </summary>
        /// <param name="El">Element carrying the attribute.</param>
        /// <param name="Name">Attribute local name.</param>
        /// <param name="Default">Value used when the attribute is absent.</param>
        /// <returns>Parsed boolean or the default.</returns>
        static private bool ParseBoolAttr
            (
            XElement El,
            string Name,
            bool Default
            )
        {
            string? V = El.Attribute(Name)?.Value;
            return V == null ? Default : string.Equals(V, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Water;

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
                ReadHaul(HaulEl, Project);

            XElement? WaterEl = Root.Element(Ns + "Water");
            if (WaterEl != null)
                ReadWater(WaterEl, Project);
            else
                Project.Water.ApplyDefaultSettingsIfNeeded();

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

            Root.Add(WriteHaul(Project));
            Root.Add(WriteWater(Project));
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
        /// <summary>
        /// Serializes haul settings and optional haul results payload to XML.
        /// </summary>
        /// <param name="Project">Project whose haul state is written.</param>
        /// <returns>Haul XML element.</returns>
        static private XElement WriteHaul
            (
            ProjectDocument Project
            )
        {
            XElement HaulEl = new XElement(Ns + "Haul");
            WriteHaulSettingsAttributes(HaulEl, Project.HaulSettings);

            if (Project.HasHaulData && Project.HaulData != null)
            {
                HaulEl.Add(new XElement(Ns + "Payload",
                    HaulDataSerializer.SerializePayload(Project.HaulData.Payload)));
            }

            return HaulEl;
        }

        /// <summary>
        /// Writes haul settings as XML attributes on the Haul element.
        /// </summary>
        /// <param name="HaulEl">Haul element receiving settings attributes.</param>
        /// <param name="Settings">Haul settings to serialize.</param>
        static private void WriteHaulSettingsAttributes
            (
            XElement HaulEl,
            HaulSettings Settings
            )
        {
            HaulEl.Add(
                new XAttribute("scraperCapacityLcy", Settings.ScraperCapacityLcy.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("swellFactor", Settings.SwellFactor.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("bladeWidthMm", Settings.BladeWidthMm.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("maxDepthMm", Settings.MaxDepthMm.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("toleranceMm", Settings.ToleranceMm.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("maxDropHopFt", Settings.MaxDropHopFt.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("minLoadFillPct", Settings.MinLoadFillPct.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("equipmentWidthFt", Settings.EquipmentWidthFt.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("laneOverlap", Settings.LaneOverlap.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("fieldEfficiency", Settings.FieldEfficiency.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("transportMode", Settings.TransportMode),
                new XAttribute("balanceMode", Settings.BalanceMode),
                new XAttribute("zones", Settings.Zones.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("noFlowAligned", Settings.NoFlowAligned ? "true" : "false"),
                new XAttribute("useDesignMainSlopeDirection", Settings.UseDesignMainSlopeDirection ? "true" : "false"),
                new XAttribute("headingDeg", Settings.HeadingDeg.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("loadingSpeedMph", Settings.LoadingSpeedMph.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("haulSpeedMph", Settings.HaulSpeedMph.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("dumpingSpeedMph", Settings.DumpingSpeedMph.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("returnSpeedMph", Settings.ReturnSpeedMph.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("turnSpeedMph", Settings.TurnSpeedMph.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("gradeSpeedMph", Settings.GradeSpeedMph.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("accelTimeS", Settings.AccelTimeS.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("decelTimeS", Settings.DecelTimeS.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("turningCircleFt", Settings.TurningCircleFt.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("turnThresholdDeg", Settings.TurnThresholdDeg.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("noGradeReturn", Settings.NoGradeReturn ? "true" : "false"),
                new XAttribute("pivot", Settings.Pivot ? "true" : "false"),
                new XAttribute("pivotLatitudeDeg", Settings.PivotLatitudeDeg.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("pivotLongitudeDeg", Settings.PivotLongitudeDeg.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("pivotWheelbaseFt", Settings.PivotWheelbaseFt.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("pivotClearanceFt", Settings.PivotClearanceFt.ToString("G17", CultureInfo.InvariantCulture)),
                new XAttribute("pivotMoveCost", Settings.PivotMoveCost.ToString("G17", CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Deserializes haul settings and optional haul results payload from XML.
        /// </summary>
        /// <param name="HaulEl">Haul XML element.</param>
        /// <param name="Project">Project receiving haul state.</param>
        static private void ReadHaul
            (
            XElement HaulEl,
            ProjectDocument Project
            )
        {
            ReadHaulSettings(HaulEl, Project.HaulSettings);

            string? PayloadJson = HaulEl.Element(Ns + "Payload")?.Value;
            if (PayloadJson != null)
            {
                Project.HaulData = HaulDataSerializer.FromPayload(
                    HaulDataSerializer.DeserializePayload(PayloadJson));
            }
        }

        /// <summary>
        /// Deserializes haul settings from XML attributes, applying defaults for missing values.
        /// </summary>
        /// <param name="El">Haul element carrying settings attributes.</param>
        /// <param name="Settings">Settings instance to populate.</param>
        static private void ReadHaulSettings
            (
            XElement El,
            HaulSettings Settings
            )
        {
            HaulSettings Defaults = new HaulSettings();
            Settings.ScraperCapacityLcy = ParseAttrDouble(El, "scraperCapacityLcy", Defaults.ScraperCapacityLcy);
            Settings.SwellFactor = ParseAttrDouble(El, "swellFactor", Defaults.SwellFactor);
            Settings.BladeWidthMm = ParseAttrDouble(El, "bladeWidthMm", Defaults.BladeWidthMm);
            Settings.MaxDepthMm = ParseAttrDouble(El, "maxDepthMm", Defaults.MaxDepthMm);
            Settings.ToleranceMm = ParseAttrDouble(El, "toleranceMm", Defaults.ToleranceMm);
            Settings.MaxDropHopFt = ParseAttrDouble(El, "maxDropHopFt", Defaults.MaxDropHopFt);
            Settings.MinLoadFillPct = ParseAttrDouble(El, "minLoadFillPct", Defaults.MinLoadFillPct);
            Settings.EquipmentWidthFt = ParseAttrDouble(El, "equipmentWidthFt", Defaults.EquipmentWidthFt);
            Settings.LaneOverlap = ParseAttrDouble(El, "laneOverlap", Defaults.LaneOverlap);
            Settings.FieldEfficiency = ParseAttrDouble(El, "fieldEfficiency", Defaults.FieldEfficiency);
            Settings.TransportMode = ParseAttrString(El, "transportMode", Defaults.TransportMode);
            Settings.BalanceMode = ParseAttrString(El, "balanceMode", Defaults.BalanceMode);
            Settings.Zones = ParseAttrInt(El, "zones", Defaults.Zones);
            Settings.NoFlowAligned = ParseBoolAttr(El, "noFlowAligned", Defaults.NoFlowAligned);
            Settings.UseDesignMainSlopeDirection = ParseBoolAttr(El, "useDesignMainSlopeDirection", Defaults.UseDesignMainSlopeDirection);
            Settings.HeadingDeg = ParseAttrDouble(El, "headingDeg", Defaults.HeadingDeg);
            Settings.LoadingSpeedMph = ParseAttrDouble(El, "loadingSpeedMph", Defaults.LoadingSpeedMph);
            Settings.HaulSpeedMph = ParseAttrDouble(El, "haulSpeedMph", Defaults.HaulSpeedMph);
            Settings.DumpingSpeedMph = ParseAttrDouble(El, "dumpingSpeedMph", Defaults.DumpingSpeedMph);
            Settings.ReturnSpeedMph = ParseAttrDouble(El, "returnSpeedMph", Defaults.ReturnSpeedMph);
            Settings.TurnSpeedMph = ParseAttrDouble(El, "turnSpeedMph", Defaults.TurnSpeedMph);
            Settings.GradeSpeedMph = ParseAttrDouble(El, "gradeSpeedMph", Defaults.GradeSpeedMph);
            Settings.AccelTimeS = ParseAttrDouble(El, "accelTimeS", Defaults.AccelTimeS);
            Settings.DecelTimeS = ParseAttrDouble(El, "decelTimeS", Defaults.DecelTimeS);
            Settings.TurningCircleFt = ParseAttrDouble(El, "turningCircleFt", Defaults.TurningCircleFt);
            Settings.TurnThresholdDeg = ParseAttrDouble(El, "turnThresholdDeg", Defaults.TurnThresholdDeg);
            Settings.NoGradeReturn = ParseBoolAttr(El, "noGradeReturn", Defaults.NoGradeReturn);
            Settings.Pivot = ParseBoolAttr(El, "pivot", Defaults.Pivot);
            Settings.PivotLatitudeDeg = ParseAttrDouble(El, "pivotLatitudeDeg", Defaults.PivotLatitudeDeg);
            Settings.PivotLongitudeDeg = ParseAttrDouble(El, "pivotLongitudeDeg", Defaults.PivotLongitudeDeg);
            Settings.PivotWheelbaseFt = ParseAttrDouble(El, "pivotWheelbaseFt", Defaults.PivotWheelbaseFt);
            Settings.PivotClearanceFt = ParseAttrDouble(El, "pivotClearanceFt", Defaults.PivotClearanceFt);
            Settings.PivotMoveCost = ParseAttrDouble(El, "pivotMoveCost", Defaults.PivotMoveCost);
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
                    new XAttribute("water", State.Display.Water ? "true" : "false"),
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
                State.Display.Water = ParseBoolAttr(Toggles, "water", false);
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

        /// <summary>
        /// Serializes water settings and persisted cache payloads to XML (WATER-SPEC Appendix A).
        /// </summary>
        /// <param name="Project">Project whose water state is written.</param>
        /// <returns>Water XML element.</returns>
        static private XElement WriteWater
            (
            ProjectDocument Project
            )
        {
            WaterSettings Settings = Project.Water.Settings;
            XElement WaterEl = new XElement(Ns + "Water",
                new XAttribute("pondingRainfallMm", Settings.PondingRainfallMm.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("pondingCurveNumber", Settings.PondingCurveNumber.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("minimumPondDepthMm", Settings.MinimumPondDepthMm.ToString(CultureInfo.InvariantCulture)));

            if (Project.Water.ExistingSurfaceCache is WaterLayerCacheEntry Existing &&
                Existing.IsValidForDisplay())
                WaterEl.Add(WriteWaterCacheElement("Existing", Existing));

            if (Project.Water.ProposedSurfaceCache is WaterLayerCacheEntry Proposed &&
                Proposed.IsValidForDisplay())
                WaterEl.Add(WriteWaterCacheElement("Proposed", Proposed));

            return WaterEl;
        }

        /// <summary>
        /// Serializes one persisted water cache child element with BASE64 payload text.
        /// </summary>
        /// <param name="LocalName">Existing or Proposed element name.</param>
        /// <param name="Entry">Cache entry to write.</param>
        /// <returns>Cache XML element.</returns>
        static private XElement WriteWaterCacheElement
            (
            string LocalName,
            WaterLayerCacheEntry Entry
            )
        {
            byte[] Blob = Entry.PayloadBlob.Length > 0 ? Entry.PayloadBlob : WaterLayerBlobCodec.Encode(Entry);
            return new XElement(Ns + LocalName,
                new XAttribute("pondingRainfallMm", Entry.PondingRainfallMm.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("pondingCurveNumber", Entry.PondingCurveNumber.ToString(CultureInfo.InvariantCulture)),
                new XElement(Ns + "Payload", WaterLayerBlobCodec.EncodeBase64(Blob)));
        }

        /// <summary>
        /// Deserializes water settings and cache payloads from XML.
        /// </summary>
        /// <param name="El">Water XML element.</param>
        /// <param name="Project">Project receiving water state.</param>
        static private void ReadWater
            (
            XElement El,
            ProjectDocument Project
            )
        {
            Project.Water.Settings.PondingRainfallMm = ParseUIntAttr(El, "pondingRainfallMm", WaterConstants.DEFAULT_PONDING_RAINFALL_MM);
            Project.Water.Settings.PondingCurveNumber = ParseUIntAttr(El, "pondingCurveNumber", WaterConstants.DEFAULT_PONDING_CURVE_NUMBER);
            Project.Water.Settings.MinimumPondDepthMm = ParseUIntAttr(El, "minimumPondDepthMm", WaterConstants.DEFAULT_MINIMUM_POND_DEPTH_MM);

            XElement? ExistingEl = El.Element(Ns + "Existing");
            if (ExistingEl != null)
                Project.Water.ExistingSurfaceCache = ReadWaterCacheElement(Project, ExistingEl, WaterBasisSurfaceKind.Existing);

            XElement? ProposedEl = El.Element(Ns + "Proposed");
            if (ProposedEl != null)
                Project.Water.ProposedSurfaceCache = ReadWaterCacheElement(Project, ProposedEl, WaterBasisSurfaceKind.Proposed);
        }

        /// <summary>
        /// Deserializes one Existing or Proposed cache element.
        /// </summary>
        /// <param name="El">Cache XML element.</param>
        /// <param name="BasisSurface">Basis surface kind for the cache entry.</param>
        /// <returns>Valid cache entry, or null when payload decoding fails.</returns>
        static private WaterLayerCacheEntry? ReadWaterCacheElement
            (
            ProjectDocument Project,
            XElement El,
            WaterBasisSurfaceKind BasisSurface
            )
        {
            string? PayloadText = El.Element(Ns + "Payload")?.Value;
            if (!WaterLayerBlobCodec.TryDecodeBase64(PayloadText ?? string.Empty, out byte[] Blob))
                return null;

            WaterLayerCacheEntry Entry = new WaterLayerCacheEntry
            {
                BasisSurface = BasisSurface,
                PondingRainfallMm = ParseUIntAttr(El, "pondingRainfallMm", WaterConstants.DEFAULT_PONDING_RAINFALL_MM),
                PondingCurveNumber = ParseUIntAttr(El, "pondingCurveNumber", WaterConstants.DEFAULT_PONDING_CURVE_NUMBER),
                PayloadBlob = Blob,
            };

            if (!WaterLayerBlobCodec.TryDecode(Blob, Entry, Project.Water.Settings))
                return null;

            WaterLayerCacheGeography.TryRestoreFromProject(Project, Entry, BasisSurface);
            return Entry.IsValidForDisplay() ? Entry : null;
        }

        /// <summary>
        /// Parses an optional string attribute, returning a default when absent.
        /// </summary>
        /// <param name="El">Element carrying the attribute.</param>
        /// <param name="Name">Attribute local name.</param>
        /// <param name="Default">Value used when the attribute is missing.</param>
        /// <returns>Attribute text or the default.</returns>
        static private string ParseAttrString
            (
            XElement El,
            string Name,
            string Default
            )
        {
            return El.Attribute(Name)?.Value ?? Default;
        }

        /// <summary>
        /// Parses an optional unsigned integer attribute.
        /// </summary>
        /// <param name="El">Element carrying the attribute.</param>
        /// <param name="Name">Attribute local name.</param>
        /// <param name="Default">Value used when the attribute is missing or invalid.</param>
        /// <returns>Parsed unsigned integer or the default.</returns>
        static private uint ParseUIntAttr
            (
            XElement El,
            string Name,
            uint Default
            )
        {
            string? Value = El.Attribute(Name)?.Value;
            return uint.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint Parsed) ? Parsed : Default;
        }
    }
}

using AgGradeDesigner.Core.Features;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Water;

namespace AgGradeDesigner.Core.Models
{
    /// <summary>
    /// Root in-memory model for an AgGrade Designer project file (.agproject zip archive).
    /// </summary>
    public sealed class ProjectDocument
    {
        /// <summary>Application display name written into saved project metadata.</summary>
        public const string APPLICATION_NAME = "AgGrade Designer";

        /// <summary>Application version string written into saved project metadata.</summary>
        public const string APPLICATION_VERSION = "1.0.0";

        /// <summary>Project file extension including the leading dot.</summary>
        public const string FILE_EXTENSION = ".agproject";

        /// <summary>Default file name suggested for unsaved projects.</summary>
        public const string DEFAULT_FILE_NAME = "Untitled.agproject";

        /// <summary>Zip entry path containing the project XML payload.</summary>
        public const string ARCHIVE_DATA_ENTRY = "data.xml";

        /// <summary>XML namespace URI for AgDesign project documents.</summary>
        public const string SCHEMA_NAMESPACE = "https://aggrade.com/schemas/agdesign/1.0";

        /// <summary>Supported AgDesign schema version string.</summary>
        public const string SCHEMA_VERSION = "1.0";

        /// <summary>UTC timestamp when the project was first created.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp of the last modification.</summary>
        public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Loaded survey points and benchmarks, or null when no survey is present.</summary>
        public SurveyModel? Survey { get; set; }

        /// <summary>Original format of the survey data embedded in or linked from the project.</summary>
        public SurveySourceFormat SurveySourceFormat { get; set; } = SurveySourceFormat.None;

        /// <summary>Design slope and earthwork parameters, or null before a design is computed.</summary>
        public DesignParameters? DesignParameters { get; set; }

        /// <summary>Computed design grid, or null before a design is computed.</summary>
        public DesignGrid? DesignGrid { get; set; }

        /// <summary>Haul-planning parameters owned by the open project.</summary>
        public HaulSettings HaulSettings { get; set; } = new HaulSettings();

        /// <summary>Computed haul-planning results, or null before haul is generated.</summary>
        public HaulData? HaulData { get; set; }

        /// <summary>Water settings and persisted existing/proposed water layer caches.</summary>
        public WaterLayerState Water { get; set; } = new WaterLayerState();

        /// <summary>Wells and roads placed on the field.</summary>
        public FieldFeatureSet Features { get; set; } = new FieldFeatureSet();

        /// <summary>Persisted viewer mode, overlays, and camera settings.</summary>
        public ViewerState ViewerState { get; set; } = new ViewerState();

        /// <summary>Tracks whether existing/proposed GPU mesh geometry needs rebuilding.</summary>
        public TopoGeometryDirtyState TopoGeometryDirty { get; } = new TopoGeometryDirtyState();

        /// <summary>Path to the on-disk project file, or null for unsaved projects.</summary>
        public string? FilePath { get; set; }

        /// <summary>True when in-memory changes have not been saved to disk.</summary>
        public bool IsModified { get; set; }

        /// <summary>True when the project contains at least one survey point.</summary>
        public bool HasSurvey => (Survey != null) &&
            ((Survey.InteriorPoints.Count > 0) || (Survey.BoundaryPoints.Count > 0));

        /// <summary>True when both design parameters and grid are present.</summary>
        public bool HasDesign => (DesignGrid != null) && (DesignParameters != null);

        /// <summary>True when haul data is present and valid for display.</summary>
        public bool HasHaulData => HaulData?.IsValid == true;

        /// <summary>Project name for UI display, or "Untitled" when no path is set.</summary>
        public string DisplayName
        {
            get
            {
                // Unsaved projects show a fixed placeholder instead of an empty title.
                if (string.IsNullOrWhiteSpace(FilePath))
                    return "Untitled";
                return System.IO.Path.GetFileNameWithoutExtension(FilePath);
            }
        }

        /// <summary>Display name with an asterisk suffix when the project is modified.</summary>
        public string TitleBarName => IsModified ? (DisplayName + "*") : DisplayName;

        /// <summary>
        /// Marks the project as modified and updates <see cref="ModifiedUtc"/>.
        /// </summary>
        public void MarkModified
            (
            )
        {
            // Stamp modification time so save dialogs and sync logic see fresh metadata.
            IsModified = true;
            ModifiedUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Clears the modified flag after a successful save.
        /// </summary>
        public void MarkClean
            (
            )
        {
            // Clear dirty flag after a successful write without altering content.
            IsModified = false;
        }

        /// <summary>
        /// Resets all project content to an empty unsaved state.
        /// </summary>
        public void ResetToEmpty
            (
            )
        {
            // Drop all loaded content and restore default viewer state for a new project.
            Survey = null;
            SurveySourceFormat = SurveySourceFormat.None;
            DesignParameters = null;
            DesignGrid = null;
            HaulSettings = new HaulSettings();
            HaulData = null;
            Water = new WaterLayerState();
            Features = new FieldFeatureSet();
            FilePath = null;
            IsModified = false;
            CreatedUtc = DateTime.UtcNow;
            ModifiedUtc = DateTime.UtcNow;
            ViewerState = new ViewerState();
            TopoGeometryDirty.ResetForEmptyProject();
        }
    }
}

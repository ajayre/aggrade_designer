using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Export
{
    /// <summary>
    /// Validates a project before writing an <c>.agcontrol</c> file (EXPORT-SPEC §7.1, Phase 2).
    /// </summary>
    static public class AgControlExportValidator
    {
        /// <summary>User-facing error when no proposed surface is present.</summary>
        public const string ERROR_NO_PROPOSED_SURFACE =
            "Export to AgGrade Control requires a proposed surface. Create a design first.";

        /// <summary>User-facing error when the design grid has no valid bins.</summary>
        public const string ERROR_NO_VALID_BINS =
            "The design grid has no valid bins to export.";

        /// <summary>User-facing error when haul data claims validity but has no ordered passes.</summary>
        public const string ERROR_HAUL_EMPTY_PASSES =
            "Haul data is present but contains no ordered cut-fill passes.";

        /// <summary>Warning when exporting without a haul plan.</summary>
        public const string WARNING_NO_HAUL =
            "No haul plan is present. Topology and benchmarks will be exported; haul tables will be empty.";

        /// <summary>Warning when the survey has no benchmarks.</summary>
        public const string WARNING_NO_BENCHMARKS =
            "No survey benchmarks are present. The Benchmarks table will be empty.";

        /// <summary>Warning when haul passes exist but guidance waypoints are missing.</summary>
        public const string WARNING_NO_GUIDANCE =
            "Haul passes are present but guidance waypoints are missing. HaulPaths will be empty.";

        /// <summary>Warning when a guidance PassNo does not match a route id.</summary>
        public const string WARNING_GUIDANCE_PASS_OUT_OF_RANGE =
            "One or more guidance waypoints reference a pass number outside the ordered haul plan.";

        /// <summary>
        /// Validates whether the project can be exported to <c>.agcontrol</c>.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <returns>Validation result with errors and warnings.</returns>
        /// <exception cref="ArgumentNullException">Project is null.</exception>
        static public AgControlExportValidationResult Validate
            (
            ProjectDocument Project
            )
        {
            if (Project == null)
                throw new ArgumentNullException(nameof(Project));

            List<string> Errors = new List<string>();
            List<string> Warnings = new List<string>();

            if (!Project.HasDesign || (Project.DesignGrid == null))
            {
                Errors.Add(ERROR_NO_PROPOSED_SURFACE);
                return new AgControlExportValidationResult(Errors, Warnings);
            }

            DesignGrid Grid = Project.DesignGrid;
            int ValidBinCount = CountValidBins(Grid);
            if (ValidBinCount <= 0)
                Errors.Add(ERROR_NO_VALID_BINS);

            if ((Project.Survey == null) || (Project.Survey.Benchmarks.Count == 0))
                Warnings.Add(WARNING_NO_BENCHMARKS);

            if (!Project.HasHaulData || (Project.HaulData == null))
            {
                // intent: haul is optional — warn only, do not block (§7.1.3)
                Warnings.Add(WARNING_NO_HAUL);
                return new AgControlExportValidationResult(Errors, Warnings);
            }

            HaulData Haul = Project.HaulData;
            if (Haul.Payload.OrderedPasses.Count <= 0)
            {
                Errors.Add(ERROR_HAUL_EMPTY_PASSES);
                return new AgControlExportValidationResult(Errors, Warnings);
            }

            if (Haul.Payload.GuidancePoints.Count == 0)
                Warnings.Add(WARNING_NO_GUIDANCE);
            else if (HasGuidanceOutsidePassRange(Haul))
                Warnings.Add(WARNING_GUIDANCE_PASS_OUT_OF_RANGE);

            return new AgControlExportValidationResult(Errors, Warnings);
        }

        /// <summary>
        /// Counts valid design-grid bins.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Number of valid bins.</returns>
        static private int CountValidBins
            (
            DesignGrid Grid
            )
        {
            int Count = 0;
            foreach (GridBinData Bin in Grid.Bins)
            {
                if (Bin.Valid)
                    Count++;
            }

            return Count;
        }

        /// <summary>
        /// Returns whether any guidance waypoint references a PassNo outside 1…passCount.
        /// </summary>
        /// <param name="Haul">Haul payload with passes and guidance.</param>
        /// <returns>True when at least one PassNo is out of range.</returns>
        static private bool HasGuidanceOutsidePassRange
            (
            HaulData Haul
            )
        {
            int PassCount = Haul.Payload.OrderedPasses.Count;
            foreach (HaulGuidancePointDto Point in Haul.Payload.GuidancePoints)
            {
                if ((Point.PassNo < 1) || (Point.PassNo > PassCount))
                    return true;
            }

            return false;
        }
    }
}

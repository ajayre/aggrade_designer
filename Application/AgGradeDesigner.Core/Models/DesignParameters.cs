namespace AgGradeDesigner.Core.Models
{
    /// <summary>
    /// Slope, cut-fill ratio, and earthwork import/export targets for field design.
    /// </summary>
    public sealed class DesignParameters
    {
        /// <summary>Default main slope direction in degrees clockwise from north.</summary>
        public const double DEFAULT_MAIN_SLOPE_DIRECTION_DEG = 90.0;

        /// <summary>Main slope direction in degrees clockwise from north.</summary>
        public double MainSlopeDirectionDeg { get; set; } = DEFAULT_MAIN_SLOPE_DIRECTION_DEG;

        /// <summary>Main slope grade as percent (metres per metre times 100).</summary>
        public double MainSlopePercent { get; set; }

        /// <summary>Cross slope grade as percent perpendicular to the main slope.</summary>
        public double CrossSlopePercent { get; set; }

        /// <summary>Target cut volume divided by fill volume.</summary>
        public double CutFillRatio { get; set; } = FieldConstants.CUT_FILL_RATIO;

        /// <summary>Material imported onto the field in cubic yards.</summary>
        public double ImportToFieldCy { get; set; }

        /// <summary>Material exported from the field in cubic yards.</summary>
        public double ExportFromFieldCy { get; set; }

        /// <summary>
        /// Creates a shallow copy of all design parameter values.
        /// </summary>
        /// <returns>New instance with identical scalar settings.</returns>
        public DesignParameters Clone
            (
            )
        {
            // Duplicate scalar settings so UI edits do not mutate the saved project snapshot.
            return new DesignParameters
            {
                MainSlopeDirectionDeg = MainSlopeDirectionDeg,
                MainSlopePercent = MainSlopePercent,
                CrossSlopePercent = CrossSlopePercent,
                CutFillRatio = CutFillRatio,
                ImportToFieldCy = ImportToFieldCy,
                ExportFromFieldCy = ExportFromFieldCy
            };
        }
    }
}

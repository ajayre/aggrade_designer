namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Serializable haul plan time from production guidance timing, or the older time model when guidance timing is absent.
    /// </summary>
    public sealed class HaulGuidanceTimeDto
    {
        /// <summary>
        /// Total plan time in seconds. Production guidance stores deterministic seconds
        /// (motion plus events), the same figure HaulBuilder reports as deterministic hours.
        /// </summary>
        public double TotalSeconds { get; set; }

        /// <summary>Loading time in seconds.</summary>
        public double LoadingSeconds { get; set; }

        /// <summary>Dumping time in seconds.</summary>
        public double DumpingSeconds { get; set; }

        /// <summary>Grading time in seconds.</summary>
        public double GradingSeconds { get; set; }

        /// <summary>Hauling time in seconds.</summary>
        public double HaulingSeconds { get; set; }

        /// <summary>Returning time in seconds.</summary>
        public double ReturningSeconds { get; set; }

        /// <summary>Turning time in seconds.</summary>
        public double TurningSeconds { get; set; }

        /// <summary>Non-productive overhead time in seconds.</summary>
        public double OtherSeconds { get; set; }

        /// <summary>Count of significant turns.</summary>
        public int TurnCount { get; set; }
    }
}

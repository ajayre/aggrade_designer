namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Serializable cut/fill pair summary for one haul route step.
    /// </summary>
    public sealed class HaulCutFillPairDto
    {
        /// <summary>1-based route order index.</summary>
        public int Order { get; set; }

        /// <summary>Carried load in bank cubic yards for this route.</summary>
        public double LoadBcy { get; set; }

        /// <summary>Haul distance in metres for this route.</summary>
        public double HaulDistanceM { get; set; }

        /// <summary>Cumulative cut BCY completed before this route starts.</summary>
        public double TotalCutBcyBefore { get; set; }

        /// <summary>Cumulative fill BCY completed before this route starts.</summary>
        public double TotalFillBcyBefore { get; set; }

        /// <summary>Estimated time to complete this route in seconds.</summary>
        public double RouteTimeSeconds { get; set; }
    }
}

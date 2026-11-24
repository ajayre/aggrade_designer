namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Maps a production guidance job onto the planner pass indexes the haul view scrubs.
    /// </summary>
    internal static class HaulDriveOrder
    {
        /// <summary>
        /// Reads drive order as planner-pass indexes.
        /// Drive step X is <c>DriveOrder[X - 1]</c> into <c>Job.Trips</c>, and that trip's
        /// <c>TripNumber</c> is the planner pass.
        /// </summary>
        /// <param name="Job">Guidance job whose trips stay in planner order.</param>
        /// <param name="PassCount">Number of planner passes the view can draw.</param>
        /// <returns>0-based planner pass indexes in drive order. Trips with no planner pass are omitted.</returns>
        public static List<int> PlannerIndexes
            (
            HaulBuilder.TripGuidance.JobGuidance Job,
            int PassCount
            )
        {
            List<int> Indexes = new List<int>(Job.DriveOrder.Count);
            foreach (int TripIndex in Job.DriveOrder)
            {
                if (TripIndex < 0 || TripIndex >= Job.Trips.Count)
                    continue;

                // intent: TripNumber is the planner trip, not the position in the drive sequence
                int PlannerIndex = Job.Trips[TripIndex].TripNumber - 1;
                if (PlannerIndex < 0 || PlannerIndex >= PassCount)
                    continue;

                Indexes.Add(PlannerIndex);
            }

            return Indexes;
        }
    }
}

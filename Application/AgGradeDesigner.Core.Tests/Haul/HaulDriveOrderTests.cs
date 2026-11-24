using AgGradeDesigner.Core.Haul;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Verifies the haul view steps through drive order rather than planner order.
    /// </summary>
    public sealed class HaulDriveOrderTests
    {
        /// <summary>
        /// Verifies drive step 2 selects planner trip 123.
        /// </summary>
        [Fact]
        public void PlannerIndexes_DriveStepSelectsPlannerTripNumber
            (
            )
        {
            HaulBuilder.TripGuidance.JobGuidance Job = JobWithTrips(
                Trip(2, 1),
                Trip(123, 2));
            Job.DriveOrder.Clear();
            Job.DriveOrder.Add(1);
            Job.DriveOrder.Add(0);

            List<int> Indexes = HaulDriveOrder.PlannerIndexes(Job, PassCount: 123);

            Assert.Equal(new[] { 122, 1 }, Indexes);
        }

        /// <summary>
        /// Verifies the scrub step and the volumes before it follow the stored drive order.
        /// </summary>
        [Fact]
        public void DriveOrder_ShowsTrip123AtDriveStep2
            (
            )
        {
            HaulData Data = new HaulData();
            for (int Index = 0; Index < 123; Index++)
            {
                Data.Payload.OrderedPasses.Add(new HaulPassDto { LoadBcy = 1.0 });
                Data.Payload.CutFillPairs.Add(Pair(Index + 1, 1.0));
            }

            Data.Payload.CutFillPairs[0].LoadBcy = 10.0;
            Data.Payload.CutFillPairs[122].LoadBcy = 30.0;
            Data.Payload.DriveOrder.Add(122);
            Data.Payload.DriveOrder.Add(0);

            Assert.Equal(123, Data.PlannerPassNumberForDriveStep(0));
            Assert.Equal(1, Data.PlannerPassNumberForDriveStep(1));

            IReadOnlyList<HaulCutFillPairDto> Driven = Data.ResolveCutFillPairsInDriveOrder();
            Assert.Equal(30.0, Driven[0].LoadBcy, precision: 3);
            Assert.Equal(0.0, Driven[0].TotalCutBcyBefore, precision: 3);
            Assert.Equal(10.0, Driven[1].LoadBcy, precision: 3);
            Assert.Equal(30.0, Driven[1].TotalCutBcyBefore, precision: 3);
        }

        /// <summary>
        /// Builds one trip with a planner number and a drive position.
        /// </summary>
        /// <param name="TripNumber">1-based planner trip number.</param>
        /// <param name="ExecutionIndex">1-based drive position.</param>
        /// <returns>A trip course with those two numbers.</returns>
        private static HaulBuilder.TripGuidance.TripCourse Trip
            (
            int TripNumber,
            int ExecutionIndex
            )
        {
            HaulBuilder.TripGuidance.TripCourse Course = new HaulBuilder.TripGuidance.TripCourse(
                TripNumber,
                HaulBuilder.HaulStage.Flow,
                new List<HaulBuilder.TripGuidance.CourseSegment>(),
                new List<HaulBuilder.TripGuidance.GuidancePoint>(),
                new List<HaulBuilder.TripGuidance.ConnectorNote>(),
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                new HaulBuilder.TripGuidance.GradeLedger(0.0, 0.0, 0.0));
            Course.ExecutionIndex = ExecutionIndex;
            return Course;
        }

        /// <summary>
        /// Builds a job whose trip list is the supplied planner order.
        /// </summary>
        /// <param name="Trips">Trips in planner order.</param>
        /// <returns>A job with an empty drive order.</returns>
        private static HaulBuilder.TripGuidance.JobGuidance JobWithTrips
            (
            params HaulBuilder.TripGuidance.TripCourse[] Trips
            )
        {
            return new HaulBuilder.TripGuidance.JobGuidance(
                Trips.ToList(),
                new double[1, 1],
                null,
                0.8,
                new Dictionary<int, double[,]>(),
                0.0,
                0.0,
                new List<int>(),
                0,
                0,
                0);
        }

        /// <summary>
        /// Builds one statistics row.
        /// </summary>
        /// <param name="Order">1-based planner trip number.</param>
        /// <param name="LoadBcy">Load on that trip.</param>
        /// <returns>A pair row.</returns>
        private static HaulCutFillPairDto Pair
            (
            int Order,
            double LoadBcy
            )
        {
            return new HaulCutFillPairDto
            {
                Order = Order,
                LoadBcy = LoadBcy
            };
        }
    }
}

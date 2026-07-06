using AgGradeDesigner.Core.Models;



using NetTopologySuite.Geometries;



namespace AgGradeDesigner.Core.Ai.SurfaceTools

{

    /// <summary>

    /// Parameters for building a raised road surface above the proposed design surface.

    /// </summary>

    public readonly struct RoadBuildParameters

    {

        /// <summary>Road centerline in UTM metres.</summary>

        public LineString Centerline { get; init; }



        /// <summary>Full flat crown or ditch bottom width in metres.</summary>

        public double CrownWidthM { get; init; }



        /// <summary>
        /// Signed vertical offset from the proposed surface at the centerline in metres.
        /// Positive raises a road; negative lowers a ditch.
        /// </summary>
        public double VerticalOffsetM { get; init; }

        /// <summary>True when the corridor lowers the surface (ditch); false for a raised road.</summary>
        public bool IsCut => VerticalOffsetM < 0.0;

        /// <summary>Absolute vertical offset magnitude in metres.</summary>
        public double VerticalOffsetAbsM => Math.Abs(VerticalOffsetM);



        /// <summary>Optional side slope outside the crown.</summary>

        public RoadSideSlopeSpec? SideSlope { get; init; }



        /// <summary>Longitudinal grade along the centerline in m/m.</summary>

        public double LongitudinalGradeMm { get; init; }



        /// <summary>Crossfall from crown centerline in m/m; positive slopes to the left.</summary>

        public double CrownCrossfallMm { get; init; }

        /// <summary>
        /// When true, offset the centerline inward from the field boundary so the outer crown
        /// edge is flush with the boundary while following curved edges.
        /// </summary>
        public bool AlignCrownToFieldEdge { get; init; }

    }



    /// <summary>

    /// Result of building a road on a preview grid.

    /// </summary>

    public readonly struct RoadBuildResult

    {

        /// <summary>Number of bins modified by the road build.</summary>

        public int AffectedBinCount { get; init; }



        /// <summary>Registered or supplied road identifier.</summary>

        public string RoadId { get; init; }



        /// <summary>Corridor polygon covering the road influence area.</summary>

        public Polygon Corridor { get; init; }



        /// <summary>Centerline length in metres.</summary>

        public double CenterlineLengthM { get; init; }

        /// <summary>Signed vertical offset applied at the centerline in metres.</summary>
        public double VerticalOffsetM { get; init; }

        /// <summary>Fill volume added by this road operation in cubic metres.</summary>
        public double OperationFillM3 { get; init; }

        /// <summary>Cut volume added by this road operation in cubic metres.</summary>
        public double OperationCutM3 { get; init; }

    }



    /// <summary>

    /// Deterministic road-surface mutators for raised field roads.

    /// </summary>

    static public class RoadMutator

    {

        /// <summary>Elevation delta below which a bin is not counted as modified.</summary>

        private const double BIN_CHANGE_EPSILON_M = 1e-6;



        /// <summary>Offset from bin index to bin centre in cell units.</summary>

        private const double BIN_CELL_CENTRE_OFFSET = 0.5;



        /// <summary>

        /// Builds a raised road surface on the preview grid.

        /// </summary>

        /// <param name="Grid">Preview design grid.</param>

        /// <param name="Parameters">Road build parameters.</param>

        /// <param name="RoadId">Road identifier echoed in the result.</param>

        /// <param name="Project">Optional project document used to resolve survey boundaries.</param>

        /// <returns>Road build summary.</returns>

        static public RoadBuildResult BuildRoad

            (

            DesignGrid Grid,

            RoadBuildParameters Parameters,

            string RoadId,

            ProjectDocument? Project = null

            )

        {

            LineString Centerline = Parameters.Centerline;
            if (Parameters.AlignCrownToFieldEdge &&
                FieldBoundaryGeometry.TryResolveBoundaryRing(Project, Grid, out LinearRing? BoundaryRing, out Coordinate FieldCentroid) &&
                BoundaryRing != null)
            {
                Centerline = RoadCenterlineBoundaryAlign.AlignCrownToFieldEdge(
                    Grid,
                    BoundaryRing,
                    FieldCentroid,
                    Centerline,
                    Parameters.CrownWidthM,
                    Enabled: true);
            }

            double SnapReachM = Math.Max(Parameters.CrownWidthM, Grid.BinSizeM * 2.0);
            Centerline = RoadCenterlineFieldSnap.SnapToValidField(
                Grid,
                Centerline,
                SnapReachM);
            Centerline = RoadCenterlineSmoothing.CreateMeasurementLine(Centerline, Grid.BinSizeM);
            RoadBuildParameters AdjustedParameters = new RoadBuildParameters
            {
                Centerline = Centerline,
                CrownWidthM = Parameters.CrownWidthM,
                VerticalOffsetM = Parameters.VerticalOffsetM,
                SideSlope = Parameters.SideSlope,
                LongitudinalGradeMm = Parameters.LongitudinalGradeMm,
                CrownCrossfallMm = Parameters.CrownCrossfallMm,
                AlignCrownToFieldEdge = Parameters.AlignCrownToFieldEdge
            };

            Dictionary<(int X, int Y), double> BaselineProposedM = CaptureBaselineProposedM(Grid);
            double HalfCrownM = AdjustedParameters.CrownWidthM * 0.5;
            double MaxInfluenceM = ComputeMaxInfluenceDistanceM(AdjustedParameters);
            double BinAreaM2 = Grid.BinSizeM * Grid.BinSizeM;
            int Affected = 0;
            double OperationFillM3 = 0.0;
            double OperationCutM3 = 0.0;

            foreach (GridBinData Bin in Grid.Bins)

            {

                if (!Bin.Valid)

                    continue;



                double EastingM = Grid.OriginEastingM + (Bin.X + BIN_CELL_CENTRE_OFFSET) * Grid.BinSizeM;

                double NorthingM = Grid.OriginNorthingM + (Bin.Y + BIN_CELL_CENTRE_OFFSET) * Grid.BinSizeM;

                SurfacePolylineHelper.MeasureRelativeToCenterline(

                    AdjustedParameters.Centerline,

                    EastingM,

                    NorthingM,

                    out double ChainageM,

                    out double PerpendicularDistanceM,

                    out double SignedOffsetM);



                if (PerpendicularDistanceM > MaxInfluenceM + Grid.BinSizeM * 0.75)

                    continue;



                double ProposedAtBinM = BaselineProposedM[(Bin.X, Bin.Y)];

                if (!TryComputeTargetElevation(

                        Grid,

                        AdjustedParameters,

                        BaselineProposedM,

                        ChainageM,

                        SignedOffsetM,

                        HalfCrownM,

                        ProposedAtBinM,

                        out double TargetElevationM))

                    continue;



                double Before = Bin.ProposedM;

                if (AdjustedParameters.IsCut)
                {
                    if (TargetElevationM < Bin.ProposedM)
                        Bin.ProposedM = TargetElevationM;
                }
                else if (TargetElevationM > Bin.ProposedM)
                {
                    Bin.ProposedM = TargetElevationM;
                }



                if (Math.Abs(Bin.ProposedM - Before) > BIN_CHANGE_EPSILON_M)
                {
                    Affected++;
                    double Delta = Bin.ProposedM - Before;
                    if (Delta > BIN_CHANGE_EPSILON_M)
                        OperationFillM3 += Delta * BinAreaM2;
                    else if (Delta < -BIN_CHANGE_EPSILON_M)
                        OperationCutM3 += -Delta * BinAreaM2;
                }
            }

            Grid.InvalidateBinIndex();
            Polygon Corridor = SurfacePolylineHelper.BuildCorridorPolygon(
                AdjustedParameters.Centerline,
                Math.Max(AdjustedParameters.CrownWidthM, MaxInfluenceM * 2.0));

            return new RoadBuildResult
            {
                AffectedBinCount = Affected,
                RoadId = RoadId,
                Corridor = Corridor,
                CenterlineLengthM = AdjustedParameters.Centerline.Length,
                VerticalOffsetM = AdjustedParameters.VerticalOffsetM,
                OperationFillM3 = OperationFillM3,
                OperationCutM3 = OperationCutM3
            };
        }



        /// <summary>

        /// Computes cut/fill volumes limited to bins inside a road corridor polygon.

        /// </summary>

        /// <param name="Grid">Design grid.</param>

        /// <param name="Corridor">Road corridor polygon.</param>

        /// <returns>Cut/fill volumes within the corridor.</returns>

        static public SurfaceCutFillVolumes CalculateRoadVolumes

            (

            DesignGrid Grid,

            Polygon Corridor

            )

        {

            double CutM3 = 0.0;

            double FillM3 = 0.0;

            double BinAreaM2 = Grid.BinSizeM * Grid.BinSizeM;



            foreach (GridBinData Bin in Grid.Bins)

            {

                if (!Bin.Valid)

                    continue;



                if (!SurfacePolygonHelper.IsBinIncluded(Grid, Bin, Corridor))

                    continue;



                double Delta = Bin.ProposedM - Bin.ExistingM;

                if (Delta > BIN_CHANGE_EPSILON_M)

                    FillM3 += Delta * BinAreaM2;

                else if (Delta < -BIN_CHANGE_EPSILON_M)

                    CutM3 += -Delta * BinAreaM2;

            }



            return new SurfaceCutFillVolumes

            {

                CutM3 = CutM3,

                FillM3 = FillM3

            };

        }



        /// <summary>

        /// Captures proposed elevations before road geometry is applied.

        /// </summary>

        /// <param name="Grid">Design grid.</param>

        /// <returns>Baseline proposed elevation keyed by bin coordinates.</returns>

        static private Dictionary<(int X, int Y), double> CaptureBaselineProposedM

            (

            DesignGrid Grid

            )

        {

            Dictionary<(int X, int Y), double> BaselineProposedM = new Dictionary<(int X, int Y), double>();

            foreach (GridBinData Bin in Grid.Bins)

            {

                if (!Bin.Valid)

                    continue;



                BaselineProposedM[(Bin.X, Bin.Y)] = Bin.ProposedM;

            }



            return BaselineProposedM;

        }



        /// <summary>

        /// Computes the target road elevation for a bin relative to the centerline.

        /// </summary>

        /// <param name="Grid">Design grid.</param>

        /// <param name="Parameters">Road build parameters.</param>

        /// <param name="BaselineProposedM">Proposed elevations captured before road build.</param>

        /// <param name="ChainageM">Chainage along the centerline in metres.</param>

        /// <param name="SignedOffsetM">Signed perpendicular offset from the centerline.</param>

        /// <param name="HalfCrownM">Half of the crown width in metres.</param>

        /// <param name="ProposedAtBinM">Baseline proposed elevation at the bin centroid in metres.</param>

        /// <param name="TargetElevationM">Computed target elevation.</param>

        /// <returns>True when the bin lies inside the road influence area.</returns>

        static private bool TryComputeTargetElevation

            (

            DesignGrid Grid,

            RoadBuildParameters Parameters,

            IReadOnlyDictionary<(int X, int Y), double> BaselineProposedM,

            double ChainageM,

            double SignedOffsetM,

            double HalfCrownM,

            double ProposedAtBinM,

            out double TargetElevationM

            )

        {

            TargetElevationM = 0.0;

            double AbsOffsetM = Math.Abs(SignedOffsetM);

            double CenterlineProposedM = SurfacePolylineHelper.SampleBaselineProposedAtChainage(

                Grid,

                Parameters.Centerline,

                ChainageM,

                BaselineProposedM);

            double CenterlineTargetM = CenterlineProposedM +

                Parameters.VerticalOffsetM +

                Parameters.LongitudinalGradeMm * ChainageM;



            if (AbsOffsetM <= HalfCrownM)

            {

                TargetElevationM = CenterlineTargetM - Parameters.CrownCrossfallMm * SignedOffsetM;

                return true;

            }



            if (Parameters.SideSlope == null)

                return false;



            if (!Parameters.SideSlope.Value.AppliesToSignedOffset(SignedOffsetM))

                return false;



            double CrownEdgeTargetM = CenterlineTargetM -

                Parameters.CrownCrossfallMm * Math.Sign(SignedOffsetM) * HalfCrownM;

            double OffsetBeyondCrownM = AbsOffsetM - HalfCrownM;

            TargetElevationM = Parameters.SideSlope.Value.ElevationBeyondFlatEdge(

                CrownEdgeTargetM,

                OffsetBeyondCrownM,

                Parameters.IsCut);



            if (Parameters.IsCut)
            {
                if (TargetElevationM >= ProposedAtBinM - BIN_CHANGE_EPSILON_M)
                    return false;
            }
            else if (TargetElevationM <= ProposedAtBinM + BIN_CHANGE_EPSILON_M)
            {
                return false;
            }



            return true;

        }



        /// <summary>

        /// Estimates the maximum perpendicular influence distance for the road shape.

        /// </summary>

        /// <param name="Parameters">Road build parameters.</param>

        /// <returns>Maximum perpendicular distance in metres.</returns>

        static private double ComputeMaxInfluenceDistanceM

            (

            RoadBuildParameters Parameters

            )

        {

            double HalfCrownM = Parameters.CrownWidthM * 0.5;

            if (Parameters.SideSlope == null)

                return HalfCrownM;



            double SideReachM = Parameters.VerticalOffsetAbsM * Parameters.SideSlope.Value.HorizontalToVertical;

            return HalfCrownM + Math.Max(SideReachM, Parameters.CrownWidthM);

        }

    }

}



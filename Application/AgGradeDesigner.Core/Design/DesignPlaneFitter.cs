using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Design
{
    /// <summary>
    /// Fits a design plane to imported or legacy grid bin elevations and derives slope metadata.
    /// </summary>
    internal static class DesignPlaneFitter
    {
        /// <summary>Multiplier converting gradient magnitude to slope percent.</summary>
        private const double SLOPE_PERCENT_SCALE = 100.0;

        /// <summary>Minimum non-zero gradient magnitude for heading calculation.</summary>
        private const double GRADIENT_MAGNITUDE_EPSILON = 1e-18;

        /// <summary>Default downhill heading when the fitted plane is flat.</summary>
        private const double DEFAULT_DOWNHILL_HEADING_DEG = 0.0;

        /// <summary>Singular-matrix pivot threshold for 3×3 Gaussian elimination.</summary>
        private const double MATRIX_SINGULAR_EPSILON = 1e-20;

        /// <summary>Linear-system dimension for the plane normal equations.</summary>
        private const int NORMAL_EQUATION_DIM = 3;

        /// <summary>Minimum valid bins required before attempting a full plane solve.</summary>
        private const int MIN_PLANE_FIT_BIN_COUNT = 3;

        /// <summary>
        /// Fits plane coefficients and composite slope metadata onto a populated design grid.
        /// </summary>
        /// <param name="Grid">Design grid whose valid bins define the proposed surface.</param>
        public static void ApplyToGrid
            (
            DesignGrid Grid
            )
        {
            Grid.EnsureCentroidsFromUtm();
            List<(double DeltaEastingM, double DeltaNorthingM, double ProposedM, double Lat, double Lon)> Samples =
                CollectFitSamples(Grid);

            if (Samples.Count == 0)
                return;

            double MeanLat = Samples.Average(S => S.Lat);
            double MeanLon = Samples.Average(S => S.Lon);

            if (!TryFitPlane(Samples, out double PlaneA, out double PlaneB, out double PlaneC))
            {
                PlaneA = 0.0;
                PlaneB = 0.0;
                PlaneC = Samples.Average(S => S.ProposedM);
            }

            CompositeSlopeAndHeading(PlaneA, PlaneB, out double CompositeSlopePercent, out double CompositeHeadingDeg);
            (double TotalCutCy, double TotalFillCy) = ComputeCutFillVolumes(Grid, PlaneA, PlaneB, PlaneC);

            Grid.PlaneA = PlaneA;
            Grid.PlaneB = PlaneB;
            Grid.PlaneC = PlaneC;
            Grid.MeanLat = MeanLat;
            Grid.MeanLon = MeanLon;
            Grid.CompositeSlopePercent = CompositeSlopePercent;
            Grid.CompositeSlopeHeadingDeg = CompositeHeadingDeg;
            Grid.TotalCutCy = TotalCutCy;
            Grid.TotalFillCy = TotalFillCy;
        }

        /// <summary>
        /// Builds design parameters from a grid that already has fitted plane metadata.
        /// </summary>
        /// <param name="Grid">Design grid with composite slope fields populated.</param>
        /// <returns>Parameters suitable for UI display and contour thresholds.</returns>
        public static DesignParameters InferParametersFromGrid
            (
            DesignGrid Grid
            )
        {
            // intent: AGD files carry no explicit main/cross split; treat the composite grade as main slope
            return new DesignParameters
            {
                MainSlopePercent = Grid.CompositeSlopePercent,
                MainSlopeDirectionDeg = Grid.CompositeSlopeHeadingDeg,
                CrossSlopePercent = 0.0
            };
        }

        /// <summary>
        /// Collects UTM-relative samples from valid bins for least-squares plane fitting.
        /// </summary>
        /// <param name="Grid">Design grid with bin centroids and proposed elevations.</param>
        /// <returns>Relative easting/northing samples with proposed elevation and centroid coordinates.</returns>
        static private List<(double DeltaEastingM, double DeltaNorthingM, double ProposedM, double Lat, double Lon)> CollectFitSamples
            (
            DesignGrid Grid
            )
        {
            List<(double DeltaEastingM, double DeltaNorthingM, double ProposedM, double Lat, double Lon)> Samples =
                new List<(double DeltaEastingM, double DeltaNorthingM, double ProposedM, double Lat, double Lon)>(Grid.Bins.Count);

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                UTM.UTMCoordinate Utm = UTM.FromLatLon(Bin.CentroidLat, Bin.CentroidLon);
                Samples.Add((
                    Utm.Easting - Grid.OriginEastingM,
                    Utm.Northing - Grid.OriginNorthingM,
                    Bin.ProposedM,
                    Bin.CentroidLat,
                    Bin.CentroidLon));
            }

            return Samples;
        }

        /// <summary>
        /// Solves z = a·ΔE + b·ΔN + c by least squares over bin proposed elevations.
        /// </summary>
        /// <param name="Samples">Relative UTM samples with proposed elevations.</param>
        /// <param name="PlaneA">Output dz/dE gradient.</param>
        /// <param name="PlaneB">Output dz/dN gradient.</param>
        /// <param name="PlaneC">Output intercept at the grid origin.</param>
        /// <returns>True when a stable plane was solved.</returns>
        static private bool TryFitPlane
            (
            IReadOnlyList<(double DeltaEastingM, double DeltaNorthingM, double ProposedM, double Lat, double Lon)> Samples,
            out double PlaneA,
            out double PlaneB,
            out double PlaneC
            )
        {
            PlaneA = 0.0;
            PlaneB = 0.0;
            PlaneC = 0.0;

            if (Samples.Count < MIN_PLANE_FIT_BIN_COUNT)
            {
                if (Samples.Count > 0)
                    PlaneC = Samples.Average(S => S.ProposedM);

                return false;
            }

            double SumXX = 0.0;
            double SumXY = 0.0;
            double SumYY = 0.0;
            double SumX = 0.0;
            double SumY = 0.0;
            double SumXz = 0.0;
            double SumYz = 0.0;
            double SumZ = 0.0;
            int Count = Samples.Count;

            foreach ((double DeltaEastingM, double DeltaNorthingM, double ProposedM, _, _) in Samples)
            {
                SumXX += DeltaEastingM * DeltaEastingM;
                SumXY += DeltaEastingM * DeltaNorthingM;
                SumYY += DeltaNorthingM * DeltaNorthingM;
                SumX += DeltaEastingM;
                SumY += DeltaNorthingM;
                SumXz += DeltaEastingM * ProposedM;
                SumYz += DeltaNorthingM * ProposedM;
                SumZ += ProposedM;
            }

            if (!TrySolve3x3(
                    SumXX,
                    SumXY,
                    SumX,
                    SumYY,
                    SumY,
                    Count,
                    SumXz,
                    SumYz,
                    SumZ,
                    out PlaneA,
                    out PlaneB,
                    out PlaneC))
            {
                PlaneC = Samples.Average(S => S.ProposedM);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Computes cut/fill cubic-yard totals from existing versus the fitted design plane.
        /// </summary>
        /// <param name="Grid">Design grid with valid bins.</param>
        /// <param name="PlaneA">Fitted dz/dE gradient.</param>
        /// <param name="PlaneB">Fitted dz/dN gradient.</param>
        /// <param name="PlaneC">Fitted intercept at the grid origin.</param>
        /// <returns>Total cut and fill in cubic yards.</returns>
        static private (double TotalCutCy, double TotalFillCy) ComputeCutFillVolumes
            (
            DesignGrid Grid,
            double PlaneA,
            double PlaneB,
            double PlaneC
            )
        {
            double BinAreaM2 = Grid.BinSizeM * Grid.BinSizeM;
            double CutM3 = 0.0;
            double FillM3 = 0.0;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid || Bin.ExistingM == FieldConstants.BIN_NO_DATA_SENTINEL)
                    continue;

                UTM.UTMCoordinate Utm = UTM.FromLatLon(Bin.CentroidLat, Bin.CentroidLon);
                double PlaneM = (PlaneA * (Utm.Easting - Grid.OriginEastingM))
                    + (PlaneB * (Utm.Northing - Grid.OriginNorthingM))
                    + PlaneC;
                double DeltaM = Bin.ExistingM - PlaneM;
                if (DeltaM > 0.0)
                    CutM3 += DeltaM * BinAreaM2;
                else if (DeltaM < 0.0)
                    FillM3 += (-DeltaM) * BinAreaM2;
            }

            return (
                CutM3 * FieldConstants.CUBIC_YARDS_PER_CUBIC_METER,
                FillM3 * FieldConstants.CUBIC_YARDS_PER_CUBIC_METER);
        }

        /// <summary>
        /// Derives composite grade percent and downhill heading from plane gradients.
        /// </summary>
        /// <param name="A">Design plane easting gradient dz/dE.</param>
        /// <param name="B">Design plane northing gradient dz/dN.</param>
        /// <param name="SlopePercent">Steepest downhill grade as percent.</param>
        /// <param name="DownhillHeadingDegCwFromNorth">Downhill direction in degrees clockwise from north.</param>
        static private void CompositeSlopeAndHeading
            (
            double A,
            double B,
            out double SlopePercent,
            out double DownhillHeadingDegCwFromNorth
            )
        {
            double Mag = Math.Sqrt((A * A) + (B * B));
            SlopePercent = Mag * SLOPE_PERCENT_SCALE;
            double De = -A;
            double Dn = -B;
            if ((Math.Abs(De) < GRADIENT_MAGNITUDE_EPSILON) && (Math.Abs(Dn) < GRADIENT_MAGNITUDE_EPSILON))
            {
                DownhillHeadingDegCwFromNorth = DEFAULT_DOWNHILL_HEADING_DEG;
                return;
            }

            double HeadingRad = Math.Atan2(De, Dn);
            double HeadingDeg = HeadingRad * (FieldConstants.DEGREES_PER_RADIAN / Math.PI);
            DownhillHeadingDegCwFromNorth =
                (HeadingDeg % FieldConstants.DEGREES_FULL_CIRCLE + FieldConstants.DEGREES_FULL_CIRCLE)
                % FieldConstants.DEGREES_FULL_CIRCLE;
        }

        /// <summary>
        /// Solves a 3×3 linear system using partial pivoting without external linear algebra dependencies.
        /// </summary>
        /// <returns>True when a finite solution was found.</returns>
        static private bool TrySolve3x3
            (
            double A00,
            double A01,
            double A02,
            double A11,
            double A12,
            double A22,
            double B0,
            double B1,
            double B2,
            out double X0,
            out double X1,
            out double X2
            )
        {
            double[,] Matrix = new double[NORMAL_EQUATION_DIM, NORMAL_EQUATION_DIM];
            double[] Vector = new double[NORMAL_EQUATION_DIM];
            Matrix[0, 0] = A00;
            Matrix[0, 1] = A01;
            Matrix[0, 2] = A02;
            Matrix[1, 0] = A01;
            Matrix[1, 1] = A11;
            Matrix[1, 2] = A12;
            Matrix[2, 0] = A02;
            Matrix[2, 1] = A12;
            Matrix[2, 2] = A22;
            Vector[0] = B0;
            Vector[1] = B1;
            Vector[2] = B2;

            // Gaussian elimination with partial pivoting on the 3×3 normal equations.
            for (int Column = 0; Column < NORMAL_EQUATION_DIM; Column++)
            {
                int PivotRow = Column;
                double Best = Math.Abs(Matrix[Column, Column]);
                for (int Row = Column + 1; Row < NORMAL_EQUATION_DIM; Row++)
                {
                    double AbsValue = Math.Abs(Matrix[Row, Column]);
                    if (AbsValue > Best)
                    {
                        Best = AbsValue;
                        PivotRow = Row;
                    }
                }

                if (Best <= MATRIX_SINGULAR_EPSILON)
                {
                    X0 = double.NaN;
                    X1 = double.NaN;
                    X2 = double.NaN;
                    return false;
                }

                if (PivotRow != Column)
                {
                    for (int Col = Column; Col < NORMAL_EQUATION_DIM; Col++)
                    {
                        double Temp = Matrix[Column, Col];
                        Matrix[Column, Col] = Matrix[PivotRow, Col];
                        Matrix[PivotRow, Col] = Temp;
                    }

                    double TempVector = Vector[Column];
                    Vector[Column] = Vector[PivotRow];
                    Vector[PivotRow] = TempVector;
                }

                double Pivot = Matrix[Column, Column];
                for (int Col = Column; Col < NORMAL_EQUATION_DIM; Col++)
                    Matrix[Column, Col] /= Pivot;

                Vector[Column] /= Pivot;

                for (int Row = 0; Row < NORMAL_EQUATION_DIM; Row++)
                {
                    if (Row == Column)
                        continue;

                    double Factor = Matrix[Row, Column];
                    if (Math.Abs(Factor) <= 0.0)
                        continue;

                    for (int Col = Column; Col < NORMAL_EQUATION_DIM; Col++)
                        Matrix[Row, Col] -= Factor * Matrix[Column, Col];

                    Vector[Row] -= Factor * Vector[Column];
                }
            }

            X0 = Vector[0];
            X1 = Vector[1];
            X2 = Vector[2];
            return double.IsFinite(X0) && double.IsFinite(X1) && double.IsFinite(X2);
        }
    }
}

using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Creates deep copies of <see cref="DesignGrid"/> for preview editing.
    /// </summary>
    static public class DesignGridCloner
    {
        /// <summary>
        /// Clones grid metadata and every bin cell into a new instance.
        /// </summary>
        /// <param name="Source">Grid to copy.</param>
        /// <returns>Independent grid copy.</returns>
        static public DesignGrid Clone
            (
            DesignGrid Source
            )
        {
            DesignGrid Copy = new DesignGrid
            {
                BinSizeM = Source.BinSizeM,
                Width = Source.Width,
                Height = Source.Height,
                UtmZone = Source.UtmZone,
                UtmNorth = Source.UtmNorth,
                OriginEastingM = Source.OriginEastingM,
                OriginNorthingM = Source.OriginNorthingM,
                MinX = Source.MinX,
                MinY = Source.MinY,
                MaxX = Source.MaxX,
                MaxY = Source.MaxY,
                PlaneA = Source.PlaneA,
                PlaneB = Source.PlaneB,
                PlaneC = Source.PlaneC,
                MeanLat = Source.MeanLat,
                MeanLon = Source.MeanLon,
                TotalCutCy = Source.TotalCutCy,
                TotalFillCy = Source.TotalFillCy,
                CompositeSlopeHeadingDeg = Source.CompositeSlopeHeadingDeg,
                CompositeSlopePercent = Source.CompositeSlopePercent
            };

            foreach (GridBinData Bin in Source.Bins)
            {
                Copy.Bins.Add(new GridBinData
                {
                    X = Bin.X,
                    Y = Bin.Y,
                    ExistingM = Bin.ExistingM,
                    ProposedM = Bin.ProposedM,
                    CentroidLat = Bin.CentroidLat,
                    CentroidLon = Bin.CentroidLon,
                    Valid = Bin.Valid
                });
            }

            return Copy;
        }

        /// <summary>
        /// Copies proposed elevations from a source grid into the destination grid.
        /// </summary>
        /// <param name="Destination">Grid receiving proposed values.</param>
        /// <param name="Source">Grid supplying proposed values.</param>
        static public void CopyProposedElevations
            (
            DesignGrid Destination,
            DesignGrid Source
            )
        {
            Dictionary<(int X, int Y), double> ProposedByCoordinate = new Dictionary<(int X, int Y), double>();
            foreach (GridBinData Bin in Source.Bins)
            {
                if (!Bin.Valid)
                    continue;

                ProposedByCoordinate[(Bin.X, Bin.Y)] = Bin.ProposedM;
            }

            foreach (GridBinData Bin in Destination.Bins)
            {
                if (!Bin.Valid)
                    continue;

                if (ProposedByCoordinate.TryGetValue((Bin.X, Bin.Y), out double ProposedM))
                    Bin.ProposedM = ProposedM;
            }

            Destination.InvalidateBinIndex();
        }

        /// <summary>
        /// Captures proposed elevations for every valid bin in a grid.
        /// </summary>
        /// <param name="Grid">Source grid.</param>
        /// <returns>Map of bin coordinates to proposed elevation in metres.</returns>
        static public Dictionary<(int X, int Y), double> CaptureProposedElevations
            (
            DesignGrid Grid
            )
        {
            Dictionary<(int X, int Y), double> Snapshot = new Dictionary<(int X, int Y), double>();
            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                Snapshot[(Bin.X, Bin.Y)] = Bin.ProposedM;
            }

            return Snapshot;
        }

        /// <summary>
        /// Restores proposed elevations from a prior snapshot.
        /// </summary>
        /// <param name="Grid">Grid to restore.</param>
        /// <param name="Snapshot">Captured proposed elevations.</param>
        static public void RestoreProposedElevations
            (
            DesignGrid Grid,
            IReadOnlyDictionary<(int X, int Y), double> Snapshot
            )
        {
            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                if (Snapshot.TryGetValue((Bin.X, Bin.Y), out double ProposedM))
                    Bin.ProposedM = ProposedM;
            }

            Grid.InvalidateBinIndex();
        }
    }
}

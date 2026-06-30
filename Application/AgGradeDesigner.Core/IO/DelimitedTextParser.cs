using System.Globalization;

namespace AgGradeDesigner.Core.IO
{
    /// <summary>
    /// Low-allocation helpers for parsing comma- and tab-delimited survey rows.
    /// </summary>
    static internal class DelimitedTextParser
    {
        /// <summary>
        /// Estimates data row count from file size using an expected bytes-per-row guess.
        /// </summary>
        /// <param name="FilePath">Path to the text file.</param>
        /// <param name="AverageBytesPerRow">Expected average encoded row size.</param>
        /// <returns>Conservative capacity hint for list pre-allocation.</returns>
        static public int EstimateRowCapacity
            (
            string FilePath,
            int AverageBytesPerRow
            )
        {
            long Length = new FileInfo(FilePath).Length;
            if (Length <= 0 || AverageBytesPerRow <= 0)
                return 256;

            return (int)Math.Clamp(Length / AverageBytesPerRow, 256, 2_000_000);
        }

        /// <summary>
        /// Parses the first three comma-separated numeric columns from one trimmed line.
        /// </summary>
        /// <param name="Line">Single CSV row without trailing newline.</param>
        /// <param name="Latitude">Parsed latitude in decimal degrees.</param>
        /// <param name="Longitude">Parsed longitude in decimal degrees.</param>
        /// <param name="Value">Parsed third-column numeric value.</param>
        /// <param name="FourthFieldStart">Start index of the optional fourth field within <paramref name="Line"/>.</param>
        /// <param name="FourthFieldLength">Length of the optional fourth field.</param>
        /// <returns>True when all three numeric fields parse successfully.</returns>
        static public bool TryParseLeadingDoubles
            (
            ReadOnlySpan<char> Line,
            out double Latitude,
            out double Longitude,
            out double Value,
            out int FourthFieldStart,
            out int FourthFieldLength
            )
        {
            Latitude = 0;
            Longitude = 0;
            Value = 0;
            FourthFieldStart = -1;
            FourthFieldLength = 0;

            if (!TryParseField(Line, 0, out Latitude, out int AfterFirst))
                return false;
            if (!TryParseField(Line, AfterFirst, out Longitude, out int AfterSecond))
                return false;
            if (!TryParseField(Line, AfterSecond, out Value, out int AfterThird))
                return false;

            if (AfterThird < Line.Length)
            {
                FourthFieldStart = AfterThird;
                FourthFieldLength = Line.Length - AfterThird;
            }

            return true;
        }

        /// <summary>
        /// Parses one comma-separated field starting at or after <paramref name="StartIndex"/>.
        /// </summary>
        static private bool TryParseField
            (
            ReadOnlySpan<char> Line,
            int StartIndex,
            out double Value,
            out int NextIndex
            )
        {
            Value = 0;
            NextIndex = StartIndex;

            while (NextIndex < Line.Length && Line[NextIndex] == ' ')
                NextIndex++;

            int FieldStart = NextIndex;
            while (NextIndex < Line.Length && Line[NextIndex] != ',')
                NextIndex++;

            ReadOnlySpan<char> Field = Line[FieldStart..NextIndex].Trim();
            if (Field.IsEmpty)
                return false;

            if (!double.TryParse(Field, NumberStyles.Float, CultureInfo.InvariantCulture, out Value))
                return false;

            if (NextIndex < Line.Length)
                NextIndex++;

            return true;
        }

        /// <summary>
        /// Parses one required floating-point CSV field.
        /// </summary>
        static public double ParseRequiredDouble
            (
            ReadOnlySpan<char> Text
            )
        {
            return double.Parse(Text.Trim(), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Attempts to parse an optional floating-point CSV field.
        /// </summary>
        static public bool TryParseOptionalDouble
            (
            ReadOnlySpan<char> Text,
            out double Value
            )
        {
            Value = 0;
            ReadOnlySpan<char> Trimmed = Text.Trim();
            if (Trimmed.IsEmpty)
                return false;

            return double.TryParse(Trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out Value);
        }

        /// <summary>
        /// Returns whether a trimmed code field denotes a benchmark row.
        /// </summary>
        static public bool IsBenchmarkCode
            (
            ReadOnlySpan<char> Code
            )
        {
            if (Code.IsEmpty)
                return false;

            for (int Index = 0; Index <= Code.Length - 2; Index++)
            {
                char A = char.ToUpperInvariant(Code[Index]);
                char B = char.ToUpperInvariant(Code[Index + 1]);
                if ((A == 'M' && B == 'B') || (A == 'B' && B == 'M'))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Compares a trimmed code field to another code using ordinal case-insensitive rules.
        /// </summary>
        static public bool CodeEquals
            (
            ReadOnlySpan<char> Code,
            string Expected
            )
        {
            return Code.Equals(Expected.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }
    }
}

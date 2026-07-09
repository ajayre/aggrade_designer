using System.Buffers.Binary;

namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Encodes and decodes opaque water layer binary payloads for project persistence (WATER-SPEC Appendix A §A.0.12).
    /// </summary>
    public static class WaterLayerBlobCodec
    {
        /// <summary>
        /// Encodes pond depth samples and grid metadata into a binary blob.
        /// </summary>
        /// <param name="Entry">Cache entry whose depth grid should be serialized.</param>
        /// <returns>Binary blob suitable for BASE64 encoding.</returns>
        public static byte[] Encode
            (
            WaterLayerCacheEntry Entry
            )
        {
            if (!Entry.IsValidForDisplay())
                throw new InvalidOperationException("Cannot encode an incomplete water layer cache entry.");

            int BodyBytes = Entry.PondDepthM.Length * WaterConstants.BLOB_DEPTH_BYTES_PER_CELL;
            byte[] Blob = new byte[WaterConstants.BLOB_HEADER_SIZE_BYTES + BodyBytes];
            BinaryPrimitives.WriteUInt32LittleEndian(Blob.AsSpan(0, 4), WaterConstants.BLOB_MAGIC);
            BinaryPrimitives.WriteUInt32LittleEndian(Blob.AsSpan(4, 4), WaterConstants.BLOB_VERSION);
            BinaryPrimitives.WriteInt32LittleEndian(Blob.AsSpan(8, 4), Entry.GridWidth);
            BinaryPrimitives.WriteInt32LittleEndian(Blob.AsSpan(12, 4), Entry.GridHeight);
            BinaryPrimitives.WriteDoubleLittleEndian(Blob.AsSpan(16, 8), Entry.CellSizeM);
            BinaryPrimitives.WriteDoubleLittleEndian(Blob.AsSpan(24, 8), Entry.MinLat);
            BinaryPrimitives.WriteDoubleLittleEndian(Blob.AsSpan(32, 8), Entry.MaxLat);
            BinaryPrimitives.WriteDoubleLittleEndian(Blob.AsSpan(40, 8), Entry.MinLon);
            BinaryPrimitives.WriteDoubleLittleEndian(Blob.AsSpan(48, 8), Entry.MaxLon);

            Span<byte> Body = Blob.AsSpan(WaterConstants.BLOB_HEADER_SIZE_BYTES);
            for (Int32 Index = 0; Index < Entry.PondDepthM.Length; Index++)
            {
                BinaryPrimitives.WriteSingleLittleEndian(Body.Slice(Index * 4, 4), Entry.PondDepthM[Index]);
            }

            return Blob;
        }

        /// <summary>
        /// Decodes a binary blob into pond depth samples and grid metadata.
        /// </summary>
        /// <param name="Blob">Decoded binary payload bytes.</param>
        /// <param name="Entry">Partial cache entry receiving decoded grid fields.</param>
        /// <returns>True when decoding succeeds.</returns>
        public static bool TryDecode
            (
            byte[] Blob,
            WaterLayerCacheEntry Entry
            )
        {
            if ((Blob == null) || (Blob.Length < WaterConstants.BLOB_HEADER_SIZE_BYTES))
                return false;

            uint Magic = BinaryPrimitives.ReadUInt32LittleEndian(Blob.AsSpan(0, 4));
            if (Magic != WaterConstants.BLOB_MAGIC)
                return false;

            uint Version = BinaryPrimitives.ReadUInt32LittleEndian(Blob.AsSpan(4, 4));
            if ((Version != WaterConstants.BLOB_VERSION) && (Version != WaterConstants.BLOB_VERSION_LEGACY))
                return false;

            int HeaderSize = Version == WaterConstants.BLOB_VERSION
                ? WaterConstants.BLOB_HEADER_SIZE_BYTES
                : WaterConstants.BLOB_LEGACY_HEADER_SIZE_BYTES;

            int Width = BinaryPrimitives.ReadInt32LittleEndian(Blob.AsSpan(8, 4));
            int Height = BinaryPrimitives.ReadInt32LittleEndian(Blob.AsSpan(12, 4));
            double CellSizeM = BinaryPrimitives.ReadDoubleLittleEndian(Blob.AsSpan(16, 8));
            if ((Width <= 0) || (Height <= 0))
                return false;

            int CellCount = Width * Height;
            int ExpectedLength = HeaderSize + (CellCount * WaterConstants.BLOB_DEPTH_BYTES_PER_CELL);
            if (Blob.Length != ExpectedLength)
                return false;

            double MinLat = 0.0;
            double MaxLat = 0.0;
            double MinLon = 0.0;
            double MaxLon = 0.0;
            if (Version == WaterConstants.BLOB_VERSION)
            {
                MinLat = BinaryPrimitives.ReadDoubleLittleEndian(Blob.AsSpan(24, 8));
                MaxLat = BinaryPrimitives.ReadDoubleLittleEndian(Blob.AsSpan(32, 8));
                MinLon = BinaryPrimitives.ReadDoubleLittleEndian(Blob.AsSpan(40, 8));
                MaxLon = BinaryPrimitives.ReadDoubleLittleEndian(Blob.AsSpan(48, 8));
            }

            float[] Depths = new float[CellCount];
            Span<byte> Body = Blob.AsSpan(HeaderSize);
            for (Int32 Index = 0; Index < CellCount; Index++)
            {
                Depths[Index] = BinaryPrimitives.ReadSingleLittleEndian(Body.Slice(Index * 4, 4));
            }

            Entry.GridWidth = Width;
            Entry.GridHeight = Height;
            Entry.CellSizeM = CellSizeM;
            Entry.MinLat = MinLat;
            Entry.MaxLat = MaxLat;
            Entry.MinLon = MinLon;
            Entry.MaxLon = MaxLon;
            Entry.PondDepthM = Depths;
            Entry.OverlayRgba = PondingAnalysisEngine.BuildOverlayRgba(Depths, Width, Height);
            Entry.PayloadBlob = Blob;
            return Entry.IsValidForDisplay();
        }

        /// <summary>
        /// Encodes a blob as standard BASE64 text without line breaks (RFC 4648 §4).
        /// </summary>
        /// <param name="Blob">Binary payload.</param>
        /// <returns>BASE64 text.</returns>
        public static string EncodeBase64
            (
            byte[] Blob
            )
        {
            return Convert.ToBase64String(Blob);
        }

        /// <summary>
        /// Decodes standard BASE64 text into bytes; rejects malformed input (WATER-SPEC Appendix A §A.0.12).
        /// </summary>
        /// <param name="Base64">BASE64 payload text.</param>
        /// <param name="Blob">Decoded bytes when successful.</param>
        /// <returns>True when decoding succeeds.</returns>
        public static bool TryDecodeBase64
            (
            string Base64,
            out byte[] Blob
            )
        {
            Blob = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(Base64))
                return false;

            try
            {
                Blob = Convert.FromBase64String(Base64.Trim());
                return Blob.Length > 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}

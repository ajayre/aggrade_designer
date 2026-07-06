using System.Text.Json;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Serializes and deserializes embedded haul payloads for project files (§7.5, Appendix A §A.2).
    /// </summary>
    public static class HaulDataSerializer
    {
        /// <summary>JSON options for stable haul payload persistence.</summary>
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// Serializes haul payload to JSON text.
        /// </summary>
        /// <param name="Payload">Payload to write.</param>
        /// <returns>UTF-8 JSON string.</returns>
        public static string SerializePayload
            (
            HaulPersistedPayload Payload
            )
        {
            return JsonSerializer.Serialize(Payload, JsonOptions);
        }

        /// <summary>
        /// Deserializes haul payload from JSON text.
        /// </summary>
        /// <param name="Json">UTF-8 JSON string.</param>
        /// <returns>Restored payload, or null when invalid.</returns>
        public static HaulPersistedPayload? DeserializePayload
            (
            string Json
            )
        {
            if (string.IsNullOrWhiteSpace(Json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<HaulPersistedPayload>(Json, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Builds in-memory haul data from a restored payload.
        /// </summary>
        /// <param name="Payload">Payload loaded from disk.</param>
        /// <returns>Haul data suitable for display when valid.</returns>
        public static HaulData? FromPayload
            (
            HaulPersistedPayload? Payload
            )
        {
            if (Payload == null || Payload.OrderedPasses.Count == 0)
                return null;

            return new HaulData { Payload = Payload };
        }
    }
}

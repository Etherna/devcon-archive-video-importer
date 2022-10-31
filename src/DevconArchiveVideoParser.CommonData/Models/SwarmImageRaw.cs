using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Etherna.DevconArchiveVideoParser.CommonData.Models
{
    public class SwarmImageRaw
    {
        // Constructors.
        public SwarmImageRaw(
            float aspectRatio,
            string blurhash,
            IReadOnlyDictionary<string, string> sources,
            string v)
        {
            AspectRatio = aspectRatio;
            Blurhash = blurhash;
            Sources = sources;
            V = v;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public SwarmImageRaw() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Properties.
        [JsonInclude]
        public float AspectRatio { get; protected set; }
        [JsonInclude]
        public string Blurhash { get; protected set; }
        [JsonInclude]
        public IReadOnlyDictionary<string, string> Sources { get; protected set; }
        [JsonInclude]
        public string V { get; protected set; }
    }
}

using System.Text.Json.Serialization;

namespace Etherna.DevconArchiveVideoParser.CommonData.Models
{
    public class MetadataVideoSource
    {
        // Constructors.
        public MetadataVideoSource(
            int bitrate,
            string quality,
            string reference,
            long size)
        {
            Bitrate = bitrate;
            Quality = quality;
            Reference = reference;
            Size = size;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MetadataVideoSource() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Properties.
        [JsonInclude]
        public int Bitrate { get; protected set; }
        [JsonInclude]
        public string Quality { get; protected set; }
        [JsonInclude]
        public string Reference { get; protected set; }
        [JsonInclude]
        public long Size { get; protected set; }
    }
}

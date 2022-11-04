using Etherna.DevconArchiveVideoImporter.Index.Models.MetadataVideoAgg;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Etherna.DevconArchiveVideoImporter.Index.Models
{
    public class MetadataVideo
    {
        // Constructors.
        public MetadataVideo(
            string? batchId,
            string description,
            long duration,
            long createdAt,
            string originalQuality,
            string ownerAddress,
            IEnumerable<MetadataVideoSource> sources,
            SwarmImageRaw? thumbnail,
            string title,
            long? updatedAt,
            string v,
            string jsonMetadataPersonalData)
        {
            BatchId = batchId;
            CreatedAt = createdAt;
            Description = description;
            Duration = duration;
            PersonalData = jsonMetadataPersonalData;
            OriginalQuality = originalQuality;
            OwnerAddress = ownerAddress;
            Sources = sources;
            Thumbnail = thumbnail;
            Title = title;
            UpdatedAt = updatedAt;
            V = v;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MetadataVideo() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Properties.
        [JsonInclude]
        public string? BatchId { get; protected set; }
        [JsonInclude]
        public long CreatedAt { get; protected set; }
        [JsonInclude]
        public string Description { get; protected set; }
        [JsonInclude]
        public long Duration { get; protected set; }
        [JsonInclude]
        public string PersonalData { get; protected set; }
        [JsonInclude]
        public string? Hash { get; protected set; }
        [JsonInclude]
        public string OriginalQuality { get; protected set; }
        [JsonInclude]
        public string OwnerAddress { get; protected set; }
        [JsonInclude]
        public IEnumerable<MetadataVideoSource> Sources { get; protected set; }
        [JsonInclude]
        public SwarmImageRaw? Thumbnail { get; protected set; }
        [JsonInclude]
        public string Title { get; protected set; }
        [JsonInclude]
        public long? UpdatedAt { get; protected set; }
        [JsonInclude]
        public string V { get; protected set; }

        // Methods.
        public bool CheckForMetadataInfoChanged(
            string description,
            string title)
        {
            return title != Title ||
                description != Description;
        }

        public void UpdateMetadataInfo(
            string description,
            string title)
        {
            Description = description ?? "";
            Title = title ?? "";
        }
    }
}

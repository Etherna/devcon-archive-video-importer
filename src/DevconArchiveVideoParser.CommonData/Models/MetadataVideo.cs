using Etherna.DevconArchiveVideoParser.CommonData.Dtos;
using Etherna.DevconArchiveVideoParser.CommonData.Json;
using Etherna.DevconArchiveVideoParser.CommonData.Models.MetadataVideoAgg;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Etherna.DevconArchiveVideoParser.CommonData.Models
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
            MetadataPersonalDataDto metadataPersonalData)
        {
            BatchId = batchId;
            CreatedAt = createdAt;
            Description = description;
            Duration = duration;
            PersonalData = JsonUtility.ToJson(metadataPersonalData);
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
        public bool CheckForMetadataInfoChanged(MDFileData mdFileData)
        {
            if (mdFileData is null)
                return true;

            return mdFileData.Title != Title ||
                mdFileData.Description != Description;
        }
        
        public T? PersonalDataTyped<T>() where T : class
        {
            return JsonUtility.FromJson<T>(PersonalData);
        }


        public void UpdateMetadataInfo(MDFileData mdFileData)
        {
            if (mdFileData is null)
                throw new ArgumentNullException(nameof(mdFileData));

            Description = mdFileData.Description ?? "";
            Title = mdFileData.Title ?? "";
        }
    }
}

using Etherna.DevconArchiveVideoParser.CommonData.Json;
using System;
using System.Collections.Generic;

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
            MetadataExtraInfo metadataExtraInfo)
        {
            BatchId = batchId;
            CreatedAt = createdAt;
            Description = description;
            Duration = duration;
            ExtraInfo = JsonUtility.ToJson(metadataExtraInfo);
            OriginalQuality = originalQuality;
            OwnerAddress = ownerAddress;
            Sources = sources;
            Thumbnail = thumbnail;
            Title = title;
            UpdatedAt = updatedAt;
            V = v;
        }

        // Properties.
        public string? BatchId { get; }
        public long CreatedAt { get; }
        public string Description { get; private set; }
        public long Duration { get; }
        public string ExtraInfo { get; }
        public string? Hash { get; }
        public string OriginalQuality { get; }
        public string OwnerAddress { get; }
        public IEnumerable<MetadataVideoSource> Sources { get; }
        public SwarmImageRaw? Thumbnail { get; }
        public string Title { get; private set; }
        public long? UpdatedAt { get; }
        public string V { get; }

        // Methods.
        public bool CheckForMetadataInfoChanged(MDFileData mdFileData)
        {
            if (mdFileData is null)
                return true;

            return mdFileData.Title != Title ||
                mdFileData.Description != Description;
        }

        public T? ExtraInfoTyped<T>() where T : class
        {
            return JsonUtility.FromJson<T>(ExtraInfo);
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

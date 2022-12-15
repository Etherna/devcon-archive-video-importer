using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Models
{
    public class VideoDataResolution
    {
        // Constructors.
        public VideoDataResolution(
            int audioBitrate,
            string name,
            int resolution,
            Uri uri)
        {
            AudioBitrate = audioBitrate;
            Name = name;
            Resolution = resolution;
            Uri = uri;
        }

        // Properties.
        public int AudioBitrate { get; protected set; }
        public int Bitrate { get; protected set; }
        public int Duration { get; protected set; }
        public string? DownloadedFileName { get; protected set; }
        public string? DownloadedFilePath { get; protected set; }
        public string? DownloadedThumbnailPath { get; protected set; }
        public string? UploadedVideoReference { get; protected set; }
        public string Name { get; protected set; }
        public int Resolution { get; protected set; }
        public long Size { get; protected set; }
        public Uri Uri { get; protected set; }

        // Methods.
        public void SetVideoInfo(
            string filename,
            long fileSize,
            int duration)
        {
            DownloadedFileName = filename;
            Size = fileSize;
            Duration = duration;
            Bitrate = (int)Math.Ceiling((double)fileSize * 8 / duration);
        }

        public void SetDownloadedFilePath(string downloadedFilePath)
        {
            DownloadedFilePath = downloadedFilePath;
        }

        public void SetUploadedVideoReference(string uploadedVideoReference)
        {
            UploadedVideoReference = uploadedVideoReference;
        }
    }
}

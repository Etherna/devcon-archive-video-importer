namespace Etherna.DevconArchiveVideoParser.CommonData.Requests
{
    public class IndexManifestRequest
    {
        public IndexManifestRequest(string manifestHash)
        {
            ManifestHash = manifestHash;
        }

        public string ManifestHash { get; set; }
    }
}

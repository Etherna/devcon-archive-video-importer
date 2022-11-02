namespace Etherna.DevconArchiveVideoParser.CommonData.Requests
{
    public class IndexManifestRequest
    {
        // Constructors.
        public IndexManifestRequest(string manifestHash)
        {
            ManifestHash = manifestHash;
        }

        // Properties.
        public string ManifestHash { get; set; }
    }
}

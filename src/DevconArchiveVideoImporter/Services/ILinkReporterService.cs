using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    public interface ILinkReporterService
    {
        Task SetEthernaValueAsync(
            string ethernaIndex,
            string ethernaPermalink,
            int duration);
    }
}

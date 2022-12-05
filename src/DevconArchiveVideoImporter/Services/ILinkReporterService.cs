using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    public interface ILinkReporterService
    {
        Task SetEthernaFieldsAsync(
            string ethernaIndex,
            string ethernaPermalink);
    }
}

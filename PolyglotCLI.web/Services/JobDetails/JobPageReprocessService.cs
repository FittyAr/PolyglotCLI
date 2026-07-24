using System.Threading.Tasks;
using PolyglotCLI;

namespace PolyglotCLI.web.Services.JobDetails;

public interface IJobPageReprocessService
{
    Task<bool> ReprocessAsync(string jobId, string sourceFilePath, int pageNumber, AppConfig config);
}

public class JobPageReprocessService : IJobPageReprocessService
{
    public Task<bool> ReprocessAsync(string jobId, string sourceFilePath, int pageNumber, AppConfig config)
    {
        return Task.Run(() => TranslationOrchestrator.ReprocessPageAsync(jobId, sourceFilePath, pageNumber, config));
    }
}

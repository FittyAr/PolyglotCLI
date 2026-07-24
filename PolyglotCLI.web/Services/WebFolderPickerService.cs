using System.Threading.Tasks;

namespace PolyglotCLI.web.Services
{
    public class WebFolderPickerService : IFolderPickerService
    {
        public bool IsSupported => false;
        public Task<string?> PickFolderAsync() => Task.FromResult<string?>(null);
    }
}

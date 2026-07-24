using System.Threading.Tasks;

namespace PolyglotCLI
{
    public interface IFolderPickerService
    {
        bool IsSupported { get; }
        Task<string?> PickFolderAsync();
    }
}

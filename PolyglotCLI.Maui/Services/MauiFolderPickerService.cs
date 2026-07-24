using System;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;

namespace PolyglotCLI.Maui.Services
{
    public class MauiFolderPickerService : IFolderPickerService
    {
        public bool IsSupported => true;

        public async Task<string?> PickFolderAsync()
        {
            try
            {
                var result = await FolderPicker.Default.PickAsync(default);
                if (result != null && result.IsSuccessful && result.Folder != null)
                {
                    return result.Folder.Path;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error picking folder: {ex.Message}");
            }
            return null;
        }
    }
}

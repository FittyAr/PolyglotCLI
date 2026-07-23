using System.Threading.Tasks;

namespace PolyglotCLI
{
    /// <summary>
    /// Cross-mode bridge used by the History page to export/import .zpg packages.
    /// Web implementation uses HTTP endpoints + browser downloads.
    /// MAUI implementation uses native file pickers and direct package I/O.
    /// </summary>
    public interface IJobPackageHost
    {
        /// <summary>
        /// Exports the given job as a .zpg package and delivers it to the user.
        /// Returns a user-friendly status message describing what happened
        /// (e.g. "Download started" for web, "Saved to /path/file.zpg" for MAUI).
        /// Throws on error.
        /// </summary>
        Task<string> ExportJobPackageAsync(JobManifest job);

        /// <summary>
        /// Prompts the user to select a .zpg package and imports it.
        /// Returns the new effective JobId, or <c>null</c> if the user cancelled.
        /// Throws on error.
        /// </summary>
        Task<string?> ImportJobPackageAsync();
    }
}

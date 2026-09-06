using System.Diagnostics;

namespace EatsVoiceCompanion.App.Services;

public sealed record EatsProcessInfo(
    int ProcessId,
    string WindowTitle,
    nint MainWindowHandle,
    string? ExecutablePath,
    string? ProductVersion);

public sealed class EatsProcessDetector
{
    public EatsProcessInfo? FindRunningInstance()
    {
        foreach (Process process in Process.GetProcessesByName("eATS"))
        {
            using (process)
            {
                if (process.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                string? executablePath = null;
                string? productVersion = null;

                try
                {
                    executablePath = process.MainModule?.FileName;
                    productVersion =
                        process.MainModule?.FileVersionInfo.ProductVersion;
                }
                catch
                {
                    // Detection should still work if Windows denies access
                    // to detailed process information.
                }

                return new EatsProcessInfo(
                    process.Id,
                    process.MainWindowTitle,
                    process.MainWindowHandle,
                    executablePath,
                    productVersion);
            }
        }

        return null;
    }
}
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AudioControlApp.Automation;

/// <summary>A cheap snapshot of the running executables and which one owns the foreground window.</summary>
public sealed class ProcessSnapshot
{
    public HashSet<string> Running { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Foreground { get; private set; }

    public static ProcessSnapshot Capture()
    {
        var snapshot = new ProcessSnapshot();
        int foregroundPid = 0;
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd != IntPtr.Zero)
        {
            GetWindowThreadProcessId(hwnd, out foregroundPid);
        }

        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                string name;
                try
                {
                    name = process.ProcessName + ".exe";
                }
                catch
                {
                    continue;
                }

                snapshot.Running.Add(name);
                if (foregroundPid != 0 && process.Id == foregroundPid)
                {
                    snapshot.Foreground = name;
                }
            }
        }

        return snapshot;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);
}

using DeskWidgets.Forms;
using DeskWidgets.Services;

namespace DeskWidgets;

internal static class Program
{
    private const string MutexName = @"Local\DeskWidgets.SingleInstance.v1";

    [STAThread]
    private static int Main(string[] args)
    {
        // Clean uninstall helper: DeskWidgets.exe --cleanup
        if (args.Any(a => string.Equals(a, "--cleanup", StringComparison.OrdinalIgnoreCase)))
            return RunCleanup();

        using var mutex = new Mutex(true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // Already running: ask the running copy to show its dashboard, then quit.
            NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, DashboardForm.ShowMessageId, IntPtr.Zero, IntPtr.Zero);
            return 0;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) => LogCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);

        try
        {
            using var controller = new AppController();
            Application.Run(controller);
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            MessageBox.Show("DeskWidgets could not start:\n\n" + ex.Message +
                            "\n\nDetails were saved to crash.log in the settings folder.",
                "DeskWidgets", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        return 0;
    }

    private static int RunCleanup()
    {
        bool startupRemoved = StartupService.SetEnabled(false);
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskWidgets");
        bool folderRemoved = true;
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        catch
        {
            folderRemoved = false;
        }

        MessageBox.Show(
            (startupRemoved ? "The Start with Windows entry was removed.\n" : "The startup entry could not be removed.\n") +
            (folderRemoved ? "Your DeskWidgets settings folder was deleted.\n" : "The settings folder could not be deleted (is DeskWidgets still running? Exit it from the tray first).\n") +
            "\nYou can now delete DeskWidgets.exe.",
            "DeskWidgets cleanup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return startupRemoved && folderRemoved ? 0 : 1;
    }

    private static void LogCrash(Exception? ex)
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskWidgets");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "crash.log"), $"[{DateTime.Now:u}] {ex}\n\n");
        }
        catch
        {
            // Logging must never cause another crash.
        }
    }
}

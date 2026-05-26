using ClassDiagramMaker.Analysis;

namespace ClassDiagramMaker;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, args) => ReportFatalError(args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                {
                    ReportFatalError(exception);
                }
            };

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(new ClassDiagramService()));
        }
        catch (Exception exception)
        {
            ReportFatalError(exception);
        }
    }

    private static void ReportFatalError(Exception exception)
    {
        var logPath = WriteErrorLog(exception);
        var message = string.IsNullOrWhiteSpace(logPath)
            ? $"画面の起動に失敗しました。{Environment.NewLine}{Environment.NewLine}{exception.Message}"
            : $"画面の起動に失敗しました。{Environment.NewLine}{Environment.NewLine}{exception.Message}{Environment.NewLine}{Environment.NewLine}ログ: {logPath}";

        try
        {
            MessageBox.Show(
                message,
                "ClassDiagramMaker",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // If the UI subsystem itself is unavailable, the log is the fallback.
        }
    }

    private static string? WriteErrorLog(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClassDiagramMaker");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, "ClassDiagramMaker.error.log");
            File.AppendAllText(
                path,
                $"[{DateTimeOffset.Now:O}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");

            return path;
        }
        catch
        {
            return null;
        }
    }
}

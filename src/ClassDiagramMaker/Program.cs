using ClassDiagramMaker.Analysis;

namespace ClassDiagramMaker;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(new ClassDiagramService()));
    }
}

using System.Windows;
using BimArena.Core;
using BimArena.Desktop;

namespace BimArena.Demo;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var application = new Application();
        var window = new GameWindow(new GameSession(DemoScene.Create()));
        application.Run(window);
        if (window.Failure is { } error) MessageBox.Show(error.ToString(), "BIM Arena error");
    }
}

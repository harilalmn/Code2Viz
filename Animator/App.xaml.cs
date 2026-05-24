using System.IO;
using System.Windows;

namespace Animator;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string? initialFile = null;
        if (e.Args != null)
        {
            foreach (var arg in e.Args)
            {
                if (!string.IsNullOrWhiteSpace(arg) && File.Exists(arg))
                {
                    initialFile = arg;
                    break;
                }
            }
        }

        var main = new MainWindow(initialFile);
        main.Show();
    }
}

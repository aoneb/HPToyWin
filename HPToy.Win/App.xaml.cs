using System.IO;
using System.Windows;
using HPToy.Win.Helpers;
using HPToy.Win.Services;

namespace HPToy.Win;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            if (!IsAutoTest(e.Args))
                MessageBox.Show(args.Exception.Message, UiText.HptoyErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        UiLanguageService.Initialize();
        var presetsPath = Path.Combine(AppContext.BaseDirectory, "Presets");
        HPToyAppService.Instance.Initialize(presetsPath);

        if (IsAutoTest(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Task.Run(RunAutoTestBackground);
            return;
        }

        if (IsAutoTestCold(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Task.Run(RunAutoTestColdBackground);
            return;
        }

        new MainWindow().Show();
    }

    private static bool IsAutoTest(string[] args) =>
        args.Any(a => a.Equals("--autotest", StringComparison.OrdinalIgnoreCase));

    private static bool IsAutoTestCold(string[] args) =>
        args.Any(a => a.Equals("--autotest-cold", StringComparison.OrdinalIgnoreCase));

    private void RunAutoTestColdBackground()
    {
        var code = 99;
        try
        {
            code = AutoConnectRunner.RunColdStartAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(AutoConnectRunner.LogPath, ex + Environment.NewLine);
            }
            catch
            {
            }
        }

        Dispatcher.Invoke(() =>
        {
            Shutdown(code);
            Environment.Exit(code);
        });
    }

    private void RunAutoTestBackground()
    {
        var code = 99;
        try
        {
            code = AutoConnectRunner.RunAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(AutoConnectRunner.LogPath, ex + Environment.NewLine);
            }
            catch
            {
            }
        }

        Dispatcher.Invoke(() =>
        {
            Shutdown(code);
            Environment.Exit(code);
        });
    }
}

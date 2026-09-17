using System.Windows;
using System.Windows.Threading;
using MarkdownPad.Core;
using MarkdownPad.Core.Settings;
using MarkdownPad.Core.Text;
using MarkdownPad.Views;

namespace MarkdownPad;

public partial class App : Application
{
    private AppSettings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Required before Shift_JIS and other legacy code pages can be resolved
        // at all on .NET; without it, opening a CP932 file throws.
        TextEncodings.EnsureProviderRegistered();

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _settings = AppSettings.Load(AppPaths.SettingsFile);

        var window = new MainWindow(_settings)
        {
            StartupFilePath = e.Args.FirstOrDefault(),
        };

        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// Last-resort handler so an unexpected failure reports itself instead of
    /// closing the window and taking the user's buffer with it.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"予期しないエラーが発生しました。\n\n{e.Exception.Message}\n\n" +
            "作業内容は復元用データとして保存されている場合があります。",
            "MarkdownPad",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settings.Save(AppPaths.SettingsFile);
        base.OnExit(e);
    }
}

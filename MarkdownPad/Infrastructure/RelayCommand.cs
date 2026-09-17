using System.Windows.Input;

namespace MarkdownPad.Infrastructure;

/// <summary>
/// A command whose availability is published explicitly rather than through
/// <see cref="CommandManager.RequerySuggested"/>.
/// </summary>
/// <remarks>
/// The previous implementation subscribed to <c>RequerySuggested</c>, which fires
/// on nearly every keystroke and focus change. Combined with a
/// <c>CanExecute</c> that touched the clipboard, that hammered the OLE clipboard
/// continuously and could throw <c>COMException</c> when another process held it.
/// Clipboard-dependent commands are now WPF's own
/// <see cref="ApplicationCommands"/>, routed to the editor.
/// </remarks>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Parameterised variant, used for the recent-files list.</summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
        => parameter is T typed ? _canExecute?.Invoke(typed) ?? true : parameter is null && _canExecute is null;

    public void Execute(object? parameter)
    {
        if (parameter is T typed) _execute(typed);
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

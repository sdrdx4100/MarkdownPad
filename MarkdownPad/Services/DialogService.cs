using System.IO;
using System.Windows;
using MarkdownPad.Core.Settings;
using MarkdownPad.Views;
using Microsoft.Win32;

namespace MarkdownPad.Services;

public sealed class DialogService : IDialogService
{
    private readonly Window _owner;
    private FindReplaceDialog? _findReplaceDialog;

    public DialogService(Window owner) => _owner = owner;

    public string? PickFileToOpen(string filter, string title, string? initialDirectory)
    {
        var dialog = new OpenFileDialog { Filter = filter, Title = title };
        if (Directory.Exists(initialDirectory)) dialog.InitialDirectory = initialDirectory;

        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    public string? PickFileToSave(string filter, string title, string defaultExtension, string? suggestedName, string? initialDirectory)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            Title = title,
            DefaultExt = defaultExtension,
            AddExtension = true,
        };

        if (!string.IsNullOrEmpty(suggestedName)) dialog.FileName = suggestedName;
        if (Directory.Exists(initialDirectory)) dialog.InitialDirectory = initialDirectory;

        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    public SaveChangesAnswer AskToSaveChanges(string documentName)
    {
        var result = MessageBox.Show(
            _owner,
            $"'{documentName}' への変更を保存しますか?",
            "MarkdownPad",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        return result switch
        {
            MessageBoxResult.Yes => SaveChangesAnswer.Save,
            MessageBoxResult.No => SaveChangesAnswer.Discard,
            _ => SaveChangesAnswer.Cancel,
        };
    }

    public bool Confirm(string message, string title)
        => MessageBox.Show(_owner, message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel)
           == MessageBoxResult.OK;

    public void ShowError(string message, string title = "エラー")
        => MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInformation(string message, string title = "MarkdownPad")
        => MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public LinkDialogResult ShowInsertLink(string initialText)
    {
        var dialog = new InsertLinkDialog(initialText) { Owner = _owner };
        bool confirmed = dialog.ShowDialog() == true;

        return new LinkDialogResult(confirmed, dialog.LinkText, dialog.LinkUrl);
    }

    public bool ShowMarkdownGuide()
    {
        var window = new MarkdownGuideWindow { Owner = _owner };
        window.ShowDialog();
        return window.InsertRequested;
    }

    public void ShowFindReplace(TextBoxEditor editor, SearchSession session, bool showReplace, string? seedText)
    {
        // One instance, reused: the old code called Show() every time, so each
        // Ctrl+F left another dialog on screen.
        _findReplaceDialog ??= new FindReplaceDialog(editor, session, showReplace) { Owner = _owner };

        _findReplaceDialog.ShowReplace(showReplace);
        if (!string.IsNullOrEmpty(seedText)) _findReplaceDialog.SetSearchText(seedText);

        _findReplaceDialog.Show();
        _findReplaceDialog.FocusSearchBox();
    }

    public void CloseFindReplace()
    {
        if (_findReplaceDialog is null) return;

        var dialog = _findReplaceDialog;
        _findReplaceDialog = null;

        // The dialog cancels its own Closing to stay reusable, so it needs an
        // explicit shutdown when the application is going away.
        dialog.CloseForReal();
    }

    public bool AskToReload(string fileName, bool hasUnsavedChanges)
    {
        string warning = hasUnsavedChanges
            ? "\n\n再読み込みすると、保存していない変更は失われます。"
            : string.Empty;

        return MessageBox.Show(
            _owner,
            $"'{fileName}' が他のプログラムによって変更されました。\n再読み込みしますか?{warning}",
            "MarkdownPad",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            hasUnsavedChanges ? MessageBoxResult.No : MessageBoxResult.Yes) == MessageBoxResult.Yes;
    }

    public bool AskToRecover(RecoverySnapshot snapshot)
    {
        var localTime = snapshot.SavedAtUtc.ToLocalTime();

        return MessageBox.Show(
            _owner,
            $"前回終了時に保存されていない変更が見つかりました。\n\n対象: {snapshot.DisplayName}\n保存日時: {localTime:yyyy/MM/dd HH:mm:ss}\n\n復元しますか?",
            "MarkdownPad - 復元",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes) == MessageBoxResult.Yes;
    }
}

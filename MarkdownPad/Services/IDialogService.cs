using MarkdownPad.Core.Settings;

namespace MarkdownPad.Services;

public enum SaveChangesAnswer
{
    Save,
    Discard,
    Cancel,
}

public sealed record LinkDialogResult(bool Confirmed, string Text, string Url);

/// <summary>
/// Everything the view model needs from the UI that is not data binding. Kept
/// behind an interface so the view model holds no references to
/// <c>MessageBox</c>, file dialogs or window types.
/// </summary>
public interface IDialogService
{
    string? PickFileToOpen(string filter, string title, string? initialDirectory);

    string? PickFileToSave(string filter, string title, string defaultExtension, string? suggestedName, string? initialDirectory);

    SaveChangesAnswer AskToSaveChanges(string documentName);

    bool Confirm(string message, string title);

    void ShowError(string message, string title = "エラー");

    void ShowInformation(string message, string title = "MarkdownPad");

    LinkDialogResult ShowInsertLink(string initialText);

    /// <summary>Shows the cheat sheet; returns true when the user asked to insert it.</summary>
    bool ShowMarkdownGuide();

    void ShowFindReplace(TextBoxEditor editor, bool showReplace, string? seedText);

    void CloseFindReplace();

    /// <summary>Offers to restore a snapshot left behind by a previous run.</summary>
    bool AskToRecover(RecoverySnapshot snapshot);
}

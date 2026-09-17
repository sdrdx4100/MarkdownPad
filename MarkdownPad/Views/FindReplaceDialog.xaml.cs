using System.Windows;
using System.Windows.Controls;
using MarkdownPad.Core.Text;
using TextSearch = MarkdownPad.Core.Text.TextSearch;
using MarkdownPad.Services;

namespace MarkdownPad.Views;

/// <summary>
/// Modeless find/replace. A single instance is reused by the main window, and
/// the query it builds is shared with the F3 / Shift+F3 shortcuts.
/// </summary>
public partial class FindReplaceDialog : Window
{
    private readonly TextBoxEditor _editor;
    private readonly SearchSession _session;
    private bool _closingForReal;

    public FindReplaceDialog(TextBoxEditor editor, SearchSession session, bool showReplace)
    {
        InitializeComponent();

        _editor = editor;
        _session = session;

        ShowReplace(showReplace);
        Loaded += (_, _) => FindTextBox.Focus();
    }

    /// <summary>Switches between find-only and replace layout without reopening.</summary>
    public void ShowReplace(bool showReplace)
    {
        Title = showReplace ? "検索と置換" : "検索";

        var visibility = showReplace ? Visibility.Visible : Visibility.Collapsed;
        ReplaceLabel.Visibility = visibility;
        ReplaceTextBox.Visibility = visibility;
        ReplaceButton.Visibility = visibility;
        ReplaceAllButton.Visibility = visibility;
    }

    /// <summary>Seeds the search box, typically from the editor's current selection.</summary>
    public void SetSearchText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Contains('\n')) return;

        FindTextBox.Text = text;
        FindTextBox.SelectAll();
    }

    public void FocusSearchBox()
    {
        Activate();
        FindTextBox.Focus();
        FindTextBox.SelectAll();
    }

    /// <summary>Closes the dialog for good; used when the main window shuts down.</summary>
    public void CloseForReal()
    {
        _closingForReal = true;
        Close();
    }

    private SearchQuery BuildQuery()
    {
        var query = new SearchQuery(
            FindTextBox.Text,
            CaseSensitiveCheckBox.IsChecked == true,
            RegexCheckBox.IsChecked == true);

        _session.Query = query;
        _session.Wrap = WrapAroundCheckBox.IsChecked == true;

        return query;
    }

    private bool TryGetQuery(out SearchQuery query)
    {
        query = BuildQuery();

        if (query.Pattern.Length == 0)
        {
            StatusText.Text = "検索する文字列を入力してください。";
            return false;
        }

        if (query.Error is not null)
        {
            StatusText.Text = $"正規表現が不正です: {query.Error}";
            return false;
        }

        return true;
    }

    private void Query_Changed(object sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        _ = BuildQuery();
    }

    private void FindNextButton_Click(object sender, RoutedEventArgs e) => Find(forward: true);

    private void FindPreviousButton_Click(object sender, RoutedEventArgs e) => Find(forward: false);

    private void Find(bool forward)
    {
        if (!TryGetQuery(out var query)) return;

        string text = _editor.Text;
        bool wrap = WrapAroundCheckBox.IsChecked == true;

        // The current selection is the search anchor, so the dialog holds no
        // stale offset of its own when the document is edited between searches.
        int start = forward ? _editor.SelectionStart + _editor.SelectionLength : _editor.SelectionStart;

        var match = forward
            ? TextSearch.FindNext(text, query, start, wrap)
            : TextSearch.FindPrevious(text, query, start, wrap);

        if (match is null)
        {
            StatusText.Text = "見つかりませんでした。";
            return;
        }

        SelectMatch(match.Value);

        int total = TextSearch.FindAll(text, query).Count;
        StatusText.Text = $"{total} 件中 {LineIndex.Compute(text, match.Value.Index).Line} 行目を選択しました。";
    }

    private void ReplaceButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetQuery(out var query)) return;

        // Replace only when the selection really is the current hit; otherwise
        // this press just advances to it.
        var atSelection = TextSearch.FindNext(_editor.Text, query, _editor.SelectionStart, wrap: false);

        if (atSelection is { } hit &&
            hit.Index == _editor.SelectionStart &&
            hit.Length == _editor.SelectionLength &&
            hit.Length > 0)
        {
            string replacement = ExpandReplacement(query, _editor.SelectedText);

            _editor.Apply(new EditOperation(
                hit.Index, hit.Length, replacement, hit.Index + replacement.Length, 0));
        }

        Find(forward: true);
    }

    private void ReplaceAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetQuery(out var query)) return;

        string original = _editor.Text;
        var (replaced, count) = TextSearch.ReplaceAll(original, query, ReplaceTextBox.Text);

        if (count == 0)
        {
            StatusText.Text = "見つかりませんでした。";
            return;
        }

        // One replacement of the whole document: a single undoable change that
        // stays fast no matter how many matches there are.
        int caret = Math.Min(_editor.SelectionStart, replaced.Length);
        _editor.Apply(new EditOperation(0, original.Length, replaced, caret, 0));

        StatusText.Text = $"{count} 件を置換しました。";
    }

    /// <summary>Applies $1-style group substitution for a single regex replacement.</summary>
    private string ExpandReplacement(SearchQuery query, string matchedText)
    {
        string replacement = ReplaceTextBox.Text;
        if (!query.UseRegex) return replacement;

        var (expanded, count) = TextSearch.ReplaceAll(matchedText, query, replacement);
        return count > 0 ? expanded : replacement;
    }

    private void SelectMatch(SearchMatch match)
    {
        _editor.Select(match.Index, match.Length);
        _editor.ScrollToOffset(match.Index);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Kept alive and reused, so the close button only hides it.
        if (!_closingForReal)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}

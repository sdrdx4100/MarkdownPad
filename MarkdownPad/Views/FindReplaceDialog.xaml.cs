using System.Windows;
using MarkdownPad.Core.Text;
using MarkdownPad.Services;

namespace MarkdownPad.Views;

/// <summary>
/// Modeless find/replace. A single instance is reused by the main window, which
/// is why opening it twice no longer stacks windows on top of each other.
/// </summary>
public partial class FindReplaceDialog : Window
{
    private readonly TextBoxEditor _editor;
    private int _lastMatchIndex = -1;
    private bool _closingForReal;

    public FindReplaceDialog(TextBoxEditor editor, bool showReplace)
    {
        InitializeComponent();
        _editor = editor;

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

    private StringComparison Comparison => CaseSensitiveCheckBox.IsChecked == true
        ? StringComparison.Ordinal
        : StringComparison.OrdinalIgnoreCase;

    private bool Wrap => WrapAroundCheckBox.IsChecked == true;

    private void FindTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // A new search term restarts from the caret rather than from the last hit.
        _lastMatchIndex = -1;
        StatusText.Text = string.Empty;
    }

    private void FindNextButton_Click(object sender, RoutedEventArgs e) => Find(forward: true);

    private void FindPreviousButton_Click(object sender, RoutedEventArgs e) => Find(forward: false);

    private void Find(bool forward)
    {
        string pattern = FindTextBox.Text;
        if (string.IsNullOrEmpty(pattern))
        {
            StatusText.Text = "検索する文字列を入力してください。";
            return;
        }

        string text = _editor.Text;

        int start = forward
            ? (_lastMatchIndex >= 0 ? _lastMatchIndex + 1 : _editor.SelectionStart + _editor.SelectionLength)
            : (_lastMatchIndex >= 0 ? _lastMatchIndex : _editor.SelectionStart);

        int index = forward
            ? TextSearch.FindNext(text, pattern, start, Comparison, Wrap)
            : TextSearch.FindPrevious(text, pattern, start, Comparison, Wrap);

        if (index < 0)
        {
            _lastMatchIndex = -1;
            StatusText.Text = "見つかりませんでした。";
            return;
        }

        _lastMatchIndex = index;
        SelectMatch(index, pattern.Length);

        int total = TextSearch.FindAll(text, pattern, Comparison).Count;
        StatusText.Text = $"{total} 件中の 1 件を選択しました。（{LineIndex.Compute(text, index).Line} 行目）";
    }

    private void ReplaceButton_Click(object sender, RoutedEventArgs e)
    {
        string pattern = FindTextBox.Text;
        if (string.IsNullOrEmpty(pattern)) return;

        // Only replace when the current selection is in fact the search hit;
        // otherwise this press just moves to the next one.
        if (_editor.SelectionLength == pattern.Length &&
            string.Equals(_editor.SelectedText, pattern, Comparison))
        {
            _editor.Apply(new EditOperation(
                _editor.SelectionStart,
                _editor.SelectionLength,
                ReplaceTextBox.Text,
                _editor.SelectionStart + ReplaceTextBox.Text.Length,
                0));

            _lastMatchIndex = -1;
        }

        Find(forward: true);
    }

    private void ReplaceAllButton_Click(object sender, RoutedEventArgs e)
    {
        string pattern = FindTextBox.Text;
        if (string.IsNullOrEmpty(pattern))
        {
            StatusText.Text = "検索する文字列を入力してください。";
            return;
        }

        string original = _editor.Text;
        var (replaced, count) = TextSearch.ReplaceAll(original, pattern, ReplaceTextBox.Text, Comparison);

        if (count == 0)
        {
            StatusText.Text = "見つかりませんでした。";
            return;
        }

        // One replacement of the whole document: a single undoable change that
        // stays fast no matter how many matches there are. Assigning
        // TextBox.Text instead would discard the undo history entirely.
        int caret = Math.Min(_editor.SelectionStart, replaced.Length);
        _editor.Apply(new EditOperation(0, original.Length, replaced, caret, 0));

        _lastMatchIndex = -1;
        StatusText.Text = $"{count} 件を置換しました。";
    }

    private void SelectMatch(int index, int length)
    {
        _editor.Select(index, length);
        _editor.ScrollToOffset(index);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    /// <summary>Closes the dialog for good; used when the main window shuts down.</summary>
    public void CloseForReal()
    {
        _closingForReal = true;
        Close();
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

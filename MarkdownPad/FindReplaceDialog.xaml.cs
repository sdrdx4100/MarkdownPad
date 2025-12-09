using System.Windows;
using System.Windows.Controls;

namespace MarkdownPad;

public partial class FindReplaceDialog : Window
{
    private readonly TextBox _targetTextBox;
    private int _lastFoundIndex = -1;

    public FindReplaceDialog(TextBox targetTextBox, bool showReplace)
    {
        InitializeComponent();
        _targetTextBox = targetTextBox;

        if (!showReplace)
        {
            Title = "検索";
            ReplaceLabel.Visibility = Visibility.Collapsed;
            ReplaceTextBox.Visibility = Visibility.Collapsed;
            ReplaceButton.Visibility = Visibility.Collapsed;
            ReplaceAllButton.Visibility = Visibility.Collapsed;
        }

        FindTextBox.Focus();
    }

    private void FindNextButton_Click(object sender, RoutedEventArgs e)
    {
        FindNext();
    }

    private void ReplaceButton_Click(object sender, RoutedEventArgs e)
    {
        if (_targetTextBox.SelectionLength > 0 && 
            string.Equals(_targetTextBox.SelectedText, FindTextBox.Text, 
                CaseSensitiveCheckBox.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
        {
            _targetTextBox.SelectedText = ReplaceTextBox.Text;
        }
        FindNext();
    }

    private void ReplaceAllButton_Click(object sender, RoutedEventArgs e)
    {
        string findText = FindTextBox.Text;
        string replaceText = ReplaceTextBox.Text;

        if (string.IsNullOrEmpty(findText)) return;

        StringComparison comparison = CaseSensitiveCheckBox.IsChecked == true 
            ? StringComparison.Ordinal 
            : StringComparison.OrdinalIgnoreCase;

        string text = _targetTextBox.Text;
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(findText, index, comparison)) >= 0)
        {
            text = string.Concat(text.AsSpan(0, index), replaceText, text.AsSpan(index + findText.Length));
            index += replaceText.Length;
            count++;
        }

        if (count > 0)
        {
            _targetTextBox.Text = text;
            MessageBox.Show($"{count}件を置換しました。", "置換完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("見つかりませんでした。", "検索", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void FindNext()
    {
        string findText = FindTextBox.Text;
        if (string.IsNullOrEmpty(findText)) return;

        string text = _targetTextBox.Text;
        StringComparison comparison = CaseSensitiveCheckBox.IsChecked == true 
            ? StringComparison.Ordinal 
            : StringComparison.OrdinalIgnoreCase;

        int startIndex = _lastFoundIndex >= 0 ? _lastFoundIndex + 1 : _targetTextBox.SelectionStart + _targetTextBox.SelectionLength;
        
        if (startIndex >= text.Length)
        {
            startIndex = 0;
        }

        int index = text.IndexOf(findText, startIndex, comparison);

        // Wrap around if not found
        if (index < 0 && startIndex > 0)
        {
            index = text.IndexOf(findText, 0, comparison);
        }

        if (index >= 0)
        {
            _targetTextBox.Focus();
            _targetTextBox.SelectionStart = index;
            _targetTextBox.SelectionLength = findText.Length;
            _lastFoundIndex = index;
        }
        else
        {
            MessageBox.Show("見つかりませんでした。", "検索", MessageBoxButton.OK, MessageBoxImage.Information);
            _lastFoundIndex = -1;
        }
    }
}

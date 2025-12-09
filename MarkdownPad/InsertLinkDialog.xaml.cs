using System.Windows;

namespace MarkdownPad;

public partial class InsertLinkDialog : Window
{
    public string LinkText => LinkTextTextBox.Text;
    public string LinkUrl => LinkUrlTextBox.Text;

    public InsertLinkDialog()
    {
        InitializeComponent();
        LinkTextTextBox.Focus();
    }

    private void InsertButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LinkUrlTextBox.Text))
        {
            MessageBox.Show("URLを入力してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

using System.Windows;

namespace MarkdownPad.Views;

public partial class InsertLinkDialog : Window
{
    public InsertLinkDialog(string initialText = "")
    {
        InitializeComponent();

        LinkTextTextBox.Text = initialText;
        Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(initialText)) LinkTextTextBox.Focus();
            else LinkUrlTextBox.Focus();
        };
    }

    public string LinkText => LinkTextTextBox.Text;

    public string LinkUrl => LinkUrlTextBox.Text.Trim();

    private void InsertButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LinkUrl))
        {
            ValidationText.Text = "URL を入力してください。";
            LinkUrlTextBox.Focus();
            return;
        }

        DialogResult = true;
    }
}

using System.Windows;
using MarkdownPad.Core.Markdown;

namespace MarkdownPad.Views;

/// <summary>
/// Shows the Markdown cheat sheet in its own window instead of overwriting the
/// document the user is editing.
/// </summary>
public partial class MarkdownGuideWindow : Window
{
    public MarkdownGuideWindow()
    {
        InitializeComponent();
        GuideTextBox.Text = MarkdownGuide.Text;
    }

    /// <summary>Set when the user chose to insert the guide at the caret.</summary>
    public bool InsertRequested { get; private set; }

    private void InsertButton_Click(object sender, RoutedEventArgs e)
    {
        InsertRequested = true;
        DialogResult = true;
    }
}

using System.Windows.Input;

namespace MarkdownPad.Infrastructure;

/// <summary>
/// One entry in a data-bound menu, carrying its own command.
/// </summary>
/// <remarks>
/// Menu items live in a popup whose visual tree is detached from the window, so
/// <c>RelativeSource AncestorType=Window</c> cannot reach the view model from
/// inside an <c>ItemContainerStyle</c>. Giving each item its command directly
/// sidesteps that entirely.
/// </remarks>
public sealed record MenuCommandItem(string Header, ICommand Command, object? Parameter);

using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Rove.UI.Models;

public partial class FolderViewColumn : ObservableObject
{
    /// <summary>Narrowest a column can be resized to, so one can never be lost.</summary>
    public const double MinimumWidth = 56;

    /// <summary>Widest a column can get, so it can never push the rest off-screen.</summary>
    public const double MaximumWidth = 900;

    public required string Name { get; init; }

    /// <summary>Width this column starts at, and the one "reset" goes back to.</summary>
    public required double DefaultWidth { get; init; }

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private bool _isVisible;

    /// <summary>The column the keyboard is pointed at while resizing.</summary>
    [ObservableProperty]
    private bool _isActive;

    public void ResizeTo(double width) => Width = Math.Clamp(width, MinimumWidth, MaximumWidth);

    public void ResizeBy(double delta) => ResizeTo(Width + delta);

    public void ResetWidth() => ResizeTo(DefaultWidth);
}

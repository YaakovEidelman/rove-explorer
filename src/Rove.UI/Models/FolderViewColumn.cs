using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Rove.UI.Models;

public partial class FolderViewColumn : ObservableObject
{
    public const double MinimumWidth = 56;

    public const double MaximumWidth = 900;

    public required string Name { get; init; }

    public required double DefaultWidth { get; init; }

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isActive;

    public void ResizeTo(double width) => Width = Math.Clamp(width, MinimumWidth, MaximumWidth);

    public void ResizeBy(double delta) => ResizeTo(Width + delta);

    public void ResetWidth() => ResizeTo(DefaultWidth);
}

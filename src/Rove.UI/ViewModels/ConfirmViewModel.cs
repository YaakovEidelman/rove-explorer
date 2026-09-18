using CommunityToolkit.Mvvm.ComponentModel;
using Rove.UI.Services;
using System;

namespace Rove.UI.ViewModels;

/// <summary>
/// The visible confirm surface. While open it owns input (Mode.Confirm):
/// y/n run/discard the pending action directly, h/l move the highlight
/// between the two buttons and Enter runs whichever is highlighted.
/// Keyboard-only — the buttons are display only, not clickable. Destructive
/// verbs never run without this surface being on screen first.
/// </summary>
public partial class ConfirmViewModel : ViewModelBase
{
    private Action? _pending;

    public ConfirmViewModel(CommandRegistry registry)
    {
        registry.Register(CommandDef.ConfirmAccept, Accept);
        registry.Register(CommandDef.ConfirmCancel, Cancel);
        registry.Register(CommandDef.ConfirmSelect, Select);
        registry.Register(CommandDef.ConfirmMoveLeft, MoveLeft);
        registry.Register(CommandDef.ConfirmMoveRight, MoveRight);
    }

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>Which button h/l/Enter act on — Cancel is the left button, the safe default.</summary>
    [ObservableProperty]
    private bool _cancelHighlighted = true;

    public void Request(string message, Action onAccept)
    {
        Message = message;
        _pending = onAccept;
        CancelHighlighted = true;
        IsOpen = true;
    }

    public void Accept()
    {
        Action? pending = _pending;
        Close();
        pending?.Invoke();
    }

    public void Cancel() => Close();

    public void Select()
    {
        if (CancelHighlighted)
            Cancel();
        else
            Accept();
    }

    public void MoveLeft() => CancelHighlighted = true;

    public void MoveRight() => CancelHighlighted = false;

    private void Close()
    {
        _pending = null;
        IsOpen = false;
        Message = string.Empty;
    }
}

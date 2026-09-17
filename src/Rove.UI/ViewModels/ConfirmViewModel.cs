using CommunityToolkit.Mvvm.ComponentModel;
using Rove.UI.Services;
using System;

namespace Rove.UI.ViewModels;

/// <summary>
/// The visible confirm surface. While open it owns input (Mode.Confirm):
/// y/Enter runs the pending action, n/Esc discards it. Destructive verbs
/// never run without this surface being on screen first.
/// </summary>
public partial class ConfirmViewModel : ViewModelBase
{
    private Action? _pending;

    public ConfirmViewModel(CommandRegistry registry)
    {
        registry.Register(CommandDef.ConfirmAccept, Accept);
        registry.Register(CommandDef.ConfirmCancel, Cancel);
    }

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _message = string.Empty;

    public void Request(string message, Action onAccept)
    {
        Message = message;
        _pending = onAccept;
        IsOpen = true;
    }

    public void Accept()
    {
        Action? pending = _pending;
        Close();
        pending?.Invoke();
    }

    public void Cancel() => Close();

    private void Close()
    {
        _pending = null;
        IsOpen = false;
        Message = string.Empty;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class ConfirmViewModel : ViewModelBase
{
    private Action? _pending;
    private Action? _onCancel;

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

    [ObservableProperty]
    private bool _cancelHighlighted = true;

    public void Request(string message, Action onAccept) => Request(message, onAccept, onCancel: null);

    public void Request(string message, Action onAccept, Action? onCancel)
    {
        Action? dropped = _onCancel;
        _onCancel = null;
        dropped?.Invoke();
        Message = message;
        _pending = onAccept;
        _onCancel = onCancel;
        CancelHighlighted = true;
        IsOpen = true;
    }

    public void Accept()
    {
        Action? pending = _pending;
        _onCancel = null;
        Close();
        pending?.Invoke();
    }

    public void Cancel()
    {
        Action? onCancel = _onCancel;
        _onCancel = null;
        Close();
        onCancel?.Invoke();
    }

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

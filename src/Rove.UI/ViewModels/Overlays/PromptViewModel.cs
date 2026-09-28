using CommunityToolkit.Mvvm.ComponentModel;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class PromptViewModel : ViewModelBase
{
    private TaskCompletionSource<string?>? _pending;

    public PromptViewModel(ICommandTarget registry)
    {
        registry.Register(CommandDef.PromptApply, Apply);
        registry.Register(CommandDef.PromptCancel, Cancel);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FocusPlain), nameof(FocusSecret))]
    private bool _isOpen;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FocusPlain), nameof(FocusSecret))]
    private bool _isSecret;

    public bool FocusPlain => IsOpen && !IsSecret;

    public bool FocusSecret => IsOpen && IsSecret;

    public Task<string?> AskAsync(string message, string label, bool secret, string? initial = null)
    {
        _pending?.TrySetResult(null);
        TaskCompletionSource<string?> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = pending;
        Message = message;
        Label = label;
        Text = initial ?? string.Empty;
        IsSecret = secret;
        IsOpen = true;
        return pending.Task;
    }

    public void Apply() => Finish(Text);

    public void Cancel() => Finish(null);

    private void Finish(string? answer)
    {
        TaskCompletionSource<string?>? pending = _pending;
        _pending = null;
        IsOpen = false;
        Text = string.Empty;
        pending?.TrySetResult(answer);
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Protocol;
using Rove.UI.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rove.UI.ViewModels;

public partial class FileOperationViewModel : ViewModelBase
{
    private CancellationTokenSource? _cancellation;

    public FileOperationViewModel(CommandRegistry registry)
    {
        registry.Register(CommandDef.CancelFileOperation, Cancel);
    }

    public event Action<string>? InfoRaised;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _detail = string.Empty;

    [ObservableProperty]
    private double _percent;

    [ObservableProperty]
    private bool _isIndeterminate;

    [ObservableProperty]
    private bool _isCancelling;

    public async Task<CommandResult<OpResult[]>?> RunAsync(
        string title,
        bool indeterminate,
        Func<IProgress<FileOpProgress>, CancellationToken, Task<CommandResult<OpResult[]>>> work
    )
    {
        if (IsRunning)
        {
            InfoRaised?.Invoke("Another file operation is still running.");
            return null;
        }

        _cancellation = new CancellationTokenSource();
        Title = title;
        Detail = string.Empty;
        Percent = 0;
        IsIndeterminate = indeterminate;
        IsCancelling = false;
        IsRunning = true;

        Progress<FileOpProgress> progress = new(OnProgress);

        try
        {
            return await work(progress, _cancellation.Token);
        }
        finally
        {
            IsRunning = false;
            IsCancelling = false;
            Detail = string.Empty;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    private void OnProgress(FileOpProgress progress)
    {
        if (!IsRunning)
            return;
        Detail = progress.CurrentItem;
        if (progress.Total > 0)
        {
            IsIndeterminate = false;
            Percent = progress.Fraction * 100;
        }
    }

    public void Cancel()
    {
        if (!IsRunning || _cancellation is null)
        {
            InfoRaised?.Invoke("Nothing to cancel.");
            return;
        }
        if (IsCancelling)
            return;
        IsCancelling = true;
        Detail = "Cancelling…";
        _cancellation.Cancel();
    }
}

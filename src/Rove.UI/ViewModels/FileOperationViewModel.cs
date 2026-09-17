using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Protocol;
using Rove.UI.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rove.UI.ViewModels;

/// <summary>
/// The one long-running file operation the app will run at a time: copy,
/// move, or delete. The work happens off the UI thread, so the window keeps
/// painting and answering keys while it runs; this holds what to show about
/// it and the switch that stops it.
/// </summary>
public partial class FileOperationViewModel : ViewModelBase
{
    private CancellationTokenSource? _cancellation;

    public FileOperationViewModel(CommandRegistry registry)
    {
        registry.Register(CommandDef.CancelFileOperation, Cancel);
    }

    /// <summary>Raised when the user asks to cancel, so the status bar can say so.</summary>
    public event Action<string>? InfoRaised;

    [ObservableProperty]
    private bool _isRunning;

    /// <summary>"Copying 3 items", "Deleting 12 items" — what is happening.</summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>The item being touched right now.</summary>
    [ObservableProperty]
    private string _detail = string.Empty;

    [ObservableProperty]
    private double _percent;

    /// <summary>True while there is no meaningful count to show (the shell's own batch delete).</summary>
    [ObservableProperty]
    private bool _isIndeterminate;

    [ObservableProperty]
    private bool _isCancelling;

    /// <summary>
    /// Runs <paramref name="work"/> in the background with a progress sink and
    /// a cancellation token wired up. Comes back null when another operation
    /// is already running — one at a time keeps the reporting honest.
    /// </summary>
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

        // Built here, on the UI thread, so every report comes back here too.
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

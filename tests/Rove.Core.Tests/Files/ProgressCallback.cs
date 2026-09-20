using Rove.Core.Protocol;

namespace Rove.Core.Tests;

internal sealed class ProgressCallback : IProgress<FileOpProgress>
{
    private readonly Action<FileOpProgress> _onReport;

    public ProgressCallback(Action<FileOpProgress> onReport)
    {
        _onReport = onReport;
    }

    public void Report(FileOpProgress value) => _onReport(value);
}

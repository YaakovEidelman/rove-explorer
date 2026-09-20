using Rove.Core.Protocol;

namespace Rove.Core.Tests;

internal sealed class ProgressLog : IProgress<FileOpProgress>
{
    public List<FileOpProgress> Reports { get; } = [];

    public void Report(FileOpProgress value) => Reports.Add(value);
}

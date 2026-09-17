namespace Rove.Core.Protocol;

/// <summary>
/// How far along a long-running file operation is. <see cref="Completed"/>
/// counts finished top-level items; <see cref="CurrentItem"/> is whatever is
/// being touched right now (a file deep inside a folder being copied), so the
/// UI can say something specific while a single item takes a long time.
/// </summary>
public record FileOpProgress(string Verb, int Completed, int Total, string CurrentItem)
{
    public double Fraction => Total <= 0 ? 0 : Math.Clamp((double)Completed / Total, 0, 1);
}

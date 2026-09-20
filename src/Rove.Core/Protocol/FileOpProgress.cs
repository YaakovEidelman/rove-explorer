namespace Rove.Core.Protocol;

public record FileOpProgress(string Verb, int Completed, int Total, string CurrentItem)
{
    public double Fraction => Total <= 0 ? 0 : Math.Clamp((double)Completed / Total, 0, 1);
}

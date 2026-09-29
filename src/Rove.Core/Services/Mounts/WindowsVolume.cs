namespace Rove.Core.Services;

public sealed record WindowsVolume(string Root, string Label, string? Model, uint Serial, long Size);

namespace Rove.Core.Services;

public sealed record UDisksDrive(string Path, bool Removable, bool Ejectable, bool CanPowerOff, string Model = "");

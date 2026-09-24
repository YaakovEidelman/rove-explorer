namespace Rove.Core.Services;

public readonly record struct PathCrumb(string Label, string FullPath, bool IsLast = false);

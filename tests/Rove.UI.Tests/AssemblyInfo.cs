using Xunit;

// The headless tests own a UI thread for the whole assembly; letting other
// test classes run beside them puts two things at once through a platform
// that is built to have one.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

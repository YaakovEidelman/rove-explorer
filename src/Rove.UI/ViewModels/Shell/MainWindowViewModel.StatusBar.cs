using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class MainWindowViewModel
{
    public string ItemSummary
    {
        get
        {
            int total = ContentPage.DirectoryListing.Items.Count;
            int marked = ContentPage.DirectoryListing.Items.Count(i => i.IsMarked);
            string summary = $"{total} item{(total == 1 ? "" : "s")}";
            if (marked > 0)
                summary += $" · {marked} marked";
            if (ContentPage.DirectoryListing.ShowHidden)
                summary += " · hidden shown";
            if (ContentPage.InArchive)
                summary += " · in an archive";
            return summary;
        }
    }

    public string ClipboardSummary
    {
        get
        {
            if (!_clipboard.HasItems)
                return "";
            string verb = _clipboard.Op == ClipboardOp.Copy ? "copied" : "cut";
            return $"{_clipboard.Paths.Count} {verb}";
        }
    }

    public bool IsTransferPending => _clipboard.HasItems && _clipboard.OneShot;

    public string TransferBanner
    {
        get
        {
            if (!_clipboard.HasItems || !_clipboard.OneShot)
                return "";
            string verb = _clipboard.Op == ClipboardOp.Copy ? "Copying" : "Moving";
            int count = _clipboard.Paths.Count;
            return $"{verb} {count} item{(count == 1 ? "" : "s")} — go to a folder and press P to drop it here · Esc to cancel";
        }
    }

    private void RefreshStatusBar()
    {
        OnPropertyChanged(nameof(ModeHint));
        OnPropertyChanged(nameof(StatusLine));
        OnPropertyChanged(nameof(IsErrorShown));
        OnPropertyChanged(nameof(ItemSummary));
        OnPropertyChanged(nameof(ClipboardSummary));
        OnPropertyChanged(nameof(IsTransferPending));
        OnPropertyChanged(nameof(TransferBanner));
    }
}

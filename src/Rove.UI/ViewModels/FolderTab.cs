using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rove.Core.Services;
using Rove.UI.Services;
using System;
using System.ComponentModel;
using System.IO;

namespace Rove.UI.ViewModels;

/// <summary>
/// One tab: a folder view, and the name the strip shows for it. The name is
/// worked out from where the view is rather than stored, so a tab cannot end
/// up labelled one place while showing another.
/// </summary>
public sealed partial class FolderTab : ObservableObject, IDisposable
{
    public ContentViewModel Content { get; }

    /// <summary>This tab's own handlers for the commands every tab answers for.</summary>
    public TabCommands Commands { get; }

    /// <summary>Click on the tab itself: brings it to the front.</summary>
    public IRelayCommand ActivateCommand { get; }

    /// <summary>Click on the tab's x: closes it.</summary>
    public IRelayCommand CloseCommand { get; }

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _editText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string? _customTitle;

    public FolderTab(ContentViewModel content, TabCommands commands, Action<FolderTab> activate, Action<FolderTab> close)
    {
        Content = content;
        Commands = commands;
        Content.DirectoryListing.PropertyChanged += OnListingChanged;
        ActivateCommand = new RelayCommand(() => activate(this));
        CloseCommand = new RelayCommand(() => close(this));
    }

    /// <summary>What the strip calls this tab: the folder's own name.</summary>
    public string Title => CustomTitle is { Length: > 0 } custom ? custom : NameOf(Content.DirectoryListing.CurrentDir);

    private void OnListingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DirectoryListing.CurrentDir))
            OnPropertyChanged(nameof(Title));
    }

    public void BeginRename()
    {
        EditText = Title;
        IsRenaming = true;
    }

    public void ApplyRename()
    {
        string trimmed = EditText.Trim();
        CustomTitle = trimmed.Length > 0 ? trimmed : null;
        IsRenaming = false;
    }

    public void CancelRename() => IsRenaming = false;

    /// <summary>
    /// The last step of a path, which is what a person calls the place they
    /// are in. A drive root has no last step and is its own name.
    /// </summary>
    private static string NameOf(string directory)
    {
        string display = LongPath.Display(directory ?? string.Empty);
        if (display.Length == 0)
            return "Rove";
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(display));
        return name.Length > 0 ? name : display;
    }

    public void Dispose()
    {
        Content.DirectoryListing.PropertyChanged -= OnListingChanged;
        Content.Close();
    }
}

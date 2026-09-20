using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rove.Core.Services;
using Rove.UI.Services;
using System;
using System.ComponentModel;
using System.IO;

namespace Rove.UI.ViewModels;

public sealed partial class FolderTab : ObservableObject, IDisposable
{
    public ContentViewModel Content { get; }

    public TabCommands Commands { get; }

    public IRelayCommand ActivateCommand { get; }

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
        Content.PropertyChanged += OnContentChanged;
        ActivateCommand = new RelayCommand(() => activate(this));
        CloseCommand = new RelayCommand(() => close(this));
    }

    public string Title => CustomTitle is { Length: > 0 } custom ? custom : NameOf(Content.DirectoryListing.CurrentDir);

    public bool IsAdmin => Content.IsAdminView;

    private void OnContentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ContentViewModel.IsAdminView))
            OnPropertyChanged(nameof(IsAdmin));
    }

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
        Content.PropertyChanged -= OnContentChanged;
        Content.Close();
    }
}

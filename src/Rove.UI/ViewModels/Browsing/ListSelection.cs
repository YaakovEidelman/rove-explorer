using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Services;

namespace Rove.UI.ViewModels;

public partial class ListSelection : ObservableObject
{
    private readonly IReadOnlyList<ListViewItem> _items;

    private int _position = -1;

    private bool _isCommitting;

    private int _columnsPerRow = 1;

    public ListSelection(IReadOnlyList<ListViewItem> items)
    {
        _items = items;
    }

    [ObservableProperty]
    private int _index = -1;

    public string? Anchor { get; private set; }

    public ListViewItem? SelectedItem => ItemAt(_position);

    public void Select(int index) => Commit(index);

    public void SelectPath(string path)
    {
        int i = IndexOf(path);
        if (i >= 0)
            Commit(i);
    }

    public void SetColumnsPerRow(int columns) => _columnsPerRow = Math.Max(1, columns);

    public void MoveUp()
    {
        if (_columnsPerRow <= 1)
        {
            Commit(_position <= 0 ? _items.Count - 1 : _position - 1);
            return;
        }
        if (_position < 0)
            return;
        int target = _position - _columnsPerRow;
        if (target >= 0)
            Commit(target);
    }

    public void MoveDown()
    {
        if (_columnsPerRow <= 1)
        {
            Commit(_position >= _items.Count - 1 ? 0 : _position + 1);
            return;
        }
        int target = _position < 0 ? 0 : _position + _columnsPerRow;
        if (target < _items.Count)
            Commit(target);
    }

    public void MoveLeft()
    {
        if (_position > 0)
            Commit(_position - 1);
    }

    public void MoveRight()
    {
        if (_position >= 0 && _position < _items.Count - 1)
            Commit(_position + 1);
    }

    public void MoveTop() => Commit(0);

    public void MoveBottom() => Commit(_items.Count - 1);

    public void Detach() => Write(-1);

    public void Reconcile()
    {
        int anchored = Anchor is not null ? IndexOf(Anchor) : -1;
        Commit(anchored >= 0 ? anchored : _position);
    }

    partial void OnIndexChanged(int value)
    {
        if (_isCommitting || value == _position)
            return;
        if (value >= 0)
            Commit(value);
    }

    private void Commit(int index)
    {
        _position = Clamp(index);
        Anchor = ItemAt(_position)?.Item.FullPath;
        Write(_position);
    }

    private void Write(int index)
    {
        _isCommitting = true;
        try
        {
            Index = index;
        }
        finally
        {
            _isCommitting = false;
        }
    }

    private int IndexOf(string path)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (PathCompare.PathMatches(path, _items[i].Item.FullPath))
                return i;
        }
        return -1;
    }

    private ListViewItem? ItemAt(int index) =>
        index >= 0 && index < _items.Count ? _items[index] : null;

    private int Clamp(int index)
    {
        if (_items.Count == 0)
            return -1;
        return Math.Clamp(index, 0, _items.Count - 1);
    }
}

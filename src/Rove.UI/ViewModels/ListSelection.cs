using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Services;
using System;
using System.Collections.Generic;

namespace Rove.UI.ViewModels;

/// <summary>
/// The highlight (single moving selection) over a list of items. Anchored by
/// path so the highlight follows its item across re-sorts and filters; when
/// the anchored item disappears (e.g. deleted) the highlight stays at the
/// same position instead of jumping to the top.
///
/// <para>
/// The list control mirrors <see cref="Index"/> two-way, so it writes back
/// here as well — both when the user clicks a row and, unhelpfully, while
/// rows are being added and removed underneath it. <see cref="_position"/>
/// is therefore the model's own copy of the highlight: nothing in here ever
/// reads <see cref="Index"/> back as state, because between writing it and
/// reading it the control may have replaced it.
/// </para>
/// </summary>
public partial class ListSelection : ObservableObject
{
    private readonly IReadOnlyList<ListViewItem> _items;

    /// <summary>Where the model put the highlight. The control can't touch this.</summary>
    private int _position = -1;

    /// <summary>True while a model-driven write is in flight (see <see cref="Commit"/>).</summary>
    private bool _isCommitting;

    /// <summary>
    /// How many items a row holds in the current view. 1 in the list view,
    /// where up/down still means "the item before/after this one" and wraps
    /// at either end; more than that in the icon view, where the grid the
    /// user actually sees means up/down has to skip a whole row at a time —
    /// and, past the first or last row, simply stop rather than guess at
    /// what "wrap" would even mean on a ragged last row.
    /// </summary>
    private int _columnsPerRow = 1;

    public ListSelection(IReadOnlyList<ListViewItem> items)
    {
        _items = items;
    }

    /// <summary>Highlighted row, mirrored two-way by the list control.</summary>
    [ObservableProperty]
    private int _index = -1;

    /// <summary>Path of the highlighted item — how the highlight is re-found.</summary>
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

    /// <summary>Reading order, one item at a time — crosses a row's edge into the next.</summary>
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

    /// <summary>
    /// Lifts the highlight off the control before the list is rebuilt, keeping
    /// the anchor. Without this the control holds an index into rows that are
    /// being removed and pushes that stale index back at the model mid-change.
    /// <see cref="Reconcile"/> puts the highlight back afterwards.
    /// </summary>
    public void Detach() => Write(-1);

    /// <summary>Re-locates the highlight after the list changed under it.</summary>
    public void Reconcile()
    {
        int anchored = Anchor is not null ? IndexOf(Anchor) : -1;
        // Anchored item is gone: hold position (clamped), re-anchor there.
        Commit(anchored >= 0 ? anchored : _position);
    }

    /// <summary>The control moved the highlight on its own — the user clicked a row.</summary>
    partial void OnIndexChanged(int value)
    {
        if (_isCommitting || value == _position)
            return;
        // A "nothing selected" write is the control reacting to rows changing,
        // not a choice the user made; the model's position stands.
        if (value >= 0)
            Commit(value);
    }

    /// <summary>Moves the highlight and re-anchors it, model first, control second.</summary>
    private void Commit(int index)
    {
        _position = Clamp(index);
        Anchor = ItemAt(_position)?.Item.FullPath;
        Write(_position);
    }

    /// <summary>
    /// Pushes a value to the control. Flagged so the write-back the control
    /// answers with is recognised as an echo rather than a user click.
    /// </summary>
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

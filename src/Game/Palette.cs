using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rummage.Core;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// The search box: one line of text with ranked results under it. Arrows choose, Tab completes
/// the chosen name, Enter takes, Escape closes. A number typed before or after the name is the
/// count; otherwise the slider sets it.
/// </summary>
internal sealed class Palette
{
    private const string FieldName = "RummageSearch";
    private const int VisibleRows = 10;
    private const float Width = 640f;
    private const float RowHeight = 30f;
    private const float Top = 140f;

    private readonly Grabber _grabber;
    private Catalog _catalog;
    private Target _context;
    private Entry _contextEntry;
    private IReadOnlyList<Entry> _results = Array.Empty<Entry>();
    private string _text = string.Empty;
    private string _rankedFor;
    private int _selected;
    private int _first;
    private Entry _countFor;
    private float _count = 1f;
    private int _focusFrames;
    private bool _cursorToEnd;

    public Palette(Grabber grabber)
    {
        _grabber = grabber;
    }

    public static bool IsOpen { get; private set; }

    /// <summary>The search text, as the self-test types it.</summary>
    internal string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            Rank();
        }
    }

    internal IReadOnlyList<Entry> Results => _results;

    /// <summary>The chosen entry's parts line, for the self-test.</summary>
    internal string ChosenParts => PartsOf(Chosen);

    /// <summary>Enter, as the self-test presses it.</summary>
    internal void TakeChosen(Player player)
    {
        Rank();
        Take(player);
    }

    /// <summary>Opens the search box, starting from what the player is looking at when there is something.</summary>
    public void Open(Player player, Target context)
    {
        _catalog = Catalog.Build(player, Scene.Read(player));
        _context = context;
        _contextEntry = context == null ? null : new Entry(string.Empty, context.Label, context.Kind, context.Count);
        _text = context?.Shown ?? string.Empty;
        _rankedFor = null;
        _selected = 0;
        _first = 0;
        _countFor = null;
        _focusFrames = 3;
        _cursorToEnd = true;
        IsOpen = true;
        Rank();
    }

    public void Close()
    {
        IsOpen = false;
        _catalog = null;
        GUIUtility.keyboardControl = 0;
    }

    public void OnGUI()
    {
        Skin.Warm();
        if (!IsOpen)
        {
            return;
        }

        var player = Player.m_localPlayer;
        if (!player || player.IsDead())
        {
            Close();
            return;
        }

        Skin.Build();
        Rank();
        Keys(player);
        if (IsOpen)
        {
            Draw(player);
        }
    }

    private void Rank()
    {
        if (string.Equals(_rankedFor, _text, StringComparison.Ordinal))
        {
            return;
        }

        _rankedFor = _text;
        var query = Query.Parse(_text);
        var ranked = Search.Rank(query.Text, _catalog.Entries).ToList();
        if (_contextEntry != null && string.Equals(query.Text, _context.Shown, StringComparison.OrdinalIgnoreCase))
        {
            ranked.Insert(0, _contextEntry);
        }

        _results = ranked;
        _selected = 0;
        _first = 0;
    }

    private Entry Chosen => _selected >= 0 && _selected < _results.Count ? _results[_selected] : null;

    private Target TargetOf(Entry entry) => entry == _contextEntry ? _context : _catalog.TargetOf(entry);

    /// <summary>Where the count starts for an entry: what the player was looking at asks for its own count, an item a stack.</summary>
    private int StartCount(Entry entry)
    {
        if (entry == _contextEntry)
        {
            return _context.Count;
        }

        return entry.Kind == EntryKind.Item ? Math.Max(1, Math.Min(_catalog.StackSize(entry.Key), entry.InChests)) : 1;
    }

    /// <summary>The slider's top: what the chests give of an item, or how many of a piece or recipe can be paid for.</summary>
    private int MaxCount(Entry entry) => Math.Max(StartCount(entry), Math.Max(1, entry.InChests));

    private int CountFor(Entry entry) => Query.Parse(_text).Count ?? Mathf.RoundToInt(_count);

    private void Keys(Player player)
    {
        var e = Event.current;
        if (e.type != EventType.KeyDown)
        {
            return;
        }

        switch (e.keyCode)
        {
            case KeyCode.Escape:
                Close();
                break;
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                Take(player);
                break;
            case KeyCode.UpArrow:
                Choose(_selected - 1);
                break;
            case KeyCode.DownArrow:
                Choose(_selected + 1);
                break;
            case KeyCode.PageUp:
                Choose(_selected - VisibleRows);
                break;
            case KeyCode.PageDown:
                Choose(_selected + VisibleRows);
                break;
            case KeyCode.Tab:
                Complete();
                break;
            default:
                return;
        }

        e.Use();
    }

    private void Choose(int index)
    {
        if (_results.Count == 0)
        {
            return;
        }

        _selected = Mathf.Clamp(index, 0, _results.Count - 1);
        if (_selected < _first)
        {
            _first = _selected;
        }
        else if (_selected >= _first + VisibleRows)
        {
            _first = _selected - VisibleRows + 1;
        }
    }

    /// <summary>Tab puts the chosen name into the box, keeping a typed count.</summary>
    private void Complete()
    {
        var chosen = Chosen;
        if (chosen == null)
        {
            return;
        }

        string name = chosen == _contextEntry ? _context.Shown : chosen.Shown;
        var query = Query.Parse(_text);
        _text = query.Count is int count ? $"{count.ToString(CultureInfo.InvariantCulture)} {name}" : name;
        _cursorToEnd = true;
    }

    private void Take(Player player)
    {
        var chosen = Chosen;
        var target = chosen == null ? null : TargetOf(chosen);
        if (target == null)
        {
            return;
        }

        target = target.WithCount(CountFor(chosen));
        Close();
        _grabber.Grab(player, target, Time.time);
    }

    private void Draw(Player player)
    {
        float scale = Mathf.Max(0.5f, Screen.height / 1080f);
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float x = (Screen.width / scale - Width) / 2f;
        int rows = Math.Min(VisibleRows, Math.Max(1, _results.Count));
        var panel = new Rect(x, Top, Width, 60f + rows * RowHeight + 118f);
        GUI.Box(panel, GUIContent.none, Skin.Box);

        DrawField(new Rect(x + 12f, Top + 12f, Width - 24f, 36f));
        DrawRows(new Rect(x + 12f, Top + 56f, Width - 24f, rows * RowHeight), player);
        if (!IsOpen)
        {
            GUI.matrix = matrix;
            return;
        }

        GUI.Label(new Rect(x + 14f, panel.yMax - 110f, Width - 28f, 28f), PartsOf(Chosen), Skin.Hint);
        DrawCount(new Rect(x + 12f, panel.yMax - 76f, Width - 24f, 34f), player);
        GUI.Label(new Rect(x + 14f, panel.yMax - 36f, Width - 28f, 26f),
            "Enter take · Tab complete · ↑↓ choose · Esc close · type a number for the count", Skin.Hint);

        GUI.matrix = matrix;
    }

    private void DrawField(Rect area)
    {
        GUI.SetNextControlName(FieldName);
        _text = GUI.TextField(area, _text, 80, Skin.Field);
        if (_focusFrames > 0)
        {
            GUI.FocusControl(FieldName);
            _focusFrames--;
        }

        if (_cursorToEnd && GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) is TextEditor editor)
        {
            editor.text = _text;
            editor.MoveTextEnd();
            _cursorToEnd = false;
        }

        if (_text.Length == 0)
        {
            GUI.Label(new Rect(area.x + 12f, area.y, area.width, area.height), "Search the chests, your pieces and recipes", Skin.Hint);
        }
    }

    private void DrawRows(Rect area, Player player)
    {
        var e = Event.current;
        if (e.type == EventType.ScrollWheel && area.Contains(e.mousePosition))
        {
            _first = Mathf.Clamp(_first + (e.delta.y > 0 ? 3 : -3), 0, Math.Max(0, _results.Count - VisibleRows));
            e.Use();
        }

        if (_results.Count == 0)
        {
            GUI.Label(area, Query.Parse(_text).Text.Length == 0 ? "The chests in reach hold nothing to take" : "Nothing found", Skin.Hint);
            return;
        }

        for (int row = 0; row < VisibleRows && _first + row < _results.Count; row++)
        {
            int index = _first + row;
            var entry = _results[index];
            var rect = new Rect(area.x, area.y + row * RowHeight, area.width, RowHeight);
            if (index == _selected)
            {
                GUI.DrawTexture(rect, Skin.SelectedFill);
            }
            else if (rect.Contains(e.mousePosition))
            {
                GUI.DrawTexture(rect, Skin.HoverFill);
            }

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                _selected = index;
                if (e.clickCount > 1)
                {
                    Take(player);
                }

                e.Use();
                if (!IsOpen)
                {
                    return;
                }
            }

            GUI.Label(new Rect(rect.x + 8f, rect.y, rect.width * 0.62f, rect.height), entry.Shown, Skin.Name);
            GUI.Label(new Rect(rect.x + rect.width * 0.55f, rect.y, rect.width * 0.45f - 8f, rect.height), Detail(entry), Skin.Detail);
        }
    }

    private void DrawCount(Rect area, Player player)
    {
        var chosen = Chosen;
        if (chosen == null)
        {
            return;
        }

        if (_countFor != chosen)
        {
            _countFor = chosen;
            _count = StartCount(chosen);
        }

        int? typed = Query.Parse(_text).Count;
        GUI.Label(new Rect(area.x, area.y, 70f, area.height), "Count", Skin.Name);
        bool enabled = GUI.enabled;
        GUI.enabled = typed == null;
        _count = GUI.HorizontalSlider(new Rect(area.x + 76f, area.y + 12f, area.width - 300f, area.height), _count, 1f, MaxCount(chosen));
        GUI.enabled = enabled;
        GUI.Label(new Rect(area.xMax - 216f, area.y, 70f, area.height), CountFor(chosen).ToString("N0", CultureInfo.InvariantCulture), Skin.Name);
        if (GUI.Button(new Rect(area.xMax - 140f, area.y, 140f, area.height), "Take", Skin.Button))
        {
            Take(player);
        }
    }

    /// <summary>
    /// For the chosen entry: an item as carried and in the chests; a piece or recipe part by part,
    /// carried plus in the chests against what the count needs, short parts marked.
    /// </summary>
    private string PartsOf(Entry entry)
    {
        var target = entry == null ? null : TargetOf(entry);
        if (target == null || target.Cost.Count == 0)
        {
            return string.Empty;
        }

        if (target.Kind == EntryKind.Item)
        {
            string name = target.Cost[0].Name;
            return Readout.Item(_catalog.Carried.TryGetValue(name, out int c) ? c : 0, _catalog.InChests.TryGetValue(name, out int t) ? t : 0);
        }

        return Readout.Parts(Cost.Sum(target.Cost), CountFor(entry), _catalog.Carried, _catalog.InChests, Rummage.Game.Text.Shown);
    }

    private string Detail(Entry entry)
    {
        string many = entry.InChests.ToString("N0", CultureInfo.InvariantCulture);
        if (entry == _contextEntry)
        {
            return entry.Kind switch
            {
                EntryKind.Recipe => "selected recipe",
                EntryKind.Piece => "selected piece",
                _ => "your stack, to a full stack",
            };
        }

        return entry.Kind switch
        {
            EntryKind.Item => $"{many} in chests",
            EntryKind.Piece => entry.InChests > 0 ? $"piece · enough for {many}" : "piece · not enough",
            _ => entry.InChests > 0 ? $"recipe · enough for {many}" : "recipe · not enough",
        };
    }
}

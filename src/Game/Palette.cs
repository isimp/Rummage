using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rummage.Core;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// The search box: one line of text with ranked results under it. Arrows choose, Tab completes
/// the chosen name, Enter finds or takes, Escape closes. A number typed before or after the name
/// is the count; otherwise the slider sets it. What it last found or took (<see cref="LastUsed"/>)
/// is listed first with nothing typed and marked with the game's circling arrows.
/// </summary>
internal sealed class Palette
{
    private const string FieldName = "RummageSearch";
    private static int VisibleRows => Settings.Rows.Value;
    private const float Width = 640f;
    private const float RowHeight = 32f;
    private const float Pad = 30f;
    private const float Top = 140f;

    /// <summary>The size of the last used mark, the game's own 20 pixel circling arrows.</summary>
    private const float LastUsedMark = 18f;

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
    private bool _dragging;
    private float _count = 1f;
    private int _focusFrames;
    private readonly Dictionary<Entry, string> _details = new();
    private Entry _partsFor;
    private int _partsCount = -1;
    private string _parts = string.Empty;
    private string _queryFor;
    private Query _query;
    private List<string> _names = new();
    private bool _cursorToEnd;

    public Palette(Grabber grabber)
    {
        _grabber = grabber;
    }

    public static bool IsOpen { get; private set; }

    private static int _closedFrame = -10;

    /// <summary>
    /// Whether the game's keys are kept from the game: while the box is open, and for two frames
    /// after it closes, as the key that closed it (Enter, Escape) is still down then and would
    /// otherwise open the chat or the menu.
    /// </summary>
    public static bool HoldsKeys => IsOpen || Time.frameCount <= _closedFrame + 2;

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

    /// <summary>Tab, as the self-test presses it.</summary>
    internal void PressTab()
    {
        Rank();
        Complete();
    }

    /// <summary>The count read from the text, for the self-test.</summary>
    internal int? TypedCount => Parsed.Count;

    /// <summary>Enter, or Shift+Enter, as the self-test presses it.</summary>
    internal void PressEnter(Player player, bool shift = false)
    {
        Rank();
        Enter(player, shift);
    }

    /// <summary>The key hints under the box, for the self-test.</summary>
    internal string KeysHint => Hint();

    /// <summary>A row's text on its right, for the self-test.</summary>
    internal string DetailOf(Entry entry) => Detail(entry);

    /// <summary>Whether a row carries the last used mark, for the self-test.</summary>
    internal bool Marked(Entry entry) => IsLastUsed(entry);

    /// <summary>Opens the search box, starting from what the player is looking at when there is something.</summary>
    public void Open(Player player, Target context)
    {
        _catalog = Catalog.Build(player, Scene.Read(player), LastUsed.Current.Entries);
        _context = context;
        _contextEntry = context == null ? null : new Entry(string.Empty, context.Label, context.Kind, context.Count);
        _text = context == null ? string.Empty : $"{context.Count.ToString(CultureInfo.InvariantCulture)} {context.Shown}";
        _rankedFor = null;
        _selected = 0;
        _first = 0;
        _countFor = null;
        _details.Clear();
        _partsFor = null;
        _queryFor = null;
        _names = _catalog.Entries.Select(e => e.Shown).ToList();
        if (context != null)
        {
            _names.Add(context.Shown);
        }
        _focusFrames = 3;
        _cursorToEnd = true;
        IsOpen = true;
        Sound.Play(Sound.Cue.Open);
        Rank();
    }

    public void Close()
    {
        IsOpen = false;
        _closedFrame = Time.frameCount;
        if (ZInput.instance != null)
        {
            ZInput.ResetButtonStatus("Chat");
            ZInput.ResetButtonStatus("Inventory");
        }

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
        var query = Parsed;
        var ranked = Search.Rank(query.Text, _catalog.Entries, LastUsed.Current.Entries).ToList();
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

    /// <summary>The key the entry is remembered by; what the player was looking at has its target's.</summary>
    private string KeyOf(Entry entry) => entry == _contextEntry ? _context.Key : entry.Key;

    private bool IsLastUsed(Entry entry) => LastUsed.Current.Entries.Any(u => u.Is(entry.Kind, KeyOf(entry)));

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

    private int CountFor(Entry entry) => Parsed.Count ?? Mathf.RoundToInt(_count);

    /// <summary>The search text parsed once per change rather than on every drawing pass.</summary>
    private Query Parsed
    {
        get
        {
            if (!string.Equals(_queryFor, _text, StringComparison.Ordinal))
            {
                _queryFor = _text;
                _query = Query.Parse(_text, _names);
            }

            return _query;
        }
    }

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
                Sound.Play(Sound.Cue.Close);
                break;
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                Enter(player, e.shift);
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
        _text = Query.Complete(_text, name, _names);
        _cursorToEnd = true;
    }

    /// <summary>
    /// With a number in the text, Enter takes that many; without one it finds where the chosen
    /// entry is, and Enter on what it found last takes one (see <see cref="Recent.Enter"/>).
    /// Shift+Enter always finds. The new items row always takes, one of each.
    /// </summary>
    private void Enter(Player player, bool shift)
    {
        var chosen = Chosen;
        if (chosen == null)
        {
            return;
        }

        switch (LastUsed.Current.Enter(chosen.Kind, KeyOf(chosen), Parsed.Count != null, shift))
        {
            case EnterDoes.Take:
                Take(player);
                return;
            case EnterDoes.TakeOne:
                TakeOne(player, chosen);
                return;
        }

        var target = TargetOf(chosen);
        if (target == null || target.Cost.Count == 0)
        {
            return;
        }

        Close();
        Finder.Find(player, target);
        LastUsed.Found(chosen.Kind, KeyOf(chosen));
    }

    /// <summary>
    /// One of what was found: one item, or for a piece or recipe what a press of the key takes for
    /// it, by the PiecePress setting for a piece.
    /// </summary>
    private void TakeOne(Player player, Entry chosen)
    {
        var target = TargetOf(chosen);
        if (target == null)
        {
            return;
        }

        target = chosen.Kind == EntryKind.Item ? target.WithCount(1)
            : chosen == _contextEntry ? _context
            : Context.OneMore(target, player);
        Close();
        _grabber.Grab(player, target, Time.time);
        LastUsed.Took(chosen.Kind, KeyOf(chosen));
    }

    private void Take(Player player)
    {
        var chosen = Chosen;
        var target = chosen == null ? null : TargetOf(chosen);
        if (target == null)
        {
            return;
        }

        if (target.Kind == EntryKind.New)
        {
            // Read again from the chests as they are now; the row was made when the box opened.
            Close();
            _grabber.GrabNew(player, Time.time);
            return;
        }

        target = target.WithCount(CountFor(chosen));
        Close();
        _grabber.Grab(player, target, Time.time);
        LastUsed.Took(chosen.Kind, KeyOf(chosen));
    }

    /// <summary>
    /// The box in the game's own art: its wood panel, a Norse heading, a text field, item icons
    /// in the rows, the chosen row in the game's orange, a slider and a button from its UI. Where
    /// the art cannot be had, plain fills stand in.
    /// </summary>
    private void Draw(Player player)
    {
        float scale = Mathf.Max(0.5f, Screen.height / 1080f) * Settings.Scale.Value;
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float x = (Screen.width / scale - Width) / 2f;
        int rows = Math.Min(VisibleRows, Math.Max(1, _results.Count));
        var panel = new Rect(x, Top, Width, rows * RowHeight + 206f);
        if (!UiArt.Sliced(panel, "woodpanel_settings", Color.white))
        {
            GUI.Box(panel, GUIContent.none, Skin.Box);
        }

        float inner = x + Pad;
        float width = Width - 2f * Pad;
        GUI.DrawTexture(new Rect(inner - 12f, Top + 14f, width + 24f, panel.height - 14f - (Pad - 12f)), Skin.InnerFill);
        DrawField(new Rect(inner, Top + 20f, width, 38f));
        float y = Top + 66f;
        DrawRows(new Rect(inner, y, width, rows * RowHeight), player);
        if (!IsOpen)
        {
            GUI.matrix = matrix;
            return;
        }

        y += rows * RowHeight + 8f;
        GUI.Label(new Rect(inner + 4f, y, width - 8f, 26f), ChosenPartsCached(), Skin.DetailLeft);
        y += 32f;
        DrawCount(new Rect(inner, y, width, 36f), player);
        y += 44f;
        GUI.Label(new Rect(inner + 4f, y, width - 8f, 24f), Hint(), Skin.Hint);

        GUI.matrix = matrix;
    }

    /// <summary>The key hints for the chosen entry: what Enter does with it now.</summary>
    private string Hint()
    {
        var chosen = Chosen;
        if (chosen?.Kind == EntryKind.New)
        {
            return "Enter takes one of each · ↑↓ choose · Esc close";
        }

        return chosen != null && LastUsed.Current.Enter(chosen.Kind, KeyOf(chosen), Parsed.Count != null, shift: false) == EnterDoes.TakeOne
            ? "Enter takes one · Shift+Enter finds again · ↑↓ choose · Esc close"
            : "Enter find · a number, then Enter, takes · Tab complete · ↑↓ choose · Esc close";
    }

    private void DrawField(Rect area)
    {
        if (!UiArt.Sliced(area, "text_field", Color.white))
        {
            GUI.DrawTexture(area, Skin.SunkenFill);
        }

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
            GUI.Label(new Rect(area.x + 12f, area.y, area.width, area.height), Search.Prompt(Settings.ShowItems.Value, Settings.ShowPieces.Value, Settings.ShowRecipes.Value), Skin.Hint);
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
            string empty = Settings.ShowItems.Value ? "The chests nearby are empty" : "Type a name to search";
            GUI.Label(area, Parsed.Text.Length == 0 ? empty : "Nothing found", Skin.Hint);
            return;
        }

        for (int row = 0; row < VisibleRows && _first + row < _results.Count; row++)
        {
            int index = _first + row;
            var entry = _results[index];
            var rect = new Rect(area.x, area.y + row * RowHeight, area.width, RowHeight);
            bool chosen = index == _selected;
            if (chosen)
            {
                if (!UiArt.Sliced(rect, "button_highlight", new Color(1f, 1f, 1f, 0.55f)))
                {
                    GUI.DrawTexture(rect, Skin.SelectedFill);
                }
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
                    Enter(player, shift: false);
                }

                e.Use();
                if (!IsOpen)
                {
                    return;
                }
            }

            var icon = entry == _contextEntry ? _context.Icon : _catalog.IconOf(entry);
            UiArt.Icon(new Rect(rect.x + 6f, rect.y + 3f, RowHeight - 6f, RowHeight - 6f), icon);
            float left = rect.x + RowHeight + 6f;
            if (IsLastUsed(entry))
            {
                var color = GUI.color;
                GUI.color = UiArt.Beige;
                UiArt.Icon(new Rect(left, rect.y + (RowHeight - LastUsedMark) / 2f, LastUsedMark, LastUsedMark), UiArt.Get("refresh_icon"));
                GUI.color = color;
                left += LastUsedMark + 6f;
            }

            GUI.Label(new Rect(left, rect.y, rect.width * 0.6f, rect.height), entry.Shown, chosen ? Skin.Chosen : Skin.Name);
            GUI.Label(new Rect(rect.x + rect.width * 0.5f, rect.y, rect.width * 0.5f - 8f, rect.height), Detail(entry), Skin.Detail);
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

        if (chosen.Kind == EntryKind.New)
        {
            if (Button(new Rect(area.xMax - 150f, area.y - 2f, 150f, area.height + 4f), "Take"))
            {
                Take(player);
            }

            return;
        }

        int? typed = Parsed.Count;
        GUI.Label(new Rect(area.x + 4f, area.y, 70f, area.height), "Count", Skin.Name);
        var track = new Rect(area.x + 80f, area.y + 10f, area.width - 320f, area.height - 20f);
        _count = Slider(track, new Rect(track.x - 12f, area.y, track.width + 24f, area.height), typed ?? _count, 1f, MaxCount(chosen), typed == null);
        GUI.Label(new Rect(track.xMax + 12f, area.y, 80f, area.height), CountFor(chosen).ToString("N0", CultureInfo.InvariantCulture), Skin.Name);
        if (Button(new Rect(area.xMax - 150f, area.y - 2f, 150f, area.height + 4f), "Take"))
        {
            Take(player);
        }
    }

    /// <summary>
    /// A slider in the game's look: a sunken track, an orange fill and a small button for the
    /// handle. A press anywhere in its grip area takes hold; while the left button stays down the
    /// handle follows the pointer on every pass, read from the button itself rather than from drag
    /// events; the wheel over it steps by one.
    /// </summary>
    private float Slider(Rect track, Rect grip, float value, float min, float max, bool enabled)
    {
        var e = Event.current;
        float span = Mathf.Max(0.0001f, max - min);
        if (!enabled || !Input.GetMouseButton(0))
        {
            _dragging = false;
        }

        if (enabled)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && grip.Contains(e.mousePosition))
            {
                _dragging = true;
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && grip.Contains(e.mousePosition))
            {
                value = Mathf.Clamp(Mathf.Round(value) + (e.delta.y > 0 ? -1f : 1f), min, max);
                e.Use();
            }

            if (_dragging)
            {
                value = min + Mathf.Clamp01((e.mousePosition.x - track.x) / track.width) * span;
            }
        }

        float share = Mathf.Clamp01((value - min) / span);
        if (!UiArt.Sliced(track, "text_field", Color.white))
        {
            GUI.DrawTexture(track, Skin.SunkenFill);
        }

        var fill = new Rect(track.x + 2f, track.y + 2f, Mathf.Max(0f, (track.width - 4f) * share), track.height - 4f);
        var color = GUI.color;
        GUI.color = enabled ? UiArt.Orange : new Color(0.6f, 0.6f, 0.6f, 1f);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = color;

        var handle = new Rect(track.x + track.width * share - 9f, track.y - 6f, 18f, track.height + 12f);
        if (!UiArt.Sliced(handle, "button_small", Color.white))
        {
            GUI.DrawTexture(handle, Skin.SelectedFill);
        }

        return value;
    }

    /// <summary>A button in the game's look; true when clicked.</summary>
    private static bool Button(Rect area, string label)
    {
        bool over = area.Contains(Event.current.mousePosition);
        if (!UiArt.Sliced(area, over ? "button_highlight" : "button", Color.white))
        {
            GUI.DrawTexture(area, over ? Skin.SelectedFill : Skin.SunkenFill);
        }

        GUI.Label(area, label, Skin.Button);
        if (Event.current.type == EventType.MouseDown && over)
        {
            Event.current.Use();
            return true;
        }

        return false;
    }
    /// <summary>The chosen entry's parts line, made again only when the entry or its count changed.</summary>
    private string ChosenPartsCached()
    {
        var chosen = Chosen;
        int count = chosen == null ? 0 : CountFor(chosen);
        if (chosen != _partsFor || count != _partsCount)
        {
            _partsFor = chosen;
            _partsCount = count;
            _parts = PartsOf(chosen);
        }

        return _parts;
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

        if (target.Kind == EntryKind.New)
        {
            return Readout.NewNames(target.Cost.Select(part => Rummage.Game.Text.Shown(part.Name)).ToList());
        }

        if (target.Kind == EntryKind.Item)
        {
            string name = target.Cost[0].Name;
            return Readout.Item(_catalog.Carried.TryGetValue(name, out int c) ? c : 0, _catalog.InChests.TryGetValue(name, out int t) ? t : 0);
        }

        return Readout.Parts(Cost.Sum(target.Cost), CountFor(entry), _catalog.Carried, _catalog.InChests, Rummage.Game.Text.Shown,
            Presses.CountsCarried(target.Kind, Settings.PiecePress.Value));
    }

    /// <summary>A row's text on its right, with "found" or "taken" in front for what the search box last used.</summary>
    private string Detail(Entry entry)
    {
        if (!_details.TryGetValue(entry, out string text))
        {
            string tag = LastUsed.Current.Tag(entry.Kind, KeyOf(entry));
            text = tag == null ? DetailText(entry) : $"{tag} · {DetailText(entry)}";
            _details[entry] = text;
        }

        return text;
    }

    private string DetailText(Entry entry)
    {
        string many = entry.InChests.ToString("N0", CultureInfo.InvariantCulture);
        if (entry == _contextEntry)
        {
            return entry.Kind switch
            {
                EntryKind.Recipe => "selected recipe",
                EntryKind.Piece => "selected piece",
                _ => _context.FillsStack ? "your stack, to a full stack" : "your stack",
            };
        }

        return entry.Kind switch
        {
            EntryKind.Item => Readout.Stocked(entry.InChests, entry.Held),
            EntryKind.New => Readout.NewItems(entry.InChests, entry.Held),
            EntryKind.Piece => entry.InChests > 0 ? $"piece · enough for {many}" : "piece · not enough",
            _ => entry.InChests > 0 ? $"recipe · enough for {many}" : "recipe · not enough",
        };
    }
}

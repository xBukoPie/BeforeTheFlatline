using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class InteractionPromptManager : Node
{
    public static InteractionPromptManager Instance { get; private set; }

    private sealed record PromptEntry(ulong OwnerId, string Text, int Priority, long Order);

    private static readonly string[] InputActions = { "interact", "interact_alt", "interact_alt2" };
    private static readonly string[] InputLabels = { "E", "F", "G" };
    private static readonly Key[] InputKeys = { Key.E, Key.F, Key.G };

    private readonly Dictionary<ulong, PromptEntry> _prompts = new();
    private readonly HashSet<ulong> _suppressedOwners = new();
    private CanvasLayer _layer;
    private Control _root;
    private VBoxContainer _stack;
    private long _order;
    private string _noticeText = string.Empty;
    private float _noticeRemaining;
    private bool _lastSuppressedState;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        EnsureInputActions();
        BuildUi();
        RefreshPrompts();
    }

    public override void _Process(double delta)
    {
        bool suppressed = ShouldSuppressPrompts();
        if (suppressed != _lastSuppressedState)
        {
            _lastSuppressedState = suppressed;
            if (_root != null && GodotObject.IsInstanceValid(_root))
                _root.Visible = !suppressed;
        }

        if (_noticeRemaining > 0f)
        {
            _noticeRemaining = Mathf.Max(0f, _noticeRemaining - (float)delta);
            if (_noticeRemaining <= 0f)
            {
                _noticeText = string.Empty;
                RefreshPrompts();
            }
        }
    }

    public override void _ExitTree()
    {
        _prompts.Clear();
        _suppressedOwners.Clear();
        if (Instance == this)
            Instance = null;
    }

    public void ShowPrompt(Node owner, string text, int priority = 0)
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner) || string.IsNullOrWhiteSpace(text))
            return;

        ulong id = owner.GetInstanceId();
        string trimmed = text.Trim();
        if (_prompts.TryGetValue(id, out PromptEntry existing) &&
            existing.Priority == priority &&
            string.Equals(existing.Text, trimmed, StringComparison.Ordinal))
            return;

        _prompts[id] = new PromptEntry(id, trimmed, priority, ++_order);
        RefreshPrompts();
    }

    public void HidePrompt(Node owner)
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner))
            return;

        if (_prompts.Remove(owner.GetInstanceId()))
            RefreshPrompts();
    }

    public void ShowNotice(Node owner, string message, float duration = 2.5f)
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner) || string.IsNullOrWhiteSpace(message))
            return;

        _noticeText = message.Trim();
        _noticeRemaining = Mathf.Max(0.5f, duration);
        RefreshPrompts();
    }

    public void SetPromptsSuppressed(Node owner, bool suppressed)
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner))
            return;

        ulong id = owner.GetInstanceId();
        bool changed = suppressed ? _suppressedOwners.Add(id) : _suppressedOwners.Remove(id);
        if (changed)
            RefreshPrompts();
    }

    public bool IsInputFor(Node owner, InputEvent @event)
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner) || @event == null)
            return false;

        string action = GetAssignedAction(owner);
        return !string.IsNullOrWhiteSpace(action) && @event.IsActionPressed(action);
    }

    public string GetAssignedAction(Node owner)
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner))
            return string.Empty;

        int slot = GetPromptSlot(owner.GetInstanceId());
        return slot >= 0 && slot < InputActions.Length
            ? InputActions[slot]
            : string.Empty;
    }

    public string GetAssignedKeyLabel(Node owner)
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner))
            return string.Empty;

        int slot = GetPromptSlot(owner.GetInstanceId());
        return slot >= 0 && slot < InputLabels.Length
            ? InputLabels[slot]
            : string.Empty;
    }

    // Kept for scripts that only need to check which prompt currently has E.
    public bool IsPrimary(Node owner) =>
        owner != null && GodotObject.IsInstanceValid(owner) &&
        GetPromptSlot(owner.GetInstanceId()) == 0;

    public bool HasPrompt(Node owner) =>
        owner != null && GodotObject.IsInstanceValid(owner) &&
        _prompts.ContainsKey(owner.GetInstanceId());

    private int GetPromptSlot(ulong ownerId)
    {
        int slot = 0;
        foreach (PromptEntry entry in GetOrderedPrompts())
        {
            if (entry.OwnerId == ownerId)
                return slot;
            slot++;
        }

        return -1;
    }

    private IEnumerable<PromptEntry> GetOrderedPrompts() =>
        _prompts.Values
            .OrderByDescending(entry => entry.Priority)
            .ThenByDescending(entry => entry.Order);

    private bool ShouldSuppressPrompts()
    {
        if (_suppressedOwners.Count > 0 || GetTree().Paused ||
            SceneTransition.Instance?.IsBusy == true)
            return true;

        Node currentScene = GetTree().CurrentScene;
        return currentScene != null &&
            !string.Equals(currentScene.SceneFilePath, SaveManager.WorldScenePath, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureInputActions()
    {
        for (int i = 0; i < InputActions.Length; i++)
        {
            string action = InputActions[i];
            if (!InputMap.HasAction(action))
                InputMap.AddAction(action);

            Key requiredKey = InputKeys[i];
            bool alreadyBound = InputMap.ActionGetEvents(action)
                .OfType<InputEventKey>()
                .Any(input => input.PhysicalKeycode == requiredKey);
            if (!alreadyBound)
                InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = requiredKey });
        }
    }

    private void BuildUi()
    {
        _layer = new CanvasLayer
        {
            Name = "InteractionPromptLayer",
            Layer = 106
        };
        AddChild(_layer);

        _root = new Control
        {
            Name = "InteractionPromptRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);

        _stack = new VBoxContainer
        {
            Name = "PromptStack",
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _stack.AnchorLeft = 0.5f;
        _stack.AnchorRight = 0.5f;
        _stack.AnchorTop = 1f;
        _stack.AnchorBottom = 1f;
        _stack.OffsetLeft = -410f;
        _stack.OffsetRight = 410f;
        _stack.OffsetTop = -294f;
        _stack.OffsetBottom = -54f;
        _stack.AddThemeConstantOverride("separation", 6);
        _root.AddChild(_stack);
    }

    private void RefreshPrompts()
    {
        if (_stack == null || !GodotObject.IsInstanceValid(_stack))
            return;

        bool suppressed = ShouldSuppressPrompts();
        _lastSuppressedState = suppressed;
        _root.Visible = !suppressed;
        foreach (Node child in _stack.GetChildren())
        {
            _stack.RemoveChild(child);
            child.QueueFree();
        }

        if (suppressed)
            return;

        if (!string.IsNullOrWhiteSpace(_noticeText))
            AddNoticeRow(_noticeText);

        List<PromptEntry> entries = GetOrderedPrompts().Take(InputActions.Length).ToList();
        for (int i = 0; i < entries.Count; i++)
        {
            string text = RemoveKeyHint(entries[i].Text);
            AddPromptRow(i, text);
        }

        if (_prompts.Count > entries.Count)
            AddPromptRow(-1, $"+ {_prompts.Count - entries.Count} more nearby");
    }

    private void AddNoticeRow(string text)
    {
        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(680, 40)
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.035f, 0.055f, 0.90f),
            BorderColor = new Color(0.74f, 0.66f, 0.87f, 0.65f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 7,
            ContentMarginBottom = 7
        });
        _stack.AddChild(panel);

        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(620, 24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", Colors.White);
        panel.AddChild(label);
    }

    private void AddPromptRow(int slot, string text)
    {
        bool primary = slot == 0;
        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(680, primary ? 44 : 36)
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = primary ? new Color(0.035f, 0.035f, 0.055f, 0.88f) : new Color(0.035f, 0.035f, 0.055f, 0.74f),
            BorderColor = primary ? new Color(0.78f, 0.70f, 0.94f, 0.8f) : new Color(0.60f, 0.56f, 0.68f, 0.48f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 5,
            ContentMarginBottom = 5
        });
        _stack.AddChild(panel);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        panel.AddChild(row);

        if (slot >= 0 && slot < InputLabels.Length)
        {
            var key = new Label
            {
                Text = InputLabels[slot],
                CustomMinimumSize = new Vector2(24, 24),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            key.AddThemeFontSizeOverride("font_size", 15);
            key.AddThemeColorOverride("font_color", new Color("f6eefe"));
            key.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                BgColor = primary ? new Color("66558a") : new Color("4b4560"),
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            });
            row.AddChild(key);
        }
        else
        {
            var nearby = new Label
            {
                Text = "NEARBY",
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            nearby.AddThemeFontSizeOverride("font_size", 10);
            nearby.AddThemeColorOverride("font_color", new Color("c4b7d6"));
            row.AddChild(nearby);
        }

        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(560, primary ? 24 : 20),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", primary ? 17 : 14);
        label.AddThemeColorOverride("font_color", primary ? Colors.White : new Color("e1dbe9"));
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 2);
        row.AddChild(label);
    }

    private static string RemoveKeyHint(string text)
    {
        string value = (text ?? string.Empty).Trim();
        if (value.Length >= 3 && value[0] == '[' && value[2] == ']' &&
            (value[1] == 'E' || value[1] == 'e' ||
             value[1] == 'F' || value[1] == 'f' ||
             value[1] == 'G' || value[1] == 'g'))
            value = value.Substring(3).TrimStart(' ', ':', '-', '—');
        return value;
    }
}

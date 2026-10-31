using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

public partial class SchoolFriendsSpawner : Node2D
{
    private const float FriendSpriteScale = 0.345f;
    private const int PlayerCollisionMask = 2;
    private const float StoryHintChance = 0.005f; // 0.5% on the first conversation with each friend per day.

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<FriendActor> _friends = new();
    private readonly List<(string Speaker, string Text)> _dialogueLines = new();

    private CharacterBody2D _player;
    private ProcessModeEnum _playerPreviousProcessMode;
    private bool _playerFrozen;
    private bool _conversationOpen;
    private int _dialogueIndex;
    private FriendActor _currentFriend;

    private CanvasLayer _uiLayer;
    private Control _uiRoot;
    private Label _interactionPrompt;
    private PanelContainer _dialoguePanel;
    private Label _speakerLabel;
    private Label _dialogueText;
    private Label _advanceHint;

    private double _idleFrameTimer;

    private readonly record struct FriendDefinition(string Name, string TexturePath);
    private sealed record InteractionTopic(string Opening, string PlayerReply, string Closing);

    private sealed class FriendActor
    {
        public string Name { get; init; } = "";
        public Node2D Root { get; init; }
        public Sprite2D Sprite { get; init; }
        public Area2D Area { get; init; }
        public bool PlayerNearby { get; set; }
    }

    private static readonly FriendDefinition[] FriendDefinitions =
    {
        new("Mich", "res://Assets/Characters/pixel/michIdle.png"),
        new("Jepoy", "res://Assets/Characters/pixel/jepoyIdle.png"),
        new("Cess", "res://Assets/Characters/pixel/cessIdle.png")
    };

    // These are clear ground / path positions in front of the school building.
    // None sit inside the building, fence, tree trunks, flag pole, or basketball hoops.
    private static readonly Vector2[] SafeSpawnPositions =
    {
        new(-190f, -25f),
        new(-120f, 25f),
        new(-65f, 95f),
        new(-5f, 25f),
        new(75f, 95f),
        new(145f, 25f),
        new(-175f, 105f),
        new(5f, 145f),
        new(175f, 130f)
    };

    private static readonly Dictionary<string, InteractionTopic[]> TopicsByFriend =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Mich"] = new[]
            {
                new InteractionTopic(
                    "I swear the canteen's siomai got smaller this week.",
                    "Maybe you're just getting hungrier.",
                    "No, I checked. One of them barely covered the spoon. It's a serious issue."),
                new InteractionTopic(
                    "Did you finish the worksheet? I stared at the last question for ten minutes.",
                    "I left that one for later too.",
                    "Good. At least we're both struggling in the same direction."),
                new InteractionTopic(
                    "The breeze here is nice. I almost forgot we were supposed to be doing something.",
                    "A short break won't hurt.",
                    "Exactly. Let's pretend this is part of our study routine."),
                new InteractionTopic(
                    "I found a pen that writes only when you hold it at a very specific angle.",
                    "That sounds more difficult than the actual assignment.",
                    "I'm keeping it anyway. Maybe it's a collector's item."),
                new InteractionTopic(
                    "Do you think the teachers notice when we pretend to understand the lesson?",
                    "They probably notice more than we think.",
                    "Then I'll have to improve my acting. Anyway, see you around.")
            },
            ["Jepoy"] = new[]
            {
                new InteractionTopic(
                    "We almost had a proper game during break. Almost.",
                    "You spent half of it looking for the ball.",
                    "That's called preparation. Next time, I'm making the winning shot."),
                new InteractionTopic(
                    "My legs are still sore from yesterday's practice.",
                    "Maybe you should take it easy for a while.",
                    "I would, but someone has to keep our team from getting rusty."),
                new InteractionTopic(
                    "The classroom fan makes a weird clicking sound. I think it counts as a fourth classmate now.",
                    "At least it never asks to borrow your notes.",
                    "True. It contributes nothing, but it's always there."),
                new InteractionTopic(
                    "I tried studying with music on. Somehow I ended up listening to the same song for an hour.",
                    "That's still an hour spent doing something.",
                    "I'll tell myself that when the quiz comes."),
                new InteractionTopic(
                    "Have you noticed how quiet the school gets between classes?",
                    "Only until someone remembers they have somewhere to be.",
                    "Yeah. It's kind of nice while it lasts.")
            },
            ["Cess"] = new[]
            {
                new InteractionTopic(
                    "I've been trying to sketch the school gate from memory. It keeps looking crooked.",
                    "Maybe the gate really is crooked.",
                    "That's what I'm telling myself. It's artistic perspective."),
                new InteractionTopic(
                    "The library has a book with three different due dates written inside.",
                    "Maybe nobody wanted to return it.",
                    "Or maybe the book has been on a very long vacation."),
                new InteractionTopic(
                    "I brought extra paper today. Somehow I still managed to run out.",
                    "You always find something to write on.",
                    "That's because I keep forgetting how much I write."),
                new InteractionTopic(
                    "The clouds looked like little islands earlier. I wanted to draw them, but the bell rang.",
                    "You can draw them from memory later.",
                    "I know. I just hope I remember the shapes."),
                new InteractionTopic(
                    "I think I need a new playlist for studying. Everything I have is starting to sound the same.",
                    "Try something without lyrics.",
                    "Maybe. Though silence can be distracting too.")
            }
        };

    private static readonly string[][] RareStoryHints =
    {
        new[]
        {
            "Did you know? I saw a page in an old school record with your name on it.",
            "The date was from before you enrolled here. I thought it was a typo, but the page after it had been torn out.",
            "Anyway... maybe it was a different person. Forget I mentioned it."
        },
        new[]
        {
            "This is probably nothing, but someone asked me whether you still remembered the old classroom.",
            "I asked who they were. They just said you would recognise the place if you saw it again.",
            "Strange thing to ask, right? Anyway, how was your day?"
        },
        new[]
        {
            "I found an old class photo in one of the folders. There was someone standing beside you.",
            "The photo was dated years before you started here. When I looked again, that part of the picture was missing.",
            "Maybe the paper was damaged. Still... I thought you should know."
        },
        new[]
        {
            "Did you know? A teacher once asked me if you had returned something that didn't belong to you.",
            "I asked what it was, but she said she couldn't remember. She looked like she was waiting for me to say something.",
            "Maybe I misunderstood. Let's talk about something else."
        }
    };

    public override void _Ready()
    {
        _rng.Randomize();

        var roomManager = GetTree().GetFirstNodeInGroup("room_manager");
        _player = roomManager?.GetNodeOrNull<CharacterBody2D>("Player");

        BuildInteractionUi();
        SpawnRandomFriends();
    }

    public override void _Process(double delta)
    {
        _idleFrameTimer += delta;
        if (_idleFrameTimer >= 0.72)
        {
            _idleFrameTimer = 0;
            foreach (FriendActor friend in _friends)
            {
                if (GodotObject.IsInstanceValid(friend.Sprite))
                    friend.Sprite.Frame = (friend.Sprite.Frame + 1) % 2;
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_conversationOpen && @event.IsActionPressed("interact"))
        {
            FriendActor friend = GetNearestFriend();
            if (friend == null)
                return;

            GetViewport().SetInputAsHandled();
            BeginConversation(friend);
            return;
        }

        if (_conversationOpen && @event.IsActionPressed("interact"))
        {
            GetViewport().SetInputAsHandled();
            AdvanceConversation();
        }
    }

    public override void _ExitTree()
    {
        RestorePlayerControl();
    }

    private void SpawnRandomFriends()
    {
        var definitions = (FriendDefinition[])FriendDefinitions.Clone();
        var positions = (Vector2[])SafeSpawnPositions.Clone();
        Shuffle(definitions);
        Shuffle(positions);

        // Uniformly choose zero, one, two, or all three friends for this school visit.
        int count = _rng.RandiRange(0, definitions.Length);
        for (int i = 0; i < count; i++)
            CreateFriend(definitions[i], positions[i]);

        GD.Print($"[SchoolFriends] Spawned {_friends.Count} friend(s) near the classroom.");
    }

    private void CreateFriend(FriendDefinition definition, Vector2 position)
    {
        Texture2D texture = GD.Load<Texture2D>(definition.TexturePath);
        if (texture == null)
        {
            GD.PushWarning($"SchoolFriends: character texture not found: {definition.TexturePath}");
            return;
        }

        var actorRoot = new Node2D
        {
            Name = $"Friend_{definition.Name}",
            Position = position
        };
        AddChild(actorRoot);

        var sprite = new Sprite2D
        {
            Name = $"{definition.Name}Sprite",
            Texture = texture,
            Hframes = 2,
            Vframes = 1,
            Frame = 0,
            Scale = new Vector2(FriendSpriteScale, FriendSpriteScale),
            Position = new Vector2(0, -16),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        actorRoot.AddChild(sprite);

        var area = new Area2D
        {
            Name = "TalkArea",
            CollisionLayer = 0,
            CollisionMask = PlayerCollisionMask,
            Monitoring = true,
            Monitorable = false,
            InputPickable = false,
            Position = new Vector2(0, -8)
        };
        actorRoot.AddChild(area);
        area.AddChild(new CollisionShape2D
        {
            Name = "TalkRange",
            Shape = new CircleShape2D { Radius = 30f }
        });

        var friend = new FriendActor
        {
            Name = definition.Name,
            Root = actorRoot,
            Sprite = sprite,
            Area = area
        };

        area.BodyEntered += body => OnFriendBodyEntered(friend, body);
        area.BodyExited += body => OnFriendBodyExited(friend, body);
        _friends.Add(friend);
    }

    private void OnFriendBodyEntered(FriendActor friend, Node2D body)
    {
        if (!IsPlayerBody(body))
            return;

        friend.PlayerNearby = true;
        RefreshInteractionPrompt();
    }

    private void OnFriendBodyExited(FriendActor friend, Node2D body)
    {
        if (!IsPlayerBody(body))
            return;

        friend.PlayerNearby = false;
        RefreshInteractionPrompt();
    }

    private static bool IsPlayerBody(Node2D body) =>
        body is CharacterBody2D && string.Equals(body.Name, "Player", StringComparison.Ordinal);

    private FriendActor GetNearestFriend()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return null;

        FriendActor nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (FriendActor friend in _friends)
        {
            if (!friend.PlayerNearby || !GodotObject.IsInstanceValid(friend.Root))
                continue;

            float distance = _player.GlobalPosition.DistanceSquaredTo(friend.Root.GlobalPosition);
            if (distance >= nearestDistance)
                continue;

            nearest = friend;
            nearestDistance = distance;
        }

        return nearest;
    }

    private void RefreshInteractionPrompt()
    {
        if (_interactionPrompt == null || _conversationOpen)
            return;

        FriendActor nearest = GetNearestFriend();
        _interactionPrompt.Visible = nearest != null;
        if (nearest != null)
            _interactionPrompt.Text = $"[E] Talk to {nearest.Name}";
    }

    private void BeginConversation(FriendActor friend)
    {
        string playerName = SettingsManager.Instance?.PlayerName?.Trim();
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "You";

        bool firstConversationToday = SchoolFriendInteractionHistory.TryMarkTalked(friend.Name);
        bool recoveredSanity = false;

        if (firstConversationToday && SanityManager.Instance != null)
        {
            int previousSanity = SanityManager.Instance.CurrentSanity;
            SanityManager.Instance.Recover(2);
            recoveredSanity = SanityManager.Instance.CurrentSanity > previousSanity;

            if (recoveredSanity && SaveManager.Instance?.CanSaveCurrentGame == true)
                SaveManager.Instance.SaveAutoSave();
        }

        _dialogueLines.Clear();

        if (firstConversationToday && _rng.Randf() < StoryHintChance)
        {
            string[] hint = RareStoryHints[_rng.RandiRange(0, RareStoryHints.Length - 1)];
            _dialogueLines.Add((friend.Name, $"{playerName}, can I tell you something?"));
            foreach (string line in hint)
                _dialogueLines.Add((friend.Name, line));
        }
        else
        {
            InteractionTopic[] topics = TopicsByFriend[friend.Name];
            InteractionTopic topic = topics[_rng.RandiRange(0, topics.Length - 1)];
            _dialogueLines.Add((friend.Name, topic.Opening.Replace("{player}", playerName)));
            _dialogueLines.Add((playerName, topic.PlayerReply));
            _dialogueLines.Add((friend.Name, topic.Closing));
        }

        if (recoveredSanity)
            _dialogueLines.Add((playerName, "That was nice. I feel a little more at ease. (+2 sanity)"));

        _currentFriend = friend;
        _dialogueIndex = 0;
        _conversationOpen = true;
        _interactionPrompt.Visible = false;
        _dialoguePanel.Visible = true;
        FreezePlayerControl();
        DisplayCurrentDialogueLine();
    }

    private void DisplayCurrentDialogueLine()
    {
        if (_dialogueIndex < 0 || _dialogueIndex >= _dialogueLines.Count)
        {
            EndConversation();
            return;
        }

        (string speaker, string text) = _dialogueLines[_dialogueIndex];
        _speakerLabel.Text = speaker;
        _dialogueText.Text = text;
        _advanceHint.Text = _dialogueIndex == _dialogueLines.Count - 1
            ? "[E] Finish conversation"
            : "[E] Continue";
    }

    private void AdvanceConversation()
    {
        _dialogueIndex++;
        if (_dialogueIndex >= _dialogueLines.Count)
        {
            EndConversation();
            return;
        }

        DisplayCurrentDialogueLine();
    }

    private void EndConversation()
    {
        _conversationOpen = false;
        _currentFriend = null;
        _dialoguePanel.Visible = false;
        RestorePlayerControl();
        RefreshInteractionPrompt();
    }

    private void FreezePlayerControl()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player) || _playerFrozen)
            return;

        _playerPreviousProcessMode = _player.ProcessMode;
        _player.Velocity = Vector2.Zero;
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _playerFrozen = true;
    }

    private void RestorePlayerControl()
    {
        if (!_playerFrozen)
            return;

        if (_player != null && GodotObject.IsInstanceValid(_player))
            _player.ProcessMode = _playerPreviousProcessMode;
        _playerFrozen = false;
    }

    private void BuildInteractionUi()
    {
        _uiLayer = new CanvasLayer
        {
            Name = "SchoolFriendsUi",
            Layer = 107
        };
        AddChild(_uiLayer);

        _uiRoot = new Control
        {
            Name = "SchoolFriendsUiRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _uiLayer.AddChild(_uiRoot);

        _interactionPrompt = new Label
        {
            Name = "FriendInteractionPrompt",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(640, 42),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _interactionPrompt.AnchorLeft = 0.5f;
        _interactionPrompt.AnchorRight = 0.5f;
        _interactionPrompt.AnchorTop = 1f;
        _interactionPrompt.AnchorBottom = 1f;
        _interactionPrompt.OffsetLeft = -320;
        _interactionPrompt.OffsetRight = 320;
        _interactionPrompt.OffsetTop = -86;
        _interactionPrompt.OffsetBottom = -44;
        _interactionPrompt.AddThemeFontSizeOverride("font_size", 18);
        _interactionPrompt.AddThemeColorOverride("font_color", Colors.White);
        _interactionPrompt.AddThemeColorOverride("font_outline_color", Colors.Black);
        _interactionPrompt.AddThemeConstantOverride("outline_size", 4);
        _uiRoot.AddChild(_interactionPrompt);

        _dialoguePanel = new PanelContainer
        {
            Name = "FriendDialoguePanel",
            Visible = false,
            CustomMinimumSize = new Vector2(720, 166),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _dialoguePanel.AnchorLeft = 0.5f;
        _dialoguePanel.AnchorRight = 0.5f;
        _dialoguePanel.AnchorTop = 1f;
        _dialoguePanel.AnchorBottom = 1f;
        _dialoguePanel.OffsetLeft = -360;
        _dialoguePanel.OffsetRight = 360;
        _dialoguePanel.OffsetTop = -224;
        _dialoguePanel.OffsetBottom = -46;
        _dialoguePanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("101827ee"),
            BorderColor = new Color("98a6b8"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 12,
            ContentMarginBottom = 12
        });
        _uiRoot.AddChild(_dialoguePanel);

        var dialogueStack = new VBoxContainer();
        dialogueStack.AddThemeConstantOverride("separation", 7);
        _dialoguePanel.AddChild(dialogueStack);

        _speakerLabel = new Label
        {
            Name = "FriendSpeaker",
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _speakerLabel.AddThemeFontSizeOverride("font_size", 18);
        _speakerLabel.AddThemeColorOverride("font_color", new Color("c7d2fe"));
        dialogueStack.AddChild(_speakerLabel);

        _dialogueText = new Label
        {
            Name = "FriendDialogueText",
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 58)
        };
        _dialogueText.AddThemeFontSizeOverride("font_size", 18);
        _dialogueText.AddThemeColorOverride("font_color", Colors.White);
        dialogueStack.AddChild(_dialogueText);

        _advanceHint = new Label
        {
            Name = "FriendDialogueHint",
            Text = "[E] Continue",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _advanceHint.AddThemeFontSizeOverride("font_size", 13);
        _advanceHint.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        dialogueStack.AddChild(_advanceHint);
    }

    private void Shuffle<T>(T[] values)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = _rng.RandiRange(0, i);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}

// Saved separately from sanity so once-per-day friendship rewards cannot be repeated
// by leaving/re-entering the school scene or loading a save made after a conversation.
public static class SchoolFriendInteractionHistory
{
    private static int _interactionDay = -1;
    private static readonly HashSet<string> TalkedToToday = new(StringComparer.OrdinalIgnoreCase);

    private static int CurrentDay => Math.Max(1, DayClock.Instance?.DayNumber ?? 1);

    private static void EnsureCurrentDay()
    {
        int day = CurrentDay;
        if (_interactionDay == day)
            return;

        _interactionDay = day;
        TalkedToToday.Clear();
    }

    public static bool TryMarkTalked(string friendName)
    {
        if (string.IsNullOrWhiteSpace(friendName))
            return false;

        EnsureCurrentDay();
        return TalkedToToday.Add(friendName.Trim());
    }

    public static Godot.Collections.Dictionary CreateSaveData()
    {
        EnsureCurrentDay();
        var friends = new Godot.Collections.Array();
        foreach (string friend in TalkedToToday)
            friends.Add(friend);

        return new Godot.Collections.Dictionary
        {
            ["day"] = _interactionDay,
            ["friends"] = friends
        };
    }

    public static void RestoreFromSaveData(Godot.Collections.Dictionary data)
    {
        _interactionDay = CurrentDay;
        TalkedToToday.Clear();

        if (!data.ContainsKey("day"))
            return;

        Variant dayValue = data["day"];
        if (dayValue.VariantType != Variant.Type.Int ||
            dayValue.AsInt32() != _interactionDay)
            return;

        if (!data.ContainsKey("friends") ||
            data["friends"].VariantType != Variant.Type.Array)
            return;

        foreach (Variant value in data["friends"].AsGodotArray())
        {
            string name = value.AsString();
            if (!string.IsNullOrWhiteSpace(name))
                TalkedToToday.Add(name.Trim());
        }
    }

    public static void Reset()
    {
        _interactionDay = CurrentDay;
        TalkedToToday.Clear();
    }
}

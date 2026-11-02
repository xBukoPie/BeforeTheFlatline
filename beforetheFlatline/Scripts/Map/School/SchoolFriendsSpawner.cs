using Godot;
using System;
using System.Collections.Generic;

namespace SchoolYardGame;

public readonly record struct SchoolFriendSpawn(string Name, Vector2 Position);

public partial class SchoolFriendsSpawner : Node2D
{
    private const float FriendSpriteScale = 0.345f;
    private const int PlayerCollisionMask = 2;
    private const float StoryHintChance = 0.005f; // 0.5% on the first conversation with each friend per day.
    private const string DialogueScenePath = "res://Scenes/UI/VisualNovelScene.tscn";

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<FriendActor> _friends = new();

    private CharacterBody2D _player;
    private ProcessModeEnum _playerPreviousProcessMode;
    private bool _playerFrozen;
    private bool _conversationOpen;

    private CanvasLayer _promptLayer;
    private Control _promptRoot;
    private Label _interactionPrompt;
    private CanvasLayer _dialogueLayer;
    private VisualNovelScene _dialogue;

    private double _idleFrameTimer;

    private readonly record struct FriendDefinition(string Name, string TexturePath, string PortraitPath);

    private sealed record InteractionTopic(
        string Opening,
        string FirstReply,
        string SecondReply,
        string FirstResponse,
        string SecondResponse,
        string Closing);

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
        new("Mich", "res://Assets/Characters/pixel/michIdle.png", "res://Assets/Characters/Mich.png"),
        new("Jepoy", "res://Assets/Characters/pixel/jepoyIdle.png", "res://Assets/Characters/Jepoy.png"),
        new("Cess", "res://Assets/Characters/pixel/cessIdle.png", "res://Assets/Characters/Cess.png")
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
                    "I swear the canteen's siomai got smaller this week, {player}.",
                    "Maybe you're just getting hungrier.",
                    "You should complain to the canteen.",
                    "Maybe I am. Or maybe the siomai is shrinking. Both are possible.",
                    "I did! They told me to come back when I had proof.",
                    "Anyway, next time I'm bringing a ruler. This is becoming a serious investigation."),
                new InteractionTopic(
                    "Did you finish the worksheet, {player}? I stared at the last question for ten minutes.",
                    "I left that one for later too.",
                    "I guessed and hoped for the best.",
                    "Good. At least we're struggling in the same direction.",
                    "Bold strategy. I hope the teacher appreciates the confidence.",
                    "Let's compare answers later. Maybe one of us understood the question."),
                new InteractionTopic(
                    "The breeze here is nice. I almost forgot we were supposed to be doing something.",
                    "A short break won't hurt.",
                    "We should get back before someone notices.",
                    "Exactly. Let's pretend this is part of our study routine.",
                    "You're right. I'll look busy if anyone comes by.",
                    "We should enjoy the quiet while it lasts."),
                new InteractionTopic(
                    "I found a pen that writes only when you hold it at a very specific angle.",
                    "That sounds harder than the actual assignment.",
                    "Maybe it's just running out of ink.",
                    "I know, but I'm keeping it. Maybe it's a collector's item.",
                    "Don't say that. I was hoping it had some mysterious power.",
                    "I'll keep testing it. See you around, {player}."),
                new InteractionTopic(
                    "Do you think the teachers notice when we pretend to understand the lesson?",
                    "They probably notice more than we think.",
                    "I think everyone is just pretending together.",
                    "Then I'll have to improve my acting.",
                    "That would explain why nobody asks questions until the bell rings.",
                    "Anyway, I should get going before I get caught pretending again.")
            },
            ["Jepoy"] = new[]
            {
                new InteractionTopic(
                    "We almost had a proper game during break. Almost.",
                    "You spent half of it looking for the ball.",
                    "You were the one who kicked it away.",
                    "That's called preparation. Next time, I'm making the winning shot.",
                    "It was an accident! The ball went somewhere unexpected.",
                    "We'll settle it next break. Don't disappear before then."),
                new InteractionTopic(
                    "My legs are still sore from yesterday's practice, {player}.",
                    "Maybe you should take it easy for a while.",
                    "That's what happens when you keep showing off.",
                    "I would, but someone has to keep our team from getting rusty.",
                    "I wasn't showing off. I was demonstrating technique.",
                    "I'll probably regret saying this, but practice was worth it."),
                new InteractionTopic(
                    "The classroom fan makes a weird clicking sound. I think it counts as a fourth classmate now.",
                    "At least it never asks to borrow your notes.",
                    "Maybe it's trying to tell us something.",
                    "True. It contributes nothing, but it's always there.",
                    "If it starts talking, I'm transferring schools.",
                    "Let's leave before it decides to join the conversation."),
                new InteractionTopic(
                    "I tried studying with music on. Somehow I listened to the same song for an hour.",
                    "That's still an hour spent doing something.",
                    "Try listening to something without lyrics.",
                    "I'll tell myself that when the quiz comes.",
                    "Maybe. Silence can be distracting too.",
                    "Send me a playlist recommendation sometime. I need a new one."),
                new InteractionTopic(
                    "Have you noticed how quiet the school gets between classes?",
                    "Only until someone remembers they have somewhere to be.",
                    "It's almost like the whole place is holding its breath.",
                    "Yeah. It's kind of nice while it lasts.",
                    "That's a weird way to put it, but I get what you mean.",
                    "Well, enjoy the silence while we can. See you later.")
            },
            ["Cess"] = new[]
            {
                new InteractionTopic(
                    "I've been trying to sketch the school gate from memory. It keeps looking crooked.",
                    "Maybe the gate really is crooked.",
                    "Maybe you need to look at it from another angle.",
                    "That's what I'm telling myself. It's artistic perspective.",
                    "I'll try that. I might get a better view from the steps.",
                    "I'll show you the sketch when it's finished. Don't laugh too much."),
                new InteractionTopic(
                    "The library has a book with three different due dates written inside.",
                    "Maybe nobody wanted to return it.",
                    "Maybe the book has been on a very long vacation.",
                    "Or maybe someone kept borrowing it without checking the date.",
                    "That would explain the notes. The book has a complicated history.",
                    "I should return it before it gets another due date."),
                new InteractionTopic(
                    "I brought extra paper today. Somehow I still managed to run out.",
                    "You always find something to write on.",
                    "You should probably bring a whole notebook next time.",
                    "That's because I keep forgetting how much I write.",
                    "I know. I thought the extra sheets would be enough.",
                    "I'll prepare better tomorrow. Probably."),
                new InteractionTopic(
                    "The clouds looked like little islands earlier. I wanted to draw them, but the bell rang.",
                    "You can draw them from memory later.",
                    "Maybe they'll look different by then.",
                    "I know. I just hope I remember the shapes.",
                    "That's true. Maybe that's why I should sketch them quickly.",
                    "I'll try again tomorrow. The sky isn't going anywhere."),
                new InteractionTopic(
                    "I think I need a new playlist for studying. Everything I have is starting to sound the same.",
                    "Try something without lyrics.",
                    "Maybe you just need a break from music.",
                    "Maybe. Though silence can be distracting too.",
                    "I might do that. Just for one study session.",
                    "Let me know if you find anything good. I'll do the same.")
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
        UpdateFriendDepthSorting();

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

    // The player is a sibling of RoomHolder in World.tscn, not a child of the school room.
    // Built-in Y-sort therefore cannot compare them. Use the feet/ground anchor (Node2D origin)
    // to place friends behind or in front of the player as they cross paths.
    private void UpdateFriendDepthSorting()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player))
            return;

        float playerGroundY = _player.GlobalPosition.Y;
        int playerZ = _player.ZIndex;

        foreach (FriendActor friend in _friends)
        {
            if (friend.Root == null || !GodotObject.IsInstanceValid(friend.Root))
                continue;

            friend.Root.ZAsRelative = false;
            friend.Root.ZIndex = friend.Root.GlobalPosition.Y > playerGroundY
                ? playerZ + 1
                : playerZ - 1;
        }
    }

    private Vector2 GetPlayerSpriteGlobalScale()
    {
        AnimatedSprite2D playerSprite = _player?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        return playerSprite != null && GodotObject.IsInstanceValid(playerSprite)
            ? playerSprite.GlobalScale
            : new Vector2(FriendSpriteScale, FriendSpriteScale);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_conversationOpen || !@event.IsActionPressed("interact"))
            return;

        FriendActor friend = GetNearestFriend();
        if (friend == null)
            return;

        GetViewport().SetInputAsHandled();
        BeginConversation(friend);
    }

    public override void _ExitTree()
    {
        RestorePlayerControl();
    }

    private void SpawnRandomFriends()
    {
        int day = Mathf.Max(1, DayClock.Instance?.DayNumber ?? 1);
        bool assessmentComplete = DayClock.Instance?.SchoolChallengeCompletedToday ?? false;

        IReadOnlyList<SchoolFriendSpawn> spawnPlan =
            SchoolFriendSpawnHistory.GetOrCreateSpawnPlan(day, assessmentComplete, CreateRandomSpawnPlan);

        foreach (SchoolFriendSpawn spawn in spawnPlan)
        {
            if (TryGetDefinition(spawn.Name, out FriendDefinition definition))
                CreateFriend(definition, spawn.Position);
        }

        GD.Print($"[SchoolFriends] Spawned {_friends.Count} friend(s) for day {day} ({(assessmentComplete ? "after assessment" : "before assessment")}).");
    }

    private List<SchoolFriendSpawn> CreateRandomSpawnPlan()
    {
        var definitions = (FriendDefinition[])FriendDefinitions.Clone();
        var positions = (Vector2[])SafeSpawnPositions.Clone();
        Shuffle(definitions);
        Shuffle(positions);

        // Uniformly choose zero, one, two, or all three friends for this school phase.
        int count = _rng.RandiRange(0, definitions.Length - 1); // 0, 1, or 2 friends outside; never all three.
        var plan = new List<SchoolFriendSpawn>(count);
        for (int i = 0; i < count; i++)
            plan.Add(new SchoolFriendSpawn(definitions[i].Name, positions[i]));

        return plan;
    }

    private static bool TryGetDefinition(string name, out FriendDefinition definition)
    {
        foreach (FriendDefinition candidate in FriendDefinitions)
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                definition = candidate;
                return true;
            }
        }

        definition = default;
        return false;
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
            Position = position,
            ZAsRelative = false
        };
        AddChild(actorRoot);

        var sprite = new Sprite2D
        {
            Name = $"{definition.Name}Sprite",
            Texture = texture,
            Hframes = 2,
            Vframes = 1,
            Frame = 0,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        actorRoot.AddChild(sprite);

        // Match the player's effective rendered scale (Player root 1.5 x sprite 0.23).
        // The player sprite is centered at its ground anchor, so keep the same local origin.
        sprite.GlobalScale = GetPlayerSpriteGlobalScale();

        // The name floats just above the character's head and uses an outline for map contrast.
        var nameLabel = new Label
        {
            Name = $"{definition.Name}Name",
            Text = definition.Name,
            Position = new Vector2(-48, -44),
            Size = new Vector2(96, 20),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 5
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 14);
        nameLabel.AddThemeColorOverride("font_color", Colors.White);
        nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        nameLabel.AddThemeConstantOverride("outline_size", 5);
        actorRoot.AddChild(nameLabel);

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
        PackedScene dialogueScene = GD.Load<PackedScene>(DialogueScenePath);
        if (dialogueScene == null)
        {
            GD.PushWarning($"SchoolFriends: dialogue scene not found: {DialogueScenePath}");
            return;
        }

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

        bool showStoryHint = firstConversationToday && _rng.Randf() < StoryHintChance;
        var lines = BuildConversation(friend, playerName, showStoryHint, recoveredSanity);

        _conversationOpen = true;
        _interactionPrompt.Visible = false;
        FreezePlayerControl();

        _dialogueLayer = new CanvasLayer
        {
            Name = "SchoolFriendDialogueLayer",
            Layer = 108
        };
        AddChild(_dialogueLayer);

        _dialogue = dialogueScene.Instantiate<VisualNovelScene>();
        _dialogue.ShowBackground = false;
        _dialogue.ShowPlaceholders = false;
        _dialogue.PlayDemoOnReady = false;
        _dialogue.LoopDemo = false;
        _dialogue.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _dialogueLayer.AddChild(_dialogue);

        _dialogue.SetPortrait(DialogueSide.Left, null);
        _dialogue.SetPortrait(DialogueSide.Right, GD.Load<Texture2D>(GetPortraitPath(friend.Name)));
        _dialogue.DialogueFinished += OnFriendDialogueFinished;
        _dialogue.StartDialogue(lines);
    }

    private static string GetPortraitPath(string friendName)
    {
        foreach (FriendDefinition definition in FriendDefinitions)
        {
            if (string.Equals(definition.Name, friendName, StringComparison.OrdinalIgnoreCase))
                return definition.PortraitPath;
        }

        return string.Empty;
    }

    private static IReadOnlyList<DialogueLine> BuildConversation(
        FriendActor friend, string playerName, bool showStoryHint, bool recoveredSanity)
    {
        var lines = new List<DialogueLine>();

        if (showStoryHint)
        {
            lines.Add(new DialogueLine(
                friend.Name,
                $"{playerName}, can I tell you something?",
                DialogueSide.Right));

            foreach (string hintLine in RareStoryHints[new Random().Next(RareStoryHints.Length)])
                lines.Add(new DialogueLine(friend.Name, hintLine, DialogueSide.Right));
        }
        else
        {
            InteractionTopic[] topics = TopicsByFriend[friend.Name];
            InteractionTopic topic = topics[new Random().Next(topics.Length)];

            lines.Add(new DialogueLine(
                friend.Name,
                topic.Opening.Replace("{player}", playerName),
                DialogueSide.Right,
                Choices: new[]
                {
                    new DialogueChoice(topic.FirstReply, Goto: 1),
                    new DialogueChoice(topic.SecondReply, Goto: 2)
                }));

            lines.Add(new DialogueLine(friend.Name, topic.FirstResponse, DialogueSide.Right, Next: 3));
            lines.Add(new DialogueLine(friend.Name, topic.SecondResponse, DialogueSide.Right, Next: 3));
            lines.Add(new DialogueLine(friend.Name, topic.Closing, DialogueSide.Right));
        }

        if (recoveredSanity)
            lines.Add(new DialogueLine(
                playerName,
                "That was nice. I feel a little more at ease. (+2 sanity)",
                DialogueSide.Left));

        return lines;
    }

    private void OnFriendDialogueFinished()
    {
        if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue))
            _dialogue.DialogueFinished -= OnFriendDialogueFinished;

        if (_dialogueLayer != null && GodotObject.IsInstanceValid(_dialogueLayer))
            _dialogueLayer.QueueFree();

        _dialogue = null;
        _dialogueLayer = null;
        _conversationOpen = false;

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
        _promptLayer = new CanvasLayer
        {
            Name = "SchoolFriendsPromptLayer",
            Layer = 107
        };
        AddChild(_promptLayer);

        _promptRoot = new Control
        {
            Name = "SchoolFriendsPromptRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _promptRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _promptLayer.AddChild(_promptRoot);

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
        _promptRoot.AddChild(_interactionPrompt);
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

// A school's friend roster is rolled once per day before assessment and once after
// assessment completion. Scene changes and save/load operations reuse the same roll.
public static class SchoolFriendSpawnHistory
{
    private static int _planDay = -1;
    private static bool _planAfterAssessment;
    private static bool _hasPlan;
    private static string _classroomGreeterName = string.Empty;
    private static readonly List<SchoolFriendSpawn> SpawnPlan = new();

    public static IReadOnlyList<SchoolFriendSpawn> GetOrCreateSpawnPlan(
        int day,
        bool assessmentComplete,
        Func<List<SchoolFriendSpawn>> createPlan)
    {
        day = Math.Max(1, day);
        if (!_hasPlan || _planDay != day || _planAfterAssessment != assessmentComplete)
        {
            _planDay = day;
            _planAfterAssessment = assessmentComplete;
            _hasPlan = true;
            _classroomGreeterName = string.Empty;
            SpawnPlan.Clear();

            List<SchoolFriendSpawn> newPlan = createPlan?.Invoke();
            if (newPlan != null)
                SpawnPlan.AddRange(newPlan);
        }

        return SpawnPlan;
    }

    // Selects exactly one classroom greeter from the friends not currently spawned outside.
    // The selection remains fixed for this day/assessment phase and is saved with the roster.
    public static string GetOrAssignClassroomGreeter(IReadOnlyList<string> allFriendNames)
    {
        var absentNames = new List<string>();
        if (allFriendNames != null)
        {
            foreach (string candidate in allFriendNames)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                bool isOutside = false;
                foreach (SchoolFriendSpawn spawn in SpawnPlan)
                {
                    if (string.Equals(spawn.Name, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        isOutside = true;
                        break;
                    }
                }

                if (!isOutside)
                    absentNames.Add(candidate);
            }
        }

        if (absentNames.Count == 0)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(_classroomGreeterName))
        {
            foreach (string candidate in absentNames)
            {
                if (string.Equals(candidate, _classroomGreeterName, StringComparison.OrdinalIgnoreCase))
                    return _classroomGreeterName;
            }
        }

        var rng = new RandomNumberGenerator();
        rng.Randomize();
        _classroomGreeterName = absentNames[rng.RandiRange(0, absentNames.Count - 1)];
        return _classroomGreeterName;
    }

    public static Godot.Collections.Dictionary CreateSaveData()
    {
        var entries = new Godot.Collections.Array();
        foreach (SchoolFriendSpawn spawn in SpawnPlan)
        {
            entries.Add(new Godot.Collections.Dictionary
            {
                ["name"] = spawn.Name,
                ["x"] = spawn.Position.X,
                ["y"] = spawn.Position.Y
            });
        }

        return new Godot.Collections.Dictionary
        {
            ["day"] = _planDay,
            ["after_assessment"] = _planAfterAssessment,
            ["created"] = _hasPlan,
            ["classroom_greeter"] = _classroomGreeterName,
            ["entries"] = entries
        };
    }

    public static void RestoreFromSaveData(
        Godot.Collections.Dictionary data, int currentDay, bool assessmentComplete)
    {
        Reset();

        if (!data.ContainsKey("created") ||
            data["created"].VariantType != Variant.Type.Bool ||
            !data["created"].AsBool() ||
            !data.ContainsKey("day") ||
            data["day"].VariantType != Variant.Type.Int ||
            data["day"].AsInt32() != Math.Max(1, currentDay) ||
            !data.ContainsKey("after_assessment") ||
            data["after_assessment"].VariantType != Variant.Type.Bool ||
            data["after_assessment"].AsBool() != assessmentComplete ||
            !data.ContainsKey("entries") ||
            data["entries"].VariantType != Variant.Type.Array)
            return;

        _planDay = Math.Max(1, currentDay);
        _planAfterAssessment = assessmentComplete;
        _hasPlan = true;

        foreach (Variant entryValue in data["entries"].AsGodotArray())
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;

            Godot.Collections.Dictionary entry = entryValue.AsGodotDictionary();
            if (!entry.ContainsKey("name") ||
                entry["name"].VariantType != Variant.Type.String ||
                !entry.ContainsKey("x") ||
                !entry.ContainsKey("y"))
                continue;

            float x = ReadFloat(entry["x"]);
            float y = ReadFloat(entry["y"]);
            string name = entry["name"].AsString();
            if (!string.IsNullOrWhiteSpace(name))
                SpawnPlan.Add(new SchoolFriendSpawn(name, new Vector2(x, y)));
        }

        // Saves from older builds could contain all three friends outside.
        // Trim those legacy rosters so the new 0-2 rule is applied immediately on load.
        if (SpawnPlan.Count > 2)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            while (SpawnPlan.Count > 2)
                SpawnPlan.RemoveAt(rng.RandiRange(0, SpawnPlan.Count - 1));

            _classroomGreeterName = string.Empty;
        }

        if (data.ContainsKey("classroom_greeter") &&
            data["classroom_greeter"].VariantType == Variant.Type.String)
        {
            _classroomGreeterName = data["classroom_greeter"].AsString();
        }
    }

    public static void Reset()
    {
        _planDay = -1;
        _planAfterAssessment = false;
        _hasPlan = false;
        _classroomGreeterName = string.Empty;
        SpawnPlan.Clear();
    }

    private static float ReadFloat(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Float => (float)value.AsDouble(),
            Variant.Type.Int => value.AsInt32(),
            _ => 0f
        };
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

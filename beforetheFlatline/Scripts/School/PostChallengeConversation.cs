using Godot;
using System.Collections.Generic;
using SchoolYardGame;

/// <summary>
/// After the final daily challenge, a random classmate starts a short branching conversation.
/// Finishing the conversation reloads World.tscn with the School room selected.
/// </summary>
public partial class PostChallengeConversation : Control
{
    [Export(PropertyHint.File, "*.tscn")]
    public string WorldScenePath { get; set; } = "res://Scenes/Home/Rooms/World.tscn";

    [Export(PropertyHint.File, "*.tscn")]
    public string SchoolRoomPath { get; set; } = "res://Scenes/School/School.tscn";

    [Export]
    public string SchoolSpawn { get; set; } = "FromHome";

    private VisualNovelScene _dialogue;
    private bool _isReturning;

    private static readonly (string Name, string PortraitPath)[] Characters =
    {
        ("Mich", "res://Assets/Characters/Mich.png"),
        ("Jepoy", "res://Assets/Characters/Jepoy.png"),
        ("Cess", "res://Assets/Characters/Cess.png")
    };

    private static readonly (string Topic, string Rant, string FollowUp)[] Topics =
    {
        (
            "the group project",
            "I made the slides, organized the notes, and reminded everyone about the deadline. The group chat stayed silent all afternoon, then suddenly everyone had opinions at midnight.",
            "And somehow, if one tiny thing is missing, the whole group acts like nobody knew about the deadline."
        ),
        (
            "the canteen food",
            "The canteen changed the menu again. I waited in line for ages, ordered pancit, and got about three noodles and a mysterious amount of sauce.",
            "I swear the serving gets smaller every time the price goes up."
        ),
        (
            "our latest assignment",
            "Our teacher said, 'This is the last activity for today.' Then came another worksheet, a surprise quiz, and a reminder about a project due tomorrow.",
            "I want to be responsible, but I also want one afternoon where my bag doesn't contain six new deadlines."
        ),
        (
            "the rumor going around school",
            "People keep repeating a rumor without checking whether it's true. Now everyone is acting like they know the whole story, and nobody asks the person involved.",
            "It would take less effort to ask one honest question than to invent ten versions of what happened."
        ),
        (
            "the school's unreliable Wi-Fi",
            "The Wi-Fi only works when nobody needs it. The moment I try to submit homework, the connection disappears like it has somewhere better to be.",
            "Then the page finally loads after the deadline, as if the internet is personally trying to annoy me."
        )
    };

    public override void _Ready()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        _dialogue = GetNode<VisualNovelScene>("%PostChallengeDialogue");
        _dialogue.PlayDemoOnReady = false;
        _dialogue.LoopDemo = false;
        _dialogue.DialogueFinished += OnDialogueFinished;

        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var character = Characters[rng.RandiRange(0, Characters.Length - 1)];
        var topic = Topics[rng.RandiRange(0, Topics.Length - 1)];

        _dialogue.SetPortrait(DialogueSide.Left, null);
        _dialogue.SetPortrait(DialogueSide.Right, GD.Load<Texture2D>(character.PortraitPath));
        _dialogue.StartDialogue(BuildConversation(character.Name, topic.Topic, topic.Rant, topic.FollowUp));
    }

    public override void _ExitTree()
    {
        if (_dialogue != null && IsInstanceValid(_dialogue))
            _dialogue.DialogueFinished -= OnDialogueFinished;
    }

    private static IReadOnlyList<DialogueLine> BuildConversation(
        string character,
        string topic,
        string rant,
        string followUp)
    {
        return new List<DialogueLine>
        {
            new(
                character,
                $"Hey, can I rant to you about {topic}? The challenges were stressful enough, and now this is getting on my nerves.",
                DialogueSide.Right,
                Choices: new[]
                {
                    new DialogueChoice("Go on. I'm listening.", Goto: 1),
                    new DialogueChoice("What happened this time?", Goto: 2)
                }),

            new(
                character,
                rant,
                DialogueSide.Right,
                Next: 3),

            new(
                character,
                $"Okay, here's the short version. {rant}",
                DialogueSide.Right,
                Next: 3),

            new(
                character,
                $"{followUp} Am I overreacting, or would that annoy you too?",
                DialogueSide.Right,
                Choices: new[]
                {
                    new DialogueChoice("That's fair. I'd be annoyed too.", Goto: 4),
                    new DialogueChoice("Maybe talk it out before assuming the worst.", Goto: 5)
                }),

            new(
                character,
                "Exactly! Thank you for actually hearing me out instead of changing the subject.",
                DialogueSide.Right,
                Next: 6),

            new(
                character,
                "Maybe you're right. I'll ask them about it before I get more worked up. I needed that reminder.",
                DialogueSide.Right,
                Next: 6),

            new(
                character,
                "Anyway, thanks for sticking around. Come on, let's head back to school before we get into trouble.",
                DialogueSide.Right,
                Next: 7)
        };
    }

    private void OnDialogueFinished()
    {
        if (_isReturning)
            return;

        _isReturning = true;

        // The school is a Room inside World.tscn, not a standalone playable world.
        DayClock.Instance?.MarkSchoolChallengeCompletedToday();
        DayClock.Instance?.SkipSchoolGreetingOnNextLoad();
        global::RoomManager.RequestStartRoomOnNextLoad(SchoolRoomPath, SchoolSpawn);

        if (!ResourceLoader.Exists(WorldScenePath))
        {
            GD.PushError($"PostChallengeConversation: World scene not found: {WorldScenePath}");
            _isReturning = false;
            return;
        }

        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, WorldScenePath);
    }
}

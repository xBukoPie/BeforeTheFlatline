using Godot;
using System.Collections.Generic;
using SchoolYardGame;

public partial class PostChallengeConversation : Control
{
    [Export(PropertyHint.File, "*.tscn")]
    public string WorldScenePath { get; set; } = "res://Scenes/Home/Areas/World.tscn";

    [Export(PropertyHint.File, "*.tscn")]
    public string HomeRoomPath { get; set; } = "res://Scenes/Home/Areas/PlayerRoom.tscn";

    [Export]
    public string HomeSpawn { get; set; } = "startSpawn";

    [Export(PropertyHint.File, "*.tscn")]
    public string LotRoomPath { get; set; } = "res://Scenes/Home/Areas/Yard.tscn";

    [Export]
    public string LotSpawn { get; set; } = "FromSchool";

    private VisualNovelScene _dialogue;
    private bool _isReturning;

    private static readonly (string Name, string PortraitPath)[] Characters =
    {
        ("Mich", "res://Assets/Characters/Mich.png"),
        ("Jepoy", "res://Assets/Characters/Jepoy.png"),
        ("Cess", "res://Assets/Characters/Cess.png")
    };

    private sealed record InteractionTopic(string Opening, string FirstReply, string SecondReply, string FirstResponse, string SecondResponse, string Closing);

    private static readonly InteractionTopic[] Topics =
    {
        new("Hey, {player}, did you understand today's lesson on conditions?", "I think so, but I need more practice.", "Yeah, the examples helped.", "Same here. The THEN and ELSE parts finally make sense to me.", "Good. Maybe the next activity will feel easier too.", "Anyway, see you tomorrow. Take care on your way home."),
        new("Hey, {player}, are you joining any school clubs this term?", "I'm still deciding.", "I might join the art club.", "There are a few interesting ones. We should check the sign-up board.", "That sounds fun. I might visit their next meeting.", "Let me know what you decide. Maybe we can join together."),
        new("Have you tried the new food stall near the canteen, {player}?", "Not yet. Is it good?", "I tried it earlier.", "People say the sandwiches are worth trying. I might get one tomorrow.", "Then you already know the best thing on the menu. Save me a recommendation.", "Now I'm getting hungry again. See you around."),
        new("I missed one note from today's lesson, {player}. Could I compare notes with you?", "Sure. Let's compare them tomorrow.", "I only wrote down the main points.", "Thanks. That should help me fill in the part I missed.", "That's still useful. We can ask the teacher about the details.", "Thanks for helping. I'll catch you in class."),
        new("The sports festival is coming up, {player}. Are you watching any events?", "I'll probably watch the games.", "I might join one of them.", "Same. I want to support our class without getting drafted into a team.", "Good luck. I'll cheer for you if you end up joining.", "We should meet up during the festival. See you later."),
        new("Do you usually study in the library after class, {player}?", "Sometimes. It's quiet there.", "Not really. I prefer studying at home.", "The quiet helps me focus. Maybe I'll see you there sometime.", "Fair enough. Everyone has a different routine.", "Good luck with your work. Have a safe trip home."),
        new("Our teacher mentioned a class activity for next week, {player}. Did you hear about it?", "I only caught part of the announcement.", "Yeah, I wrote down the date.", "I'll ask around so I don't miss the details.", "Could you remind me of the date tomorrow? I don't want to forget.", "Let's check the announcement board before class. See you."),
        new("Hey, {player}, what kind of music do you listen to while studying?", "Usually something quiet.", "Anything with a good beat.", "I use instrumental playlists. Lyrics distract me too easily.", "I need something energetic or I'll fall asleep over my notes.", "Send me a song recommendation sometime. Later!"),
        new("I found a pencil case near the classroom, {player}. Have you seen anyone looking for one?", "Maybe we should leave it with the teacher.", "Let's check the name tag first.", "Good idea. The teacher can help return it to the owner.", "There's a name inside. I'll bring it to the office.", "Glad that's sorted. See you tomorrow."),
        new("The weather has been strange lately, {player}. Do you think it will rain later?", "It looks like it might.", "I hope it stays clear.", "I'll bring an umbrella just in case.", "Same. I don't want to get caught in the rain on the way home.", "Take care out there. See you next class."),
        new("Hey, {player}, did you finish the book you were reading?", "Not yet. I'm still halfway through.", "I finished it last night.", "No spoilers, please. I want to find out what happens myself.", "I'll lend it to you sometime. I think you'd like it.", "Let's talk about it after you finish. Bye!")
    };

    public override void _Ready()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.Running = true;

        _dialogue = GetNode<VisualNovelScene>("%PostChallengeDialogue");
        _dialogue.PlayDemoOnReady = false;
        _dialogue.LoopDemo = false;
        _dialogue.DialogueFinished += OnDialogueFinished;

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        // Some school days end quietly without another conversation.
        if (rng.Randf() < 0.35f)
        {
            _dialogue.Visible = false;
            CallDeferred(MethodName.OnDialogueFinished);
            return;
        }

        var character = Characters[rng.RandiRange(0, Characters.Length - 1)];
        var topic = Topics[rng.RandiRange(0, Topics.Length - 1)];

        _dialogue.SetPortrait(DialogueSide.Left, null);
        _dialogue.SetPortrait(DialogueSide.Right, GD.Load<Texture2D>(character.PortraitPath));
        _dialogue.StartDialogue(BuildConversation(character.Name, topic));
    }

    public override void _ExitTree()
    {
        if (_dialogue != null && IsInstanceValid(_dialogue))
            _dialogue.DialogueFinished -= OnDialogueFinished;
    }

    private static IReadOnlyList<DialogueLine> BuildConversation(string character, InteractionTopic topic)
    {
        return new List<DialogueLine>
        {
            new(character, topic.Opening, DialogueSide.Right, Choices: new[]
            {
                new DialogueChoice(topic.FirstReply, Goto: 1),
                new DialogueChoice(topic.SecondReply, Goto: 2)
            }),
            new(character, topic.FirstResponse, DialogueSide.Right, Next: 3),
            new(character, topic.SecondResponse, DialogueSide.Right, Next: 3),
            new(character, topic.Closing, DialogueSide.Right)
        };
    }

    private void OnDialogueFinished()
    {
        if (_isReturning)
            return;

        _isReturning = true;

        var clock = DayClock.Instance;
        if (clock != null)
        {
            clock.MarkSchoolChallengeCompletedToday();
            clock.Running = false;
            float homeTime = Mathf.Min(23.75f, clock.SchoolClosingHour + 0.25f);
            if (clock.Hour < homeTime)
                clock.SetHourOfDay(homeTime);
        }

        bool keyWaitingOnLot = RoomKeyProgression.Instance?.HasPendingKey == true;
        string destinationRoom = keyWaitingOnLot ? LotRoomPath : HomeRoomPath;
        string destinationSpawn = keyWaitingOnLot ? LotSpawn : HomeSpawn;
        global::RoomManager.RequestStartRoomOnNextLoad(destinationRoom, destinationSpawn);

        if (!ResourceLoader.Exists(WorldScenePath))
        {
            GD.PushError($"PostChallengeConversation: World scene not found: {WorldScenePath}");
            _isReturning = false;
            return;
        }

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(WorldScenePath);
        else
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, WorldScenePath);
    }
}

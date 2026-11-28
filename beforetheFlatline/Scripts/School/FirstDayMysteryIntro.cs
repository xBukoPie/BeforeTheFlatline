using Godot;
using System.Collections.Generic;
using SchoolYardGame;

public partial class FirstDayMysteryIntro : Control
{
    private const string PortraitPath = "res://Assets/Characters/Mysterious.png";
    private const string HallwayBackgroundPath = "res://Assets/Map/School/schoolHallway.jpg";

    private VisualNovelScene _dialogue;
    private bool _finishing;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildHallwayBackground();
        _dialogue = GetNode<VisualNovelScene>("%MysteryDialogue");
        _dialogue.PlayDemoOnReady = false;
        _dialogue.LoopDemo = false;
        _dialogue.ShowBackground = false;

        Texture2D portrait = GD.Load<Texture2D>(PortraitPath);
        _dialogue.ShowPlaceholders = false;
        _dialogue.SetPortrait(DialogueSide.Left, null);
        _dialogue.SetPortrait(DialogueSide.Right, portrait);
        if (portrait == null)
            GD.PushWarning($"FirstDayMysteryIntro: portrait not found at '{PortraitPath}'.");

        _dialogue.DialogueFinished += OnDialogueFinished;
        _dialogue.StartDialogue(BuildLines());
    }

    private void BuildHallwayBackground()
    {
        Texture2D texture = GD.Load<Texture2D>(HallwayBackgroundPath);
        if (texture == null)
        {
            GD.PushWarning($"FirstDayMysteryIntro: hallway background not found at '{HallwayBackgroundPath}'.");
            return;
        }

        var background = new TextureRect
        {
            Name = "HallwayBackground",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        MoveChild(background, 0);
    }

    public override void _ExitTree()
    {
        if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue))
            _dialogue.DialogueFinished -= OnDialogueFinished;
    }

    private static IReadOnlyList<DialogueLine> BuildLines()
    {
        var lines = new List<DialogueLine>
        {
            new("...", "You came back. I wondered whether you would return to this hallway.", DialogueSide.Right),
            new("You", "Do I know you?", DialogueSide.Left),
            new("...", "Perhaps. Or perhaps you remember the feeling of meeting me more clearly than my face.", DialogueSide.Right),
            new("...", "Tell me something. Does this hallway feel ordinary to you?", DialogueSide.Right)
        };

        int ordinaryReply = lines.Count + 1;
        int familiarReply = lines.Count + 2;
        int sharedFollowUp = lines.Count + 3;
        lines.Add(new DialogueLine(
            "...",
            "A place can look ordinary and still leave you with a strange feeling.",
            DialogueSide.Right,
            Choices: new[]
            {
                new DialogueChoice("It's just a hallway. Nothing unusual.", Goto: ordinaryReply),
                new DialogueChoice("I can't explain it... something feels familiar.", Goto: familiarReply)
            }));
        lines.Add(new DialogueLine(
            "You",
            "It's just a hallway. Nothing unusual.",
            DialogueSide.Left,
            Next: sharedFollowUp));
        lines.Add(new DialogueLine(
            "You",
            "I can't explain it... something feels familiar.",
            DialogueSide.Left,
            Next: sharedFollowUp));

        lines.Add(new DialogueLine(
            "...",
            "That feeling is worth noticing. Sometimes the places you know best hold memories you have not reached yet.",
            DialogueSide.Right));
        lines.Add(new DialogueLine(
            "...",
            "Your rooms, your routines, the little things you pass without looking... they may tell you more than you expect.",
            DialogueSide.Right));
        lines.Add(new DialogueLine(
            "...",
            "And when you find a photograph, don't dismiss it as decoration. Take a moment and look closely.",
            DialogueSide.Right));
        lines.Add(new DialogueLine(
            "You",
            "What are you trying to tell me?",
            DialogueSide.Left));
        lines.Add(new DialogueLine(
            "...",
            "Not every answer is ready to be spoken. Let each memory return in its own time. Don't force it.",
            DialogueSide.Right));
        lines.Add(new DialogueLine(
            "...",
            "You've been measuring yourself against everyone around you. A difficult day does not mean you're falling behind.",
            DialogueSide.Right));
        lines.Add(new DialogueLine(
            "You",
            "How would you know anything about that?",
            DialogueSide.Left));
        lines.Add(new DialogueLine(
            "...",
            "Some things are easier to notice from a distance.",
            DialogueSide.Right));
        lines.Add(new DialogueLine(
            "...",
            "You don't have to carry everything alone. When someone offers to help, let them.",
            DialogueSide.Right));
        lines.Add(new DialogueLine(
            "...",
            "Go to class. The next step is enough for today. We'll speak again when you remember a little more.",
            DialogueSide.Right));
        lines.Add(new DialogueLine("...", "Bye bye.", DialogueSide.Right));

        return lines;
    }

    private async void OnDialogueFinished()
    {
        if (_finishing)
            return;

        _finishing = true;
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, 0.35f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);

        DayClock.Instance?.MarkFirstDayMysteryEncounterCompleted();
        SaveManager.Instance?.RequestAutoSave();
        GetParent()?.RemoveChild(this);
        QueueFree();
    }
}

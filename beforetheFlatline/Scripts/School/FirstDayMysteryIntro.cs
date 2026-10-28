using Godot;
using System.Collections.Generic;
using SchoolYardGame;

public partial class FirstDayMysteryIntro : Control
{
    private const string PortraitPath = "res://Assets/Characters/Mysterious.png";

    public Door DoorToContinue { get; set; }

    private VisualNovelScene _dialogue;
    private bool _finishing;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
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

    public override void _ExitTree()
    {
        if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue))
            _dialogue.DialogueFinished -= OnDialogueFinished;
    }

    private static IReadOnlyList<DialogueLine> BuildLines() => new List<DialogueLine>
    {
        new("...", "So. You found your way here again. I wondered if this place would feel familiar to you.", DialogueSide.Right),
        new("...", "The rules are simple enough. Move with WASD or the arrow keys. Press E when an interaction prompt appears. Most things won't respond unless you're close enough.", DialogueSide.Right),
        new("...", "Time moves while you explore. When the school asks you to complete an assessment, think carefully. Failure costs sanity, and recovering it can take longer than you'd like.", DialogueSide.Right),
        new("...", "The keys matter. Complete tasks to earn them, then use them on doors that remain locked. Some rooms have been closed for a long time. The school doesn't hand out keys for nothing.", DialogueSide.Right),
        new("...", "There are things behind those doors that people stopped talking about. Perhaps they hoped that if a room stayed locked long enough, whatever happened there would be forgotten.", DialogueSide.Right),
        new("...", "And you... you look at these halls like they owe you an answer. Maybe they do. There are gaps in your story, and I would not rush to fill them. Some doors are better opened when you are ready—", DialogueSide.Right),
        new("...", "*BRRRRIIING!* The classroom bell rings sharply down the corridor, cutting the sentence short.", DialogueSide.Right),
        new("...", "Ah. The bell. That's my cue to leave. We shouldn't keep your classmates waiting.", DialogueSide.Right),
        new("...", "Pay attention to what feels familiar. And be careful what you decide to remember.", DialogueSide.Right),
        new("...", "Bye bye.", DialogueSide.Right)
    };

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
        Door door = DoorToContinue;

        GetParent()?.RemoveChild(this);
        QueueFree();

        if (door != null && GodotObject.IsInstanceValid(door))
            door.CallDeferred(nameof(Door.ContinueAfterMysteryIntro));
    }
}

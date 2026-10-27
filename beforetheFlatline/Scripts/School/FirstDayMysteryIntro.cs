using Godot;
using System.Collections.Generic;
using SchoolYardGame;

/// <summary>
/// One-time tutorial and lore encounter shown by the classroom door before the classroom overlay opens.
/// </summary>
public partial class FirstDayMysteryIntro : Control
{
    private const string PortraitPath = "res://Assets/Characters/Mysterious.svg";

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
        new("...", "So. You made it. I wondered whether you'd come back.", DialogueSide.Right),
        new("...", "The rules are simple enough. Move with WASD or the arrow keys. Press E when an interaction prompt appears. Most things won't respond unless you're close enough.", DialogueSide.Right),
        new("...", "Time moves while you explore. When the school asks you to complete an assessment, think carefully. Failure costs sanity, and recovering it can take longer than you'd like.", DialogueSide.Right),
        new("...", "The keys matter. Complete tasks to earn them, then use them on doors that remain locked. Some rooms have been closed for a long time. The school doesn't hand out keys for nothing.", DialogueSide.Right),
        new("...", "There are things behind those doors that people stopped talking about. Perhaps they hoped that if a room stayed locked long enough, whatever happened there would be forgotten.", DialogueSide.Right),
        new("...", "And you... you came back here for a reason. There's something about you that you haven't remembered yet. When the rooms begin to open, don't assume everything inside is waiting for you—", DialogueSide.Right),
        new("...", "*BRRRRIIING!* The classroom bell rings sharply down the corridor, cutting the sentence short.", DialogueSide.Right),
        new("...", "Ah. The bell. That's my cue to leave. We shouldn't keep your classmates waiting.", DialogueSide.Right),
        new("...", "Watch what you unlock. And take care of yourself.", DialogueSide.Right),
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

        // Remove this encounter before asking the same doorway to open the classroom overlay.
        GetParent()?.RemoveChild(this);
        QueueFree();

        if (door != null && GodotObject.IsInstanceValid(door))
            door.CallDeferred(nameof(Door.ContinueAfterMysteryIntro));
    }
}

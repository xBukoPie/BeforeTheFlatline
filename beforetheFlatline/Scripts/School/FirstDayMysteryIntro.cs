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

    private static IReadOnlyList<DialogueLine> BuildLines() => new List<DialogueLine>
    {
        new("...", "Before the bell, let me explain how to get through an ordinary day here. You can move with WASD or the arrow keys. The same directions are available on a connected gamepad.", DialogueSide.Right),
        new("...", "When an object, person, door, key, or photograph shows an interaction prompt, move close and press E. The prompt only appears when you are within reach. You do not need to press it from across the room.", DialogueSide.Right),
        new("...", "Doors take you between rooms and areas. Keys are picked up from the world, not sent straight into your pocket by a score screen. Photographs are important too. Look for them when a room becomes accessible.", DialogueSide.Right),
        new("...", "You can pause with Esc. The pause menu contains the Journal, save and load options, settings, and a way back to the title. The Journal keeps your memory progress, friends' information, assessment history, and player statistics.", DialogueSide.Right),
        new("...", "There are three manual save slots and an autosave. Save while you are exploring the world. The assessment workspace is not a place to manage your save files, so prepare before entering class.", DialogueSide.Right),
        new("...", "The day has a schedule. School opens at 6:30. You must begin the school assessment before 12:00 or the missed-attendance penalty will be applied. School closes at 17:00, and night begins at 18:30.", DialogueSide.Right),
        new("...", "For this first school day, the clock is held still until you finish the first assessment. On later days, time moves while you explore. Sleep can carry you to the next school morning when the day allows it.", DialogueSide.Right),
        new("...", "Now, the assessments. Each one has four questions in a shuffled order: Sequence, Condition, Flowchart, and a Final task that combines ideas. The problem values and the wording are generated with variations, so do not expect every student to receive the exact same numbers or scenario.", DialogueSide.Right),
        new("...", "Each correct answer is worth ten points. Four correct answers make forty. You need at least thirty points to pass, which means three correct answers are enough. Your result is finalized after all four questions.", DialogueSide.Right),
        new("...", "For a Sequence problem, think in ordered steps: read the input, process the values, then display the result. Start clearly and make sure the algorithm reaches its end.", DialogueSide.Right),
        new("...", "For a Condition problem, use IF and ELSE. The condition decides which path should run. Close the decision with END IF, and check both the true and false outcomes.", DialogueSide.Right),
        new("...", "Pseudocode is not C# source code. It is a readable description of the algorithm. You will commonly use START, INPUT, assignments such as total = quantity * price, IF, ELSE, END IF, WHILE or FOR, OUTPUT, and END.", DialogueSide.Right),
        new("...", "Use a variable name that makes sense to you. You may call it total, cost, amount, or something else. The evaluator checks what your algorithm does with its test inputs and outputs; it does not require you to copy a particular variable name.", DialogueSide.Right),
        new("...", "A loop must have a clear stopping condition. Initialize counters and totals before the loop, update the counter inside it, and make sure the condition eventually becomes false. An endless loop will not pass the evaluator.", DialogueSide.Right),
        new("...", "The Flowchart task uses symbols. The oval is Start or End. The parallelogram is Input or Output. The rectangle is a Process, such as a calculation. The diamond is a Decision.", DialogueSide.Right),
        new("...", "Read a flowchart by following its arrows. Drag a symbol from the palette into the canvas. After placing it, use the small grip beside the block to drag it onto a connector arrow where it should be inserted. The text you entered stays with that block.", DialogueSide.Right),
        new("...", "A Decision needs two outgoing paths. The TRUE path follows when its condition is satisfied; the FALSE path follows otherwise. Give both branch outputs a clear meaning. The paths join again below, and the rest of the algorithm continues from that merge.", DialogueSide.Right),
        new("...", "A flowchart still needs a Start, an End, meaningful inputs and outputs, and connected steps. A picture with disconnected symbols is not a complete algorithm. Read both sides of every decision before submitting.", DialogueSide.Right),
        new("...", "Your sanity is your remaining ability to endure this place. A new run starts at one hundred. A failed assessment removes sanity; the penalty grows with the day, from thirty-four up to forty-six. A missed school deadline also adds Pressure and Anxiety.", DialogueSide.Right),
        new("...", "The effects are Dizziness, Confusion, Anxiety, and Pressure. They can stack. Dizziness distorts what you see, Confusion puts fragments of text at the edge of your vision, Anxiety brings static, and Pressure is accompanied by a heartbeat-like warning.", DialogueSide.Right),
        new("...", "Those effects do not simply disappear because time passes. Each has its own recovery target. Restoring sanity can clear them when the target is reached. Passing an assessment restores five sanity.", DialogueSide.Right),
        new("...", "Talking to each available friend for the first time that day restores two sanity. A bath restores four during the day or seven at night, and a late bath may clear one random debuff. Watching television late at night can cause Dizziness. Use these choices carefully.", DialogueSide.Right),
        new("...", "If sanity reaches zero, the bad ending begins. There is no ordinary retry button in the middle of that ending. Keep an eye on your status and use the recovery opportunities available to you.", DialogueSide.Right),
        new("...", "Do not confuse the assessment score with the hidden story points. The visible score decides whether you passed. Hidden story points are earned for the first correct completion of each challenge family that day. Repeating the same family will not let you farm the same reward.", DialogueSide.Right),
        new("...", "As task difficulty rises, a first-time correct challenge can award between twenty-five and forty-five hidden points. Keys become eligible at one hundred, two hundred, three hundred, and four hundred cumulative points. The next key is still locked behind the previous photograph.", DialogueSide.Right),
        new("...", "The order is fixed: Front Yard key opens the Kitchen; the Kitchen key pickup opens the Laundry; the Laundry key pickup opens the Bathroom; and the Bathroom key pickup opens the Master Bedroom. Examine each photograph before the next key can be offered.", DialogueSide.Right),
        new("...", "You may see Mich, Jepoy, or Cess around the school yard. Sometimes none of them will be there, and sometimes one or two will. Their conversations can help you recover a little, and very rarely they may say something that sounds like a clue.", DialogueSide.Right),
        new("...", "At home, the television, bicycle, and bath may or may not be available on a given day. Each available activity can be used once that day and takes time. Think about the school schedule before spending the afternoon.", DialogueSide.Right),
        new("...", "Professor Vox will review a programming topic before the assessment. Listen to the lesson, then apply the idea to the problem in front of you. You do not have to memorize every sentence. Understand the input, the steps, the branches, and the output.", DialogueSide.Right),
        new("...", "That is enough to begin. I have told you how to move, interact, learn, recover, and follow the keys. The rest is easier to understand by doing it yourself.", DialogueSide.Right),
        new("...", "The bell is about to ring. Go to class, and take your time with the questions. Pay attention to anything that feels familiar.", DialogueSide.Right),
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
        SaveManager.Instance?.RequestAutoSave();
        GetParent()?.RemoveChild(this);
        QueueFree();
    }
}

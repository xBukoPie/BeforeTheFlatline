using Godot;
using System;
using System.Collections.Generic;
using SchoolYardGame;

public partial class FirstDayTutorialManager : Node
{
    public static FirstDayTutorialManager Instance { get; private set; }

    private const string WorldPath = "res://Scenes/Home/Areas/World.tscn";
    private const string BedroomPath = "res://Scenes/Home/Areas/PlayerRoom.tscn";
    private const string BedroomSpawn = "startSpawn";
    private const string PortraitPath = "res://Assets/Characters/Mysterious.png";

    private sealed record GuideStep(string Text, Control Target = null);

    private CanvasLayer _layer;
    private Control _root;
    private Panel _focus;
    private Control _target;
    private Label _body;
    private Label _heading;
    private Button _next;
    private List<GuideStep> _steps = new();
    private int _stepIndex;
    private Action _completed;
    private bool _returningHome;
    private Node2D _frozenPlayer;
    private ProcessModeEnum _previousPlayerMode;
    private bool _ownsPlayerFreeze;

    public bool IsActive => DayClock.Instance?.IsFirstDayTutorialActive == true;
    public bool IsGuidedClassroom => IsActive && DayClock.Instance.FirstDayTutorialStep == 5;
    public bool ShouldShowSchoolCompanion =>
        IsActive && DayClock.Instance.FirstDayTutorialStep >= 3 &&
        DayClock.Instance.FirstDayTutorialStep <= 5;
    public bool ShouldExplainResults => IsActive && DayClock.Instance.FirstDayTutorialStep == 5;

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        // The new route replaces the old one-off classroom mystery interaction.
        if (DayClock.Instance?.IsFirstDayTutorialActive == true)
            DayClock.Instance.MarkFirstDayMysteryEncounterCompleted();
    }

    public override void _Process(double delta) => UpdateFocus();

    public override void _ExitTree()
    {
        CloseGuide();
        RestorePlayer();
        if (Instance == this)
            Instance = null;
    }

    public void RegisterDoor(Door door)
    {
        if (door == null || !GodotObject.IsInstanceValid(door))
            return;

        if (!door.IsInGroup("world_doors"))
            door.AddToGroup("world_doors");
        RefreshDoor(door);
    }

    private void RefreshDoor(Door door)
    {
        if (door == null || !GodotObject.IsInstanceValid(door))
            return;

        door.SetTutorialHighlight(IsExpectedDoor(door));
    }

    private void RefreshAllDoors()
    {
        foreach (Node node in GetTree().GetNodesInGroup("world_doors"))
            if (node is Door door)
                RefreshDoor(door);
    }

    private bool IsExpectedDoor(Door door)
    {
        if (!IsActive || door == null)
            return false;

        string expected = DayClock.Instance.FirstDayTutorialStep switch
        {
            0 => "ToLivingRoom",
            1 => "ToYard",
            2 => "ToSchool",
            4 => "ToClassroom",
            _ => string.Empty
        };

        return string.Equals(door.Name.ToString(), expected, StringComparison.OrdinalIgnoreCase);
    }

    public bool CanUseDoor(Door door) => !IsActive || IsExpectedDoor(door);

    public string GetBlockedDoorMessage() =>
        DayClock.Instance?.FirstDayTutorialStep switch
        {
            0 => "Follow the highlighted door into the living room.",
            1 => "Follow the highlighted door out to the yard.",
            2 => "Follow the highlighted gate to school.",
            3 => "Stay with the mysterious person for the school introduction.",
            4 => "Follow the highlighted entrance into the classroom.",
            5 => "The assessment is in progress. Follow the guide.",
            _ => "Finish the first-day tutorial before leaving."
        };

    public void OnExpectedDoorActivated(Door door)
    {
        if (!IsActive || !IsExpectedDoor(door))
            return;

        int next = DayClock.Instance.FirstDayTutorialStep switch
        {
            0 => 1,
            1 => 2,
            2 => 3,
            4 => 5,
            _ => DayClock.Instance.FirstDayTutorialStep
        };

        DayClock.Instance.SetFirstDayTutorialStep(next);
        RefreshAllDoors();
    }

    public void OnRoomLoaded(string roomPath, string roomName)
    {
        string loadedName = (roomName ?? string.Empty).Trim();

        if (_returningHome &&
            (string.Equals(loadedName, "PlayerRoom", StringComparison.OrdinalIgnoreCase) ||
             roomPath.EndsWith("/PlayerRoom.tscn", StringComparison.OrdinalIgnoreCase)))
        {
            _returningHome = false;
            DayClock.Instance?.FinishFirstDayTutorialAndStartDayTwo();
            SaveManager.Instance?.RequestAutoSave();
            RefreshAllDoors();
            return;
        }

        if (!IsActive)
        {
            RefreshAllDoors();
            return;
        }

        int step = DayClock.Instance.FirstDayTutorialStep;
        if (step == 0 && (string.Equals(loadedName, "PlayerRoom", StringComparison.OrdinalIgnoreCase) ||
            roomPath.EndsWith("/PlayerRoom.tscn", StringComparison.OrdinalIgnoreCase)))
        {
            ShowRoomGuide("You are in your bedroom. We should get moving. Follow the glowing door into the living room.");
        }
        else if (step == 1 && (string.Equals(loadedName, "LivingRoom", StringComparison.OrdinalIgnoreCase) ||
            roomPath.EndsWith("/LivingRoom.tscn", StringComparison.OrdinalIgnoreCase)))
        {
            ShowRoomGuide("Good. This is the living room. The next highlighted door leads outside to the yard.");
        }
        else if (step == 2 && (string.Equals(loadedName, "Yard", StringComparison.OrdinalIgnoreCase) ||
            roomPath.EndsWith("/Yard.tscn", StringComparison.OrdinalIgnoreCase)))
        {
            ShowRoomGuide("Now head through the highlighted school gate. School opens at 6:30, and you should arrive before noon.");
        }
        else if (step == 3 && (string.Equals(loadedName, "School", StringComparison.OrdinalIgnoreCase) ||
            roomPath.EndsWith("/School.tscn", StringComparison.OrdinalIgnoreCase)))
        {
            BeginSchoolIntroduction();
        }

        RefreshAllDoors();
    }

    private void ShowRoomGuide(string message)
    {
        var steps = new List<GuideStep>
        {
            new GuideStep(message)
        };

        if (DayClock.Instance?.FirstDayTutorialStep == 0)
            steps.Add(new GuideStep("Move with WASD or the arrow keys. When you see an interaction prompt near a door or object, walk close and press E. The glowing ring marks the door I want you to use next."));

        ShowGuidance("MYSTERIOUS PERSON", steps, RefreshAllDoors);
    }

    private void BeginSchoolIntroduction()
    {
        FreezePlayer();
        ShowGuidance("MYSTERIOUS PERSON", new[]
        {
            new GuideStep("You made it to school. First days can be confusing, so stay close. I will accompany you through the classroom and today's assessment."),
            new GuideStep("Move with WASD or the arrow keys. When an object or person shows an interaction prompt, stand nearby and press E. Doors, keys, photographs, and activities all use the same interaction action."),
            new GuideStep("The day follows a schedule. School opens at 6:30 AM, the attendance deadline is noon, school closes at 5:00 PM, and night begins at 6:30 PM. The first-day clock stays frozen until the assessment is finalized."),
            new GuideStep("Press Esc to open the pause menu. The Journal stores memory, friend, assessment, and player-stat information. You have three manual save slots and an autosave; saving is available while exploring the world."),
            new GuideStep("You will see classmates around the school. Speaking to each friend for the first time that day restores a little sanity. Sometimes they may share a very rare clue, but do not expect them to explain everything."),
            new GuideStep("At home, the television, bicycle, and bath may be available on a given day. Activities can advance time. A bath restores sanity, and sleep can take you to the next school morning when the schedule allows it."),
            new GuideStep("We will skip the usual Professor Vox lesson today. I will explain the assessments as you work, including pseudocode, flowcharts, scoring, and the consequences of mistakes."),
            new GuideStep("The highlighted entrance leads to your classroom. Go inside and we will begin.")
        }, () =>
        {
            DayClock.Instance?.SetFirstDayTutorialStep(4);
            RefreshAllDoors();
        });
    }

    public void BeginClassroomTutorial(Action startAssessment)
    {
        if (!IsGuidedClassroom)
        {
            startAssessment?.Invoke();
            return;
        }

        ShowGuidance("MYSTERIOUS PERSON", new[]
        {
            new GuideStep("Here we are. Professor Vox can take the day off. I will guide you through the assessment instead."),
            new GuideStep("There are four question types: Sequence, Condition, Flowchart, and Final. They appear in a shuffled order, and their word problems use randomized settings and values."),
            new GuideStep("A correct answer is worth 10 points. You need 30 out of 40 to pass. Read the full prompt, solve the problem, then submit it.")
        }, startAssessment);
    }

    public void ShowQuestionTutorial(
        string questionId,
        bool isFlowchart,
        Control codeEditor,
        Control flowCanvas,
        IReadOnlyDictionary<string, Button> palette,
        Button submitButton)
    {
        if (!IsGuidedClassroom)
            return;

        var steps = new List<GuideStep>();
        string id = (questionId ?? string.Empty).ToLowerInvariant();

        if (!isFlowchart)
        {
            string explanation = id switch
            {
                "condition" => "This is a Condition question. Use IF and ELSE to describe both possible outcomes. Close the decision with END IF.",
                "final" => "This Final question combines ideas. Identify every input, calculation, condition, and loop described in the prompt, then put them in a logical order.",
                _ => "This is a Sequence question. Write the algorithm in order: START, INPUT, calculations or processing, OUTPUT, and END."
            };
            steps.Add(new GuideStep(explanation, codeEditor));
            steps.Add(new GuideStep("This is the pseudocode editor. Use readable steps and any variable names you prefer. The evaluator checks the behavior and output against different test inputs, not whether you copied a particular variable name.", codeEditor));
        }
        else
        {
            steps.Add(new GuideStep("This is the flowchart workspace. START and END are built in and cannot be removed. Clear Flowchart only clears blocks you add.", flowCanvas));
            if (palette != null && palette.TryGetValue("Input", out Button input))
                steps.Add(new GuideStep("Add an Input block when the algorithm needs a value from the user. Click the block's text field and define the variable.", input));
            if (palette != null && palette.TryGetValue("Process", out Button process))
                steps.Add(new GuideStep("Use a Process block for a calculation or assignment, such as total = quantity * price.", process));
            if (palette != null && palette.TryGetValue("Output", out Button output))
                steps.Add(new GuideStep("Use an Output block to display the answer or message required by the problem.", output));
            if (palette != null && palette.TryGetValue("Decision", out Button decision))
                steps.Add(new GuideStep("A Decision block is a diamond. It creates TRUE and FALSE branches. You can add several blocks inside either branch, including another Decision.", decision));
            steps.Add(new GuideStep("Drag a palette block onto the nearest connector. To move a placed block, drag its grip and drop it on the connector where it belongs. The block's entered text stays with it.", flowCanvas));
            steps.Add(new GuideStep("Check both branches. Lines without a following block have no arrow tip; a tip marks a path that connects to another block.", flowCanvas));
        }

        steps.Add(new GuideStep("When you are ready, press Submit & Next. An incomplete answer can be submitted, but it will not earn the question's points.", submitButton));
        ShowGuidance("MYSTERIOUS PERSON • ASSESSMENT GUIDE", steps, null);
    }

    public void ShowResultsTutorial(Control scoreTarget, Control detailsTarget, Action completed)
    {
        if (!ShouldExplainResults)
        {
            completed?.Invoke();
            return;
        }

        DayClock.Instance?.SetFirstDayTutorialStep(6);
        ShowGuidance("MYSTERIOUS PERSON • RESULTS", new[]
        {
            new GuideStep("BEST SCORE is your assessment score, out of 40. Each correct answer is worth 10 points, and 30 points is enough to pass.", scoreTarget),
            new GuideStep("POINTS RECEIVED are separate from the assessment score. These hidden story points come from eligible first-time correct challenge completions and help unlock future room keys.", scoreTarget),
            new GuideStep("SANITY shows how much you have left. A passing assessment restores 5 sanity after you return home. A failed assessment applies its penalty once, also after you return home. The penalty grows as the days progress.", detailsTarget),
            new GuideStep("The assessment debuffs are Dizziness, Confusion, Anxiety, and Pressure. They can stack and remain until their individual recovery targets are met. Dizziness distorts your view, Confusion adds stray letters, Anxiety creates static, and Pressure brings heartbeat cues.", detailsTarget),
            new GuideStep("Talking to a friend for the first time that day restores 2 sanity. A bath restores 4 during the day or 7 at night, and a late-night bath may clear one debuff. Passing an assessment is another recovery opportunity."),
            new GuideStep("A listed key reward means a key is available to collect in the world. It is not added automatically. The order is Kitchen, Laundry, Bathroom, then Master Bedroom; examine each room's photograph before the next key can appear."),
            new GuideStep("When you continue, the assessment result is finalized at home. You will return to your bedroom and the next day will begin. You can use the Journal to review your progress whenever you need to.")
        }, completed);
    }

    public bool CompleteResultsAndReturnHome()
    {
        if (!IsActive || DayClock.Instance.FirstDayTutorialStep < 6)
            return false;

        BeginReturnHomeAfterTutorial(skipped: false);
        return true;
    }

    public void SkipWholeDay()
    {
        if (!IsActive)
            return;

        BeginReturnHomeAfterTutorial(skipped: true);
    }

    private void BeginReturnHomeAfterTutorial(bool skipped)
    {
        CloseGuide();
        RestorePlayer();

        if (skipped)
            DailyTestSession.Reset();

        _returningHome = true;
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        RoomManager.RequestStartRoomOnNextLoad(BedroomPath, BedroomSpawn);
        ChangeToWorld();
    }

    private async void ChangeToWorld()
    {
        SceneTransition transition = SceneTransition.Instance;
        while (transition != null && GodotObject.IsInstanceValid(transition) && transition.IsBusy)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!IsInsideTree())
                return;
            transition = SceneTransition.Instance;
        }

        if (!IsInsideTree())
            return;

        if (transition != null && GodotObject.IsInstanceValid(transition))
            transition.ChangeSceneToFile(WorldPath);
        else
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, WorldPath);
    }

    private void ShowGuidance(string title, IReadOnlyList<GuideStep> steps, Action completed)
    {
        CloseGuide();
        if (steps == null || steps.Count == 0)
        {
            completed?.Invoke();
            return;
        }

        _steps = new List<GuideStep>(steps);
        _stepIndex = 0;
        _completed = completed;

        _layer = new CanvasLayer
        {
            Name = "FirstDayTutorialLayer",
            Layer = 124,
            ProcessMode = ProcessModeEnum.Always
        };
        AddChild(_layer);

        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);

        var dim = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.26f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dim);

        _focus = new Panel
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _focus.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.76f, 0.24f, 0.06f),
            BorderColor = new Color("ffd166"),
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7
        });
        _root.AddChild(_focus);

        var card = new PanelContainer
        {
            Name = "TutorialCard",
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorLeft = 0.04f,
            AnchorRight = 0.96f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetTop = -205,
            OffsetBottom = -18
        };
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.063f, 0.094f, 0.153f, 0.98f),
            BorderColor = new Color("d4a95c"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 11,
            CornerRadiusTopRight = 11,
            CornerRadiusBottomLeft = 11,
            CornerRadiusBottomRight = 11,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 14,
            ContentMarginBottom = 14
        });
        _root.AddChild(card);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        card.AddChild(row);

        var portrait = new TextureRect
        {
            Texture = GD.Load<Texture2D>(PortraitPath),
            CustomMinimumSize = new Vector2(112, 150),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(portrait);

        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 7);
        row.AddChild(content);

        _heading = new Label { Text = title };
        _heading.AddThemeFontSizeOverride("font_size", 14);
        _heading.AddThemeColorOverride("font_color", new Color("d7b98b"));
        content.AddChild(_heading);

        _body = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _body.AddThemeFontSizeOverride("font_size", 17);
        _body.AddThemeColorOverride("font_color", new Color("edf2f7"));
        content.AddChild(_body);

        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        buttons.AddThemeConstantOverride("separation", 10);
        content.AddChild(buttons);

        var skip = new Button { Text = "Skip First Day", CustomMinimumSize = new Vector2(145, 38) };
        skip.Pressed += SkipWholeDay;
        buttons.AddChild(skip);

        _next = new Button { Text = "Next", CustomMinimumSize = new Vector2(120, 38) };
        _next.Pressed += AdvanceGuide;
        buttons.AddChild(_next);

        FreezePlayer();
        RenderGuideStep();
    }

    private void RenderGuideStep()
    {
        if (_stepIndex < 0 || _stepIndex >= _steps.Count)
            return;

        GuideStep step = _steps[_stepIndex];
        _body.Text = step.Text;
        _next.Text = _stepIndex == _steps.Count - 1 ? "Continue" : "Next";
        _target = step.Target;
        UpdateFocus();
    }

    private void AdvanceGuide()
    {
        if (_stepIndex + 1 < _steps.Count)
        {
            _stepIndex++;
            RenderGuideStep();
            return;
        }

        Action callback = _completed;
        CloseGuide();
        RestorePlayer();
        callback?.Invoke();
        RefreshAllDoors();
    }

    private void UpdateFocus()
    {
        if (_focus == null || !GodotObject.IsInstanceValid(_focus))
            return;

        if (_target == null || !GodotObject.IsInstanceValid(_target) ||
            !_target.IsInsideTree() || !_target.IsVisibleInTree())
        {
            _focus.Visible = false;
            return;
        }

        Rect2 rect = _target.GetGlobalRect();
        _focus.Position = rect.Position - new Vector2(7, 7);
        _focus.Size = rect.Size + new Vector2(14, 14);
        _focus.Visible = true;
    }

    private void CloseGuide()
    {
        if (_layer != null && GodotObject.IsInstanceValid(_layer))
            _layer.QueueFree();

        _layer = null;
        _root = null;
        _focus = null;
        _heading = null;
        _body = null;
        _next = null;
        _target = null;
        _steps.Clear();
        _stepIndex = 0;
        _completed = null;
    }

    private void FreezePlayer()
    {
        if (_ownsPlayerFreeze && _frozenPlayer != null && GodotObject.IsInstanceValid(_frozenPlayer))
            return;

        Node roomManager = GetTree().GetFirstNodeInGroup("room_manager");
        _frozenPlayer = roomManager?.GetNodeOrNull<Node2D>("Player");
        _ownsPlayerFreeze = false;

        if (_frozenPlayer == null || _frozenPlayer.ProcessMode == ProcessModeEnum.Disabled)
            return;

        _previousPlayerMode = _frozenPlayer.ProcessMode;
        _ownsPlayerFreeze = true;
        if (_frozenPlayer is CharacterBody2D body)
            body.Velocity = Vector2.Zero;
        _frozenPlayer.ProcessMode = ProcessModeEnum.Disabled;
    }

    private void RestorePlayer()
    {
        if (_ownsPlayerFreeze && _frozenPlayer != null && GodotObject.IsInstanceValid(_frozenPlayer))
            _frozenPlayer.ProcessMode = _previousPlayerMode;

        _ownsPlayerFreeze = false;
        _frozenPlayer = null;
    }
}

using Godot;
using System.Linq;
using SchoolYardGame;

/// <summary>Displays the overall assessment only after the four question scenes are complete.</summary>
public partial class TestResults : Control
{
    private bool _passed;
    private Label _title;
    private Label _summary;
    private Label _details;
    private Button _continueButton;

    public override void _Ready()
    {
        // The timed task segment ended; time resumes on the results/conversation screens.
        if (DayClock.Instance != null)
            DayClock.Instance.Running = true;

        BuildUi();

        bool alreadyFinalized = DailyTestSession.IsFinalized;
        _passed = DailyTestSession.FinalizeTest();

        int correct = DailyTestSession.CorrectCount;
        int attempted = DailyTestSession.AnsweredCount;
        _title.Text = _passed ? "ASSESSMENT PASSED" : "ASSESSMENT FAILED";
        _title.AddThemeColorOverride("font_color", _passed ? new Color("86efac") : new Color("fca5a5"));
        _summary.Text = $"{correct} / {DailyTestSession.QuestionCount} correct  •  {attempted} answered  •  Day {DailyTestSession.Day}";

        var lines = new System.Collections.Generic.List<string>();
        lines.Add(_passed
            ? "You met the passing requirement. The school day continues."
            : $"You needed {DailyTestSession.QuestionsNeededToPass} correct answers to pass. Your focus has been affected.");

        foreach (var result in DailyTestSession.Results)
        {
            string outcome = result.GaveUp ? "GAVE UP" : result.Correct ? "CORRECT" : "INCORRECT";
            lines.Add($"{result.ChallengeId.ToUpperInvariant()}: {outcome}");
        }

        foreach (string notification in DailyTestSession.PendingNotifications)
            lines.Add(notification);

        // Read the persistent autoload, not the retryable session, so acquired keys remain
        // visible even when Retry Assessment clears the previous session's notifications.
        var keyProgression = RoomKeyProgression.Instance;
        if (keyProgression != null)
        {
            string[] unlockedKeys = keyProgression.GetUnlockedRoomNames();
            lines.Add(unlockedKeys.Length > 0
                ? $"ROOM KEYS OWNED: {string.Join(", ", unlockedKeys)}."
                : $"No room keys acquired yet. Correct task answers contribute toward the {keyProgression.NextRoomKeyName} key.");
        }

        if (_passed)
        {
            SanityManager.Instance?.Recover(5);
            StatusEffectsOverlay.Instance?.TriggerTaskPass();
            lines.Add("Sanity recovered slightly. No new debuff was applied.");
            _continueButton.Text = "Continue";
        }
        else
        {
            if (!alreadyFinalized && !DailyTestSession.FailurePenaltyApplied)
            {
                int loss = Mathf.Clamp(18 + (DailyTestSession.Day - 1) * 4, 18, 36);
                var type = NextDebuffType();
                SanityManager.Instance?.RegisterFailedTest();
                SanityManager.Instance?.ApplyDebuff("failed_assessment", type, loss);
                DailyTestSession.MarkFailurePenaltyApplied();
                StatusEffectsOverlay.Instance?.TriggerTaskFailure(loss, type.ToString());
                lines.Add($"Sanity lost: {loss}. Debuff gained: {type}.");
                lines.Add("The next assessment will be slightly harder.");
            }
            else
            {
                lines.Add("A debuff was already applied for this assessment.");
            }

            _continueButton.Text = "Continue";
        }

        _details.Text = string.Join("\n\n", lines);
        _details.AddThemeColorOverride("font_color", new Color("e2e8f0"));
    }

    private SanityManager.DebuffType NextDebuffType()
    {
        int failedCount = SanityManager.Instance?.FailedTestCount ?? 0;
        return (SanityManager.DebuffType)(failedCount % 4);
    }

    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("0b1020"), MouseFilter = Control.MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(760, 580) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("172033"),
            BorderColor = new Color("46546e"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ContentMarginLeft = 32, ContentMarginRight = 32, ContentMarginTop = 28, ContentMarginBottom = 28
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 14);
        panel.AddChild(stack);

        var eyebrow = new Label { Text = "BEFORE THE FLATLINE  •  DAILY ASSESSMENT", HorizontalAlignment = HorizontalAlignment.Center };
        eyebrow.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        eyebrow.AddThemeFontSizeOverride("font_size", 13);
        stack.AddChild(eyebrow);

        _title = new Label { Text = "ASSESSMENT", HorizontalAlignment = HorizontalAlignment.Center };
        _title.AddThemeFontSizeOverride("font_size", 30);
        stack.AddChild(_title);

        _summary = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _summary.AddThemeFontSizeOverride("font_size", 17);
        _summary.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        stack.AddChild(_summary);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stack.AddChild(scroll);
        _details = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _details.AddThemeFontSizeOverride("font_size", 15);
        scroll.AddChild(_details);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        stack.AddChild(buttons);

        var retry = new Button { Text = "Retry Assessment", CustomMinimumSize = new Vector2(0, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        retry.Pressed += () =>
        {
            DailyTestSession.Reset();
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Challenges/SequenceChallenge.tscn");
            else
                GetTree().ChangeSceneToFile("res://Scenes/Challenges/SequenceChallenge.tscn");
        };
        buttons.AddChild(retry);

        _continueButton = new Button { Text = "Continue", CustomMinimumSize = new Vector2(0, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _continueButton.Pressed += () =>
        {
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile("res://Scenes/School/PostChallengeConversation.tscn");
            else
                GetTree().ChangeSceneToFile("res://Scenes/School/PostChallengeConversation.tscn");
        };
        buttons.AddChild(_continueButton);
    }
}

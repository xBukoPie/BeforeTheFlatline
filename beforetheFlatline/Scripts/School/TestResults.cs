using Godot;
using System.Collections.Generic;
using SchoolYardGame;

public partial class TestResults : Control
{
    private bool _passed;
    private Label _title;
    private Label _summary;
    private Label _details;
    private Button _continueButton;
    private Button _retryButton;

    public override void _Ready()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        BuildUi();

        bool alreadyFinalized = DailyTestSession.IsFinalized;
        bool tutorialBonus = DailyTestSession.IsTutorialBonus;
        _passed = DailyTestSession.FinalizeTest();
        if (!alreadyFinalized && !tutorialBonus)
        {
            if (_passed)
                AudioManager.Instance?.PlayAssessmentPassed();
            else
                AudioManager.Instance?.PlayAssessmentFailed();
        }

        if (!alreadyFinalized)
            RoomKeyProgression.Instance?.QueueAssessmentKey();

        int score = DailyTestSession.BestScore;
        int pointsReceived = DailyTestSession.BestPointsReceived;
        int currentSanity = SanityManager.Instance?.CurrentSanity ?? SanityManager.MaxSanity;

        _title.Text = tutorialBonus ? "TUTORIAL COMPLETE" : (_passed ? "ASSESSMENT PASSED" : "ASSESSMENT FAILED");
        _title.AddThemeColorOverride("font_color", tutorialBonus
            ? new Color("d7b98b")
            : (_passed ? new Color("86efac") : new Color("fca5a5")));

        _summary.Text = tutorialBonus
            ? $"BEST SCORE: {score} / {DailyTestSession.MaximumScore}\nPOINTS RECEIVED: {pointsReceived}"
            : $"BEST SCORE: {score} / {DailyTestSession.MaximumScore}\nPOINTS RECEIVED: {pointsReceived}\nPASSING SCORE: {DailyTestSession.MinimumPassingScore}";

        string returnNote = tutorialBonus
            ? "Continue to return home and begin Day 2."
            : (_passed
                ? "A passing result is secured. Retrying cannot revoke it. Sanity recovery will be applied after you return home."
                : "Any failure penalty will be applied once, after you return home. Retrying before then will not repeatedly reduce sanity.");

        _details.Text = tutorialBonus
            ? BuildSanitySummary(currentSanity)
            : BuildSanitySummary(currentSanity) + "\n\n" + returnNote;
        _details.AddThemeColorOverride("font_color", new Color("e2e8f0"));
    }

    private string BuildSanitySummary(int currentSanity)
    {
        var lines = new List<string>
        {
            $"SANITY: {currentSanity} / {SanityManager.MaxSanity}"
        };

        var penalties = new List<string>();
        if (SanityManager.Instance != null)
        {
            foreach (SanityManager.DebuffType type in System.Enum.GetValues(typeof(SanityManager.DebuffType)))
            {
                int level = SanityManager.Instance.GetAssessmentPenaltyLevel(type);
                if (level > 0)
                    penalties.Add($"{type} ({level})");
            }
        }

        lines.Add(penalties.Count == 0
            ? "ASSESSMENT DEBUFF: None"
            : $"ASSESSMENT DEBUFF: {string.Join(", ", penalties)}");

        return string.Join("\n", lines);
    }


    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect
        {
            Color = new Color("0b1020"),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(720, 460) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("172033"),
            BorderColor = new Color("46546e"),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 36,
            ContentMarginRight = 36,
            ContentMarginTop = 30,
            ContentMarginBottom = 30
        });
        center.AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 18);
        panel.AddChild(stack);

        var eyebrow = new Label
        {
            Text = "BEFORE THE FLATLINE  •  DAILY ASSESSMENT",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        eyebrow.AddThemeColorOverride("font_color", new Color("7dd3fc"));
        eyebrow.AddThemeFontSizeOverride("font_size", 13);
        stack.AddChild(eyebrow);

        _title = new Label
        {
            Text = "ASSESSMENT",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _title.AddThemeFontSizeOverride("font_size", 32);
        stack.AddChild(_title);

        _summary = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _summary.AddThemeFontSizeOverride("font_size", 22);
        _summary.AddThemeColorOverride("font_color", new Color("cbd5e1"));
        stack.AddChild(_summary);

        _details = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _details.AddThemeFontSizeOverride("font_size", 18);
        stack.AddChild(_details);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        stack.AddChild(buttons);

        _retryButton = new Button
        {
            Text = "Retry Assessment",
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _retryButton.Pressed += () =>
        {
            if (!DailyTestSession.RetryCurrentAssessment())
                return;

            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Challenges/SequenceChallenge.tscn");
            else
                GetTree().ChangeSceneToFile("res://Scenes/Challenges/SequenceChallenge.tscn");
        };
        buttons.AddChild(_retryButton);

        _continueButton = new Button
        {
            Text = "Continue",
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _continueButton.Pressed += () =>
        {
            if (FirstDayTutorialManager.Instance?.CompleteResultsAndReturnHome() == true)
                return;

            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile("res://Scenes/School/PostChallengeConversation.tscn");
            else
                GetTree().ChangeSceneToFile("res://Scenes/School/PostChallengeConversation.tscn");
        };
        buttons.AddChild(_continueButton);
    }
}

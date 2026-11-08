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

    public override void _Ready()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        BuildUi();

        bool alreadyFinalized = DailyTestSession.IsFinalized;
        _passed = DailyTestSession.FinalizeTest();
        if (!alreadyFinalized)
        {
            if (_passed)
                AudioManager.Instance?.PlayAssessmentPassed();
            else
                AudioManager.Instance?.PlayAssessmentFailed();
        }

        // Keep the existing room-key progression, but record only newly appeared keys.
        string newKeyReward = string.Empty;
        if (!alreadyFinalized)
        {
            bool keyWasAlreadyWaiting = RoomKeyProgression.Instance?.HasPendingKey == true;
            string roomName = RoomKeyProgression.Instance?.QueueAssessmentKey() ?? string.Empty;
            if (!keyWasAlreadyWaiting && !string.IsNullOrWhiteSpace(roomName))
            {
                string location = RoomKeyProgression.Instance?.GetPendingKeyLocationName() ?? "the house";
                newKeyReward = $"Key available: {roomName} (pickup in {location})";
            }
        }

        int score = DailyTestSession.BestScore;
        int pointsReceived = DailyTestSession.BestPointsReceived;
        int currentSanity = SanityManager.Instance?.CurrentSanity ?? SanityManager.MaxSanity;

        _title.Text = _passed ? "ASSESSMENT PASSED" : "ASSESSMENT FAILED";
        _title.AddThemeColorOverride("font_color", _passed
            ? new Color("86efac")
            : new Color("fca5a5"));

        _summary.Text =
            $"BEST SCORE: {score} / {DailyTestSession.MaximumScore}\n" +
            $"POINTS RECEIVED: {pointsReceived}\n" +
            $"PASSING SCORE: {DailyTestSession.MinimumPassingScore}";

        string returnNote = _passed
            ? "A passing result is secured. Retrying cannot revoke it. Sanity recovery will be applied after you return home."
            : "Any failure penalty will be applied once, after you return home. Retrying before then will not repeatedly reduce sanity.";

        _details.Text = BuildSanitySummary(currentSanity) + "\n\n" + returnNote;
        _details.AddThemeColorOverride("font_color", new Color("e2e8f0"));

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

        var retry = new Button
        {
            Text = "Retry Assessment",
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        retry.Pressed += () =>
        {
            if (!DailyTestSession.RetryCurrentAssessment())
                return;

            if (SceneTransition.Instance != null)
                SceneTransition.Instance.ChangeSceneToFile("res://Scenes/Challenges/SequenceChallenge.tscn");
            else
                GetTree().ChangeSceneToFile("res://Scenes/Challenges/SequenceChallenge.tscn");
        };
        buttons.AddChild(retry);

        _continueButton = new Button
        {
            Text = "Continue",
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
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

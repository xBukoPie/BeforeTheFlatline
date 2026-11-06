using System;
using System.Collections.Generic;
using Godot;

namespace SchoolYardGame;

public partial class ClassroomVox : Control
{
    [Export(PropertyHint.File, "*.tscn")]
    public string ChallengeScenePath { get; set; } = "res://Scenes/Challenges/SequenceChallenge.tscn";

    private const string NormalPortraitPath = "res://Assets/Characters/ProfessorVox/normalVox.png";
    private const string UpsetPortraitPath = "res://Assets/Characters/ProfessorVox/upsetVox.png";

    private static string _nextChallengeScenePath = string.Empty;

    private readonly RandomNumberGenerator _rng = new();
    private readonly HashSet<int> _upsetLineIndices = new();

    private VisualNovelScene _dialogue;
    private Texture2D _normalPortrait;
    private Texture2D _upsetPortrait;

    private sealed record LessonPack(string Topic, DialogueLine[] Lines, int[] UpsetLineIndices);

    private static readonly LessonPack[] Lessons =
    {
        new(
            "Pseudocode",
            new[]
            {
                new DialogueLine("Professor Vox", "Before the assessment, let's review pseudocode. It helps you plan a solution before worrying about a programming language.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "Pseudocode describes an algorithm in clear, ordered steps. It is written for people to understand, not for a compiler to execute.", DialogueSide.Right),
                new DialogueLine("You", "So it doesn't have to follow the exact syntax of C#?", DialogueSide.Left),
                new DialogueLine("Professor Vox", "Correct. Start with the input, describe the process, then state the output. Keep each instruction specific.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "Avoid vague steps like 'do the thing'. That tells nobody what the program is supposed to do.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "For a decision, write an IF condition, then explain what happens when it is true and what happens when it is false.", DialogueSide.Right),
                new DialogueLine("You", "And when an action needs to repeat?", DialogueSide.Left),
                new DialogueLine("Professor Vox", "Use a loop, but always know what makes it stop. A loop without a stopping condition can run forever.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "Focus on the logic: clear steps, meaningful decisions, and a result you can verify.", DialogueSide.Right)
            },
            new[] { 4, 7 }),

        new(
            "Flowcharts",
            new[]
            {
                new DialogueLine("Professor Vox", "Today's review is about flowcharts. A flowchart turns the steps of an algorithm into a visual path.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "An oval marks the start or end. A rectangle represents a process, such as calculating a total.", DialogueSide.Right),
                new DialogueLine("You", "What shape should I use when the program has to choose between two paths?", DialogueSide.Left),
                new DialogueLine("Professor Vox", "Use a diamond for a decision. Its outgoing arrows should show the possible answers, such as YES and NO.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "Do not leave a decision with unlabeled branches. The reader must know which path follows each result.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "Arrows show the direction of execution. Keep them clear and connect every step to the next one.", DialogueSide.Right),
                new DialogueLine("You", "So the diagram should be readable even without a paragraph explaining it?", DialogueSide.Left),
                new DialogueLine("Professor Vox", "Exactly. If an arrow stops halfway or a step has no route forward, the logic is incomplete.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "Read the chart from its start to its end and check every possible branch before you submit it.", DialogueSide.Right)
            },
            new[] { 4, 7 }),

        new(
            "Programming History",
            new[]
            {
                new DialogueLine("Professor Vox", "Let's take a brief look at the history of programming. The ideas behind algorithms are older than modern computers.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "In the nineteenth century, Ada Lovelace wrote notes about Charles Babbage's proposed Analytical Engine. Her notes included a published sequence of steps for calculating Bernoulli numbers.", DialogueSide.Right),
                new DialogueLine("You", "Is that why people often call her the first computer programmer?", DialogueSide.Left),
                new DialogueLine("Professor Vox", "Yes. She is often credited with publishing the earliest computer algorithm intended for a machine, even though the Analytical Engine was never completed in her lifetime.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "Later, Alan Turing's work helped establish theoretical foundations for computation and the idea of a general-purpose computing machine.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "History is more than memorizing names and dates. Pay attention to how each idea changed what people could make computers do.", DialogueSide.Right),
                new DialogueLine("You", "And programming languages came later to express those instructions?", DialogueSide.Left),
                new DialogueLine("Professor Vox", "Correct. Languages evolved to make instructions easier to write, reuse, and maintain, but algorithms remain at the center of problem-solving.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "Whatever the language, you still need to understand the steps and the result.", DialogueSide.Right)
            },
            new[] { 5 }),

        new(
            "C# Fundamentals",
            new[]
            {
                new DialogueLine("Professor Vox", "For this review, let's use C#. It is a statically typed programming language, so variables have declared types.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "For example, int stores whole numbers, string stores text, and bool stores either true or false.", DialogueSide.Right),
                new DialogueLine("You", "So the type tells the program what kind of value the variable should hold?", DialogueSide.Left),
                new DialogueLine("Professor Vox", "Exactly. You can assign a value to a variable, then use an expression to calculate or compare values.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "An int variable cannot hold a sentence. Choose a type that matches the value instead of forcing unrelated data into it.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "An if statement runs a block when its condition is true. An else block handles the other path.", DialogueSide.Right),
                new DialogueLine("You", "And braces group the statements that belong to each block?", DialogueSide.Left),
                new DialogueLine("Professor Vox", "Yes. In C#, braces define the body of a method, loop, or conditional block.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "Be careful with missing braces, parentheses, or semicolons. Small syntax mistakes can change or prevent the program from running.", DialogueSide.Right),
                new DialogueLine("Professor Vox", "Understand what each line is doing, rather than memorizing code without knowing why it works.", DialogueSide.Right)
            },
            new[] { 4, 8 })
    };

    public static void SetNextChallengeScenePath(string path) =>
        _nextChallengeScenePath = path ?? string.Empty;

    public override void _Ready()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        _dialogue = GetNode<VisualNovelScene>("%ProfessorDialogue");
        _normalPortrait = GD.Load<Texture2D>(NormalPortraitPath);
        _upsetPortrait = GD.Load<Texture2D>(UpsetPortraitPath);

        if (_normalPortrait == null)
            GD.PushWarning($"ClassroomVox: professor portrait not found: {NormalPortraitPath}");
        if (_upsetPortrait == null)
            GD.PushWarning($"ClassroomVox: professor portrait not found: {UpsetPortraitPath}");

        _rng.Randomize();
        LessonPack lesson = Lessons[_rng.RandiRange(0, Lessons.Length - 1)];
        foreach (int index in lesson.UpsetLineIndices)
            _upsetLineIndices.Add(index);

        _dialogue.SetPortrait(DialogueSide.Right, _normalPortrait);
        _dialogue.LinePresented += OnLinePresented;
        _dialogue.DialogueFinished += OnLessonFinished;

        GD.Print($"[ClassroomVox] Selected lesson topic: {lesson.Topic}");
        _dialogue.StartDialogue(lesson.Lines);
    }

    public override void _ExitTree()
    {
        if (_dialogue == null || !GodotObject.IsInstanceValid(_dialogue))
            return;

        _dialogue.LinePresented -= OnLinePresented;
        _dialogue.DialogueFinished -= OnLessonFinished;
    }

    private void OnLinePresented(int index, DialogueLine line)
    {
        if (!string.Equals(line.Speaker, "Professor Vox", StringComparison.OrdinalIgnoreCase))
            return;

        Texture2D portrait = _upsetLineIndices.Contains(index)
            ? _upsetPortrait
            : _normalPortrait;
        _dialogue.SetPortrait(DialogueSide.Right, portrait);
    }

    private void OnLessonFinished()
    {
        if (_dialogue != null && GodotObject.IsInstanceValid(_dialogue))
        {
            _dialogue.DialogueFinished -= OnLessonFinished;
            _dialogue.LinePresented -= OnLinePresented;
        }

        if (DayClock.Instance != null)
            DayClock.Instance.Running = false;

        string target = string.IsNullOrWhiteSpace(_nextChallengeScenePath)
            ? ChallengeScenePath
            : _nextChallengeScenePath;
        _nextChallengeScenePath = string.Empty;

        if (string.IsNullOrWhiteSpace(target) || !ResourceLoader.Exists(target))
        {
            GD.PushError($"ClassroomVox: assessment scene could not be loaded: {target}");
            return;
        }

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeSceneToFile(target);
        else
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, target);
    }
}

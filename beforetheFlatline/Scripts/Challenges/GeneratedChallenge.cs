using System;
using System.Collections.Generic;

public sealed class FlowchartBlockDefinition
{
    public string Type { get; }
    public string Text { get; }

    public FlowchartBlockDefinition(string type, string text)
    {
        Type = type;
        Text = text;
    }
}

public sealed class GeneratedChallenge
{
    public string Title { get; init; } = "";
    public string Prompt { get; init; } = "";
    public string Concept { get; init; } = "";
    public bool IsFlowchart { get; init; }
    public string[] PseudocodeSolution { get; init; } = Array.Empty<string>();
    public FlowchartBlockDefinition[] FlowchartSolution { get; init; } = Array.Empty<FlowchartBlockDefinition>();
}

public static class GeneratedChallengeFactory
{
    private static readonly string[] Nouns =
    {
        "notebooks", "snacks", "tickets", "folders", "markers", "stickers",
        "pandesal", "mangoes", "photocopies", "school supplies", "index cards",
        "meal vouchers", "art materials", "bottled water", "worksheets", "bookmarks",
        "pens", "flash drives", "jersey patches", "bus tickets", "project folders"
    };

    private static readonly string[] VariableNames =
    {
        "score", "points", "mark", "result", "value", "grade", "average",
        "rating", "examScore", "quizScore", "finalMark", "classScore",
        "performance", "testResult"
    };

    // These settings and terms are mixed into generated prompts to vary their vocabulary.
    private static readonly string[] SchoolSettings =
    {
        "the school fair", "the canteen", "the library", "the sports festival",
        "the art club", "a class fundraiser", "a printing shop", "the science fair",
        "the student council booth", "a classroom project", "the book sale",
        "the community clean-up", "the school supply booth", "the class field trip"
    };

    private static readonly string[] PromptClosings =
    {
        "Make sure every required output is displayed.",
        "Handle all the conditions described in the problem.",
        "Use clear input, processing, and output steps.",
        "Your algorithm must produce the result requested in the situation.",
        "Check that the steps cover the full task, not just the first calculation."
    };

    public static GeneratedChallenge Generate(string challengeId, int difficulty, Random rng)
    {
        difficulty = Math.Clamp(difficulty, 1, 5);
        if (rng == null) rng = new Random();

        string id = (challengeId ?? "sequence").Trim().ToLowerInvariant();

        if (id == "flowchart" || (id == "final" && rng.Next(2) == 1))
            return GenerateFlowchart(difficulty, rng);

        if (id == "condition")
            return GenerateCondition(difficulty, rng);

        if (id == "final")
            return GenerateFinal(difficulty, rng);

        return GenerateSequence(difficulty, rng);
    }

    private static GeneratedChallenge GenerateSequence(int level, Random rng)
    {
        if (level == 1)
        {
            int quantity = rng.Next(3, 11);
            int unitPrice = rng.Next(5, 31);
            string item = Pick(rng, Nouns);
            return Code(rng, 
                "Write pseudocode for a shopping total",
                $"A student buys {quantity} {item} at {unitPrice} pesos each. Write pseudocode that reads the quantity and unit price, calculates the total cost, and displays it.",
                "Sequence and arithmetic",
                new[] { "START", "INPUT quantity", "INPUT unitPrice", "totalCost = quantity * unitPrice", "OUTPUT totalCost", "END" });
        }

        if (level == 2)
        {
            int width = rng.Next(3, 16);
            int height = rng.Next(2, 13);
            return Code(rng, 
                "Calculate a rectangle's measurements",
                $"Write pseudocode to read a rectangle's width and height, calculate its area and perimeter, and display both. Example dimensions for testing: width {width}, height {height}.",
                "Multiple calculations",
                new[] { "START", "INPUT width", "INPUT height", "area = width * height", "perimeter = 2 * (width + height)", "OUTPUT area", "OUTPUT perimeter", "END" });
        }

        if (level == 3)
        {
            int bonus = rng.Next(2, 9);
            return Code(rng, 
                "Compute a final score",
                $"Write pseudocode that reads three quiz scores, computes their average, adds a participation bonus of {bonus}, and displays the final score.",
                "Multiple inputs and arithmetic",
                new[] { "START", "INPUT quiz1", "INPUT quiz2", "INPUT quiz3", "average = (quiz1 + quiz2 + quiz3) / 3", $"finalScore = average + {bonus}", "OUTPUT finalScore", "END" });
        }

        if (level == 4)
        {
            int tax = rng.Next(5, 13);
            string item = Pick(rng, Nouns);
            return Code(rng, 
                "Calculate a bill with a fee",
                $"Write pseudocode to read the total price of {item}, add a {tax}% service fee, and display both the fee and the final bill.",
                "Derived values and percentages",
                new[] { "START", "INPUT subtotal", $"fee = subtotal * {tax} / 100", "total = subtotal + fee", "OUTPUT fee", "OUTPUT total", "END" });
        }

        int count = rng.Next(4, 8);
        return Code(rng, 
            "Accumulate several costs",
            $"Write pseudocode that reads the cost of {count} items, adds each cost into a running total using a loop, and displays the total. Initialize the total and counter before looping.",
            "Counters and loops",
            new[] { "START", "total = 0", "count = 0", $"WHILE count < {count}", "INPUT itemCost", "total = total + itemCost", "count = count + 1", "END WHILE", "OUTPUT total", "END" });
    }

    private static GeneratedChallenge GenerateCondition(int level, Random rng)
    {
        string variable = Pick(rng, VariableNames);
        int threshold = rng.Next(45, 81);

        if (level == 1)
        {
            return Code(rng, 
                "Decide whether a mark passes",
                $"Write pseudocode that reads a student's {variable}. If it is at least {threshold}, display PASS; otherwise, display TRY AGAIN.",
                "IF / ELSE",
                new[] { "START", $"INPUT {variable}", $"IF {variable} >= {threshold} THEN", "OUTPUT PASS", "ELSE", "OUTPUT TRY AGAIN", "END IF", "END" });
        }

        if (level == 2)
        {
            int cutoff = rng.Next(70, 91);
            return Code(rng, 
                "Assign a performance label",
                $"Write pseudocode that reads a mark. Display EXCELLENT if it is at least {cutoff}, PASS if it is at least {threshold}, and TRY AGAIN otherwise.",
                "Ordered conditions",
                new[] { "START", "INPUT mark", $"IF mark >= {cutoff} THEN", "OUTPUT EXCELLENT", "ELSE", $"IF mark >= {threshold} THEN", "OUTPUT PASS", "ELSE", "OUTPUT TRY AGAIN", "END IF", "END IF", "END" });
        }

        if (level == 3)
        {
            int minimumAttendance = rng.Next(70, 91);
            int cutoff = rng.Next(50, 66);
            return Code(rng, 
                "Check eligibility",
                $"Write pseudocode that reads a mark and attendance percentage. A student qualifies only when mark is at least {cutoff} AND attendance is at least {minimumAttendance}%. Otherwise, display NOT ELIGIBLE.",
                "Compound Boolean conditions",
                new[] { "START", "INPUT mark", "INPUT attendance", $"IF mark >= {cutoff} AND attendance >= {minimumAttendance} THEN", "OUTPUT ELIGIBLE", "ELSE", "OUTPUT NOT ELIGIBLE", "END IF", "END" });
        }

        int attempts = rng.Next(3, 6);
        if (level == 4)
        {
            return Code(rng, 
                "Validate an input with limited retries",
                $"Write pseudocode that repeatedly asks for a mark until it is between 0 and 100 or {attempts} attempts have been used. Display VALID when the mark is in range; otherwise display STOP.",
                "Validation loops and conditions",
                new[] { "START", "attempts = 0", "INPUT mark", $"WHILE (mark < 0 OR mark > 100) AND attempts < {attempts}", "attempts = attempts + 1", "INPUT mark", "END WHILE", "IF mark >= 0 AND mark <= 100 THEN", "OUTPUT VALID", "ELSE", "OUTPUT STOP", "END IF", "END" });
        }

        int attendance = rng.Next(70, 91);
        int passingMark = rng.Next(50, 66);
        return Code(rng, 
            "Validate, then check eligibility",
            $"Write pseudocode that allows up to {attempts} mark-entry attempts, rejecting values outside 0–100. If a valid mark is entered, read attendance and display ELIGIBLE only when mark is at least {passingMark} AND attendance is at least {attendance}%. If attempts run out, display STOP.",
            "Bounded loop, validation, and nested conditions",
            new[] { "START", "attempts = 0", "INPUT mark", $"WHILE (mark < 0 OR mark > 100) AND attempts < {attempts}", "attempts = attempts + 1", "INPUT mark", "END WHILE", "IF mark >= 0 AND mark <= 100 THEN", "INPUT attendance", $"IF mark >= {passingMark} AND attendance >= {attendance} THEN", "OUTPUT ELIGIBLE", "ELSE", "OUTPUT NOT ELIGIBLE", "END IF", "ELSE", "OUTPUT STOP", "END IF", "END" });
    }

    private static GeneratedChallenge GenerateFinal(int level, Random rng)
    {
        if (level == 1)
        {
            int threshold = rng.Next(50, 71);
            return Code(rng, 
                "Write a pass/fail algorithm",
                $"Write pseudocode that reads a student's mark and displays PASS if the mark is at least {threshold}; otherwise, display FAIL.",
                "Input and selection",
                new[] { "START", "INPUT mark", $"IF mark >= {threshold} THEN", "OUTPUT PASS", "ELSE", "OUTPUT FAIL", "END IF", "END" });
        }

        if (level == 2)
        {
            int threshold = rng.Next(50, 71);
            int maxAttempts = rng.Next(3, 5);
            return Code(rng, 
                "Write a grading algorithm with validation",
                $"Write complete pseudocode that reads a mark, validates that it is between 0 and 100 with at most {maxAttempts} attempts, then outputs PASS if the valid mark is at least {threshold} or FAIL otherwise. Invalid marks must be rejected.",
                "Input validation and selection",
                new[] { "START", "attempts = 0", "INPUT mark", $"WHILE (mark < 0 OR mark > 100) AND attempts < {maxAttempts}", "attempts = attempts + 1", "OUTPUT INVALID MARK", "INPUT mark", "END WHILE", "IF mark >= 0 AND mark <= 100 THEN", $"IF mark >= {threshold} THEN", "OUTPUT PASS", "ELSE", "OUTPUT FAIL", "END IF", "ELSE", "OUTPUT STOP", "END IF", "END" });
        }

        if (level == 3)
        {
            int itemCount = rng.Next(3, 6);
            int threshold = rng.Next(60, 81);
            return Code(rng, 
                "Process a set of measurements",
                $"Write pseudocode to read {itemCount} measurements, accumulate their total in a loop, calculate the average, then output PASS if the average is at least {threshold} or REVIEW otherwise.",
                "Loops, accumulators, and selection",
                new[] { "START", "total = 0", "count = 0", $"WHILE count < {itemCount}", "INPUT measurement", "total = total + measurement", "count = count + 1", "END WHILE", $"average = total / {itemCount}", $"IF average >= {threshold} THEN", "OUTPUT PASS", "ELSE", "OUTPUT REVIEW", "END IF", "END" });
        }

        if (level == 4)
        {
            int maxAttempts = rng.Next(3, 6);
            int threshold = rng.Next(60, 76);
            int bonus = rng.Next(3, 11);
            return Code(rng, 
                "Validate and classify a score",
                $"Write pseudocode that lets the user enter a score between 0 and 100 with at most {maxAttempts} retries. For a valid score, add a bonus of {bonus}; display EXCELLENT if the adjusted score is at least {threshold}, PASS if it is at least 50, and FAIL otherwise. If retries run out, display STOP.",
                "Bounded retries and nested selection",
                new[] { "START", "attempts = 0", "INPUT score", $"WHILE (score < 0 OR score > 100) AND attempts < {maxAttempts}", "attempts = attempts + 1", "INPUT score", "END WHILE", "IF score >= 0 AND score <= 100 THEN", $"adjustedScore = score + {bonus}", $"IF adjustedScore >= {threshold} THEN", "OUTPUT EXCELLENT", "ELSE", "IF adjustedScore >= 50 THEN", "OUTPUT PASS", "ELSE", "OUTPUT FAIL", "END IF", "END IF", "ELSE", "OUTPUT STOP", "END IF", "END" });
        }

        int finalItemCount = rng.Next(3, 6);
        int minimumAttendance = rng.Next(75, 91);
        int passingAverage = rng.Next(60, 76);
        return Code(rng, 
            "Compute and verify final eligibility",
            $"Write pseudocode to read {finalItemCount} scores in a loop, calculate the average, then read attendance. Display ELIGIBLE only if the average is at least {passingAverage} AND attendance is at least {minimumAttendance}%. Otherwise display NOT ELIGIBLE.",
            "Loop, accumulator, input, and compound Boolean logic",
            new[] { "START", "total = 0", "count = 0", $"WHILE count < {finalItemCount}", "INPUT score", "total = total + score", "count = count + 1", "END WHILE", $"average = total / {finalItemCount}", "INPUT attendance", $"IF average >= {passingAverage} AND attendance >= {minimumAttendance} THEN", "OUTPUT ELIGIBLE", "ELSE", "OUTPUT NOT ELIGIBLE", "END IF", "END" });
    }

    private static GeneratedChallenge GenerateFlowchart(int level, Random rng)
    {
        if (level == 1)
        {
            int quantity = rng.Next(2, 9);
            int price = rng.Next(4, 26);
            string item = Pick(rng, Nouns);
            return Flow(rng, 
                "Build a total-cost flowchart",
                $"A student buys {quantity} {item} at {price} pesos each. Drag the shaped blocks into the canvas, define every block, and calculate the total from quantity and unit price.",
                "Input, process, output",
                new[]
                {
                    B("Start", "START"),
                    B("Input", "INPUT quantity"),
                    B("Input", "INPUT unitPrice"),
                    B("Process", "totalCost = quantity * unitPrice"),
                    B("Output", "OUTPUT totalCost"),
                    B("End", "END")
                });
        }

        if (level == 2)
        {
            int width = rng.Next(3, 16);
            int height = rng.Next(2, 13);
            return Flow(rng, 
                "Build a rectangle flowchart",
                $"Create a flowchart that reads width and height (example values: {width} and {height}), calculates area = width * height, then displays area.",
                "Input, calculation, output",
                new[]
                {
                    B("Start", "START"),
                    B("Input", "INPUT width"),
                    B("Input", "INPUT height"),
                    B("Process", "area = width * height"),
                    B("Output", "OUTPUT area"),
                    B("End", "END")
                });
        }

        int threshold = rng.Next(50, 76);
        if (level == 3)
        {
            return Flow(rng, 
                "Build a pass/fail flowchart",
                $"Create a flowchart that reads mark, checks whether mark >= {threshold}, and outputs PASS or FAIL on the correct branch. Define the decision condition and output labels.",
                "Decision branches",
                new[]
                {
                    B("Start", "START"),
                    B("Input", "INPUT mark"),
                    B("Decision", $"mark >= {threshold}?"),
                    B("Output", "TRUE: OUTPUT PASS"),
                    B("Output", "FALSE: OUTPUT FAIL"),
                    B("End", "END")
                });
        }

        if (level == 4)
        {
            int attendance = rng.Next(75, 91);
            return Flow(rng, 
                "Build an eligibility flowchart",
                $"Create a flowchart that reads mark and attendance. Check mark >= {threshold}, then check attendance >= {attendance}%. The student is ELIGIBLE only if both conditions are true.",
                "Compound Boolean decision",
                new[]
                {
                    B("Start", "START"),
                    B("Input", "INPUT mark"),
                    B("Input", "INPUT attendance"),
                    B("Decision", $"mark >= {threshold} AND attendance >= {attendance}?"),
                    B("Output", "TRUE: OUTPUT ELIGIBLE"),
                    B("Output", "FALSE: OUTPUT NOT ELIGIBLE"),
                    B("End", "END")
                });
        }

        int retries = rng.Next(3, 6);
        return Flow(rng, 
            "Build a bounded validation flowchart",
            $"Create a flowchart that reads mark and retries while it is outside 0–100, stopping after {retries} retries. Include a decision for validity and an output for the final result.",
            "Validation and retry logic",
            new[]
            {
                B("Start", "START"),
                B("Process", "attempts = 0"),
                B("Input", "INPUT mark"),
                B("Decision", "mark >= 0 AND mark <= 100?"),
                B("Output", "TRUE: OUTPUT VALID"),
                B("Process", "attempts = attempts + 1"),
                B("Decision", $"attempts < {retries}?"),
                B("Output", "FALSE: OUTPUT STOP"),
                B("End", "END")
            });
    }

    private const string VariableNamingGuidance =
        " Use any variable names you like.";

    private static string ComposePrompt(Random rng, string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return prompt ?? string.Empty;

        string setting = Pick(rng, SchoolSettings);
        string sentence = char.ToLowerInvariant(prompt[0]) + prompt.Substring(1);
        string framedPrompt = Pick(rng, new[]
        {
            $"At {setting}, {sentence}",
            $"In connection with {setting}, {sentence}",
            $"For a task related to {setting}, {sentence}",
            $"Scenario: {prompt}",
            $"Your task: {prompt}"
        });
        return $"{framedPrompt} {Pick(rng, PromptClosings)}";
    }

    private static GeneratedChallenge Code(Random rng, string title, string prompt, string concept, string[] lines) =>
        new GeneratedChallenge
        {
            Title = title,
            Prompt = ComposePrompt(rng, prompt) + VariableNamingGuidance,
            Concept = concept,
            IsFlowchart = false,
            PseudocodeSolution = lines
        };

    private static GeneratedChallenge Flow(Random rng, string title, string prompt, string concept, FlowchartBlockDefinition[] blocks) =>
        new GeneratedChallenge
        {
            Title = title,
            Prompt = ComposePrompt(rng, prompt) + VariableNamingGuidance,
            Concept = concept,
            IsFlowchart = true,
            FlowchartSolution = blocks
        };

    private static FlowchartBlockDefinition B(string type, string text) => new(type, text);

    private static string Pick(Random rng, string[] choices) => choices[rng.Next(choices.Length)];
}

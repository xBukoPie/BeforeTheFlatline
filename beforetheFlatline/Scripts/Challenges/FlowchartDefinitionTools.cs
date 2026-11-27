using System;
using System.Collections.Generic;

public static class FlowchartDefinitionTools
{
    private const int MaximumBlocks = 80;
    private const int MaximumDecisionDepth = 8;

    public static List<FlowchartBlockDefinition> NormalizeLegacy(
        IEnumerable<FlowchartBlockDefinition> source)
    {
        var input = source == null
            ? new List<FlowchartBlockDefinition>()
            : new List<FlowchartBlockDefinition>(source);
        var result = new List<FlowchartBlockDefinition>();

        for (int i = 0; i < input.Count; i++)
        {
            FlowchartBlockDefinition current = input[i];
            if (current == null)
                continue;

            string type = (current.Type ?? "").Trim();
            if (type.Equals("Start", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("End", StringComparison.OrdinalIgnoreCase))
                continue;

            var clone = new FlowchartBlockDefinition(type, current.Text);

            if (type.Equals("Decision", StringComparison.OrdinalIgnoreCase) &&
                current.TrueBranch.Count == 0 && current.FalseBranch.Count == 0 &&
                i + 2 < input.Count &&
                IsType(input[i + 1], "Output") && IsType(input[i + 2], "Output") &&
                TryReadLegacyBranch(input[i + 1].Text, out string firstLabel, out string firstText) &&
                TryReadLegacyBranch(input[i + 2].Text, out string secondLabel, out string secondText) &&
                firstLabel != secondLabel)
            {
                if (firstLabel == "TRUE")
                    clone.TrueBranch.Add(new FlowchartBlockDefinition("Output", firstText));
                else
                    clone.FalseBranch.Add(new FlowchartBlockDefinition("Output", firstText));

                if (secondLabel == "TRUE")
                    clone.TrueBranch.Add(new FlowchartBlockDefinition("Output", secondText));
                else
                    clone.FalseBranch.Add(new FlowchartBlockDefinition("Output", secondText));

                i += 2;
            }
            else if (type.Equals("Decision", StringComparison.OrdinalIgnoreCase))
            {
                clone.TrueBranch.AddRange(NormalizeLegacy(current.TrueBranch));
                clone.FalseBranch.AddRange(NormalizeLegacy(current.FalseBranch));
            }

            result.Add(clone);
        }

        return result;
    }

    public static List<FlowchartBlockDefinition> DeepClone(
        IEnumerable<FlowchartBlockDefinition> source)
    {
        var clone = new List<FlowchartBlockDefinition>();
        if (source == null)
            return clone;

        foreach (FlowchartBlockDefinition block in source)
        {
            if (block == null)
                continue;

            var copied = new FlowchartBlockDefinition(block.Type, block.Text);
            copied.TrueBranch.AddRange(DeepClone(block.TrueBranch));
            copied.FalseBranch.AddRange(DeepClone(block.FalseBranch));
            clone.Add(copied);
        }

        return clone;
    }

    public static bool IsComplete(IReadOnlyList<FlowchartBlockDefinition> blocks)
    {
        if (blocks == null || blocks.Count == 0)
            return false;

        int count = 0;
        bool hasInput = false;
        bool hasOutput = false;

        if (!ValidateList(blocks, 0, ref count, ref hasInput, ref hasOutput))
            return false;

        return count <= MaximumBlocks && hasInput && hasOutput;
    }

    private static bool ValidateList(
        IReadOnlyList<FlowchartBlockDefinition> blocks,
        int depth,
        ref int count,
        ref bool hasInput,
        ref bool hasOutput)
    {
        if (blocks == null || depth > MaximumDecisionDepth)
            return false;

        foreach (FlowchartBlockDefinition block in blocks)
        {
            if (block == null || ++count > MaximumBlocks)
                return false;

            string type = (block.Type ?? "").Trim();
            if (string.IsNullOrWhiteSpace(block.Text))
                return false;

            if (IsType(block, "Input"))
                hasInput = true;
            else if (IsType(block, "Output"))
                hasOutput = true;
            else if (IsType(block, "Decision"))
            {
                if (block.TrueBranch.Count == 0 || block.FalseBranch.Count == 0)
                    return false;

                if (!ValidateList(block.TrueBranch, depth + 1, ref count, ref hasInput, ref hasOutput) ||
                    !ValidateList(block.FalseBranch, depth + 1, ref count, ref hasInput, ref hasOutput))
                    return false;
            }
            else if (!type.Equals("Process", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsType(FlowchartBlockDefinition block, string type) =>
        block != null && string.Equals(block.Type, type, StringComparison.OrdinalIgnoreCase);

    private static bool TryReadLegacyBranch(string text, out string label, out string cleanText)
    {
        string value = (text ?? "").Trim();
        label = "";
        cleanText = value;

        if (value.StartsWith("TRUE:", StringComparison.OrdinalIgnoreCase))
            label = "TRUE";
        else if (value.StartsWith("FALSE:", StringComparison.OrdinalIgnoreCase))
            label = "FALSE";

        if (label.Length == 0)
            return false;

        cleanText = value.Substring(value.IndexOf(':') + 1).Trim();
        return cleanText.Length > 0;
    }
}

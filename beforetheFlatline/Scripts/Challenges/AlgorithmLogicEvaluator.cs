using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Tests submitted pseudocode by executing it against the reference algorithm using several
/// different input sets. Variable names and the exact statement layout do not need to match.
/// </summary>
public static class AlgorithmLogicEvaluator
{
    private const int MaxSteps = 12000;
    private const int MaxLoopIterations = 160;

    private static readonly double[][] TestInputs =
    {
        Cycle(3, 9, 14, 22, 34, 58, 61, 79, 88, 96, 40, 12),
        Cycle(101, -1, 101, 60, 20, 70, 85, 0, 100, 49, 51, 75),
        Cycle(0, 100, 50, 99, 1, 64, 75, 42, 60, 80, 10, 90),
        Cycle(75, 74, 65, 60, 55, 45, 50, 80, 30, 100, 0, 61),
        Cycle(2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2),
        Cycle(99, 98, 97, 96, 95, 94, 93, 92, 91, 90, 89, 88)
    };

    public static bool EvaluatePseudocode(string submitted, GeneratedChallenge challenge)
    {
        if (challenge == null || challenge.PseudocodeSolution == null ||
            challenge.PseudocodeSolution.Length == 0 || string.IsNullOrWhiteSpace(submitted))
            return false;

        string reference = string.Join("\n", challenge.PseudocodeSolution);
        return ProgramsHaveSameResults(submitted, reference);
    }

    public static bool EvaluateFlowchart(IReadOnlyList<FlowchartBlockDefinition> submitted,
        GeneratedChallenge challenge)
    {
        if (submitted == null || challenge?.FlowchartSolution == null ||
            submitted.Count < 2 || submitted.Count > 80)
            return false;

        string candidate = FlowchartToPseudocode(submitted);
        string reference = FlowchartToPseudocode(challenge.FlowchartSolution);
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(reference))
            return false;

        // The canvas is an ordered block builder rather than a free-connection graph. Execute
        // both ordered algorithms instead of comparing every label to an exact template.
        return ProgramsHaveSameResults(candidate, reference);
    }

    private static double[] Cycle(params double[] values)
    {
        var data = new double[96];
        for (int i = 0; i < data.Length; i++)
            data[i] = values[i % values.Length];
        return data;
    }

    private static bool ProgramsHaveSameResults(string submitted, string reference)
    {
        try
        {
            var candidateProgram = ParseProgram(submitted);
            var referenceProgram = ParseProgram(reference);

            foreach (double[] inputs in TestInputs)
            {
                if (!TryRun(referenceProgram, inputs, out var expected) ||
                    !TryRun(candidateProgram, inputs, out var actual) ||
                    !OutputsEqual(actual, expected))
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Algorithm answer could not be evaluated: {ex.Message}");
            return false;
        }
    }

    private static bool OutputsEqual(List<object> actual, List<object> expected)
    {
        if (actual.Count != expected.Count)
            return false;

        for (int i = 0; i < actual.Count; i++)
        {
            object a = actual[i];
            object b = expected[i];
            if (IsNumber(a) && IsNumber(b))
            {
                if (Math.Abs(Convert.ToDouble(a, CultureInfo.InvariantCulture) -
                             Convert.ToDouble(b, CultureInfo.InvariantCulture)) > 0.0001)
                    return false;
            }
            else if (a is bool ab && b is bool bb)
            {
                if (ab != bb) return false;
            }
            else if (!string.Equals(Convert.ToString(a, CultureInfo.InvariantCulture)?.Trim(),
                                    Convert.ToString(b, CultureInfo.InvariantCulture)?.Trim(),
                                    StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static bool IsNumber(object value) =>
        value is byte || value is short || value is int || value is long ||
        value is float || value is double || value is decimal;

    private static bool TryRun(List<Statement> program, double[] inputs, out List<object> outputs)
    {
        outputs = new List<object>();
        var context = new ExecutionContext(inputs, outputs);
        try
        {
            ExecuteBlock(program, context);
            return !context.Failed;
        }
        catch
        {
            return false;
        }
    }

    private static List<Statement> ParseProgram(string source)
    {
        var lines = new List<string>();
        foreach (string raw in (source ?? "").Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//") || line.StartsWith("#"))
                continue;

            line = Regex.Replace(line, @"\s+#.*$", "");
            line = Regex.Replace(line, @"\s+//.*$", "");
            line = line.Replace("←", "=").Replace("≤", "<=").Replace("≥", ">=")
                       .Replace("≠", "!=").Trim().TrimEnd(';').Trim();

            // Accept common natural-language comparison and I/O aliases.
            line = Regex.Replace(line, @"\bis\s+greater\s+than\s+or\s+equal\s+to\b|\bat\s+least\b", ">=", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"\bis\s+less\s+than\s+or\s+equal\s+to\b|\bat\s+most\b", "<=", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"\bis\s+greater\s+than\b|\bgreater\s+than\b", ">", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"\bis\s+less\s+than\b|\bless\s+than\b", "<", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"\bis\s+equal\s+to\b|\bequals\b", "==", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"\b(begin program|begin)\b", "START", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"\b(stop program|stop|finish)\b", "END", RegexOptions.IgnoreCase);
            lines.Add(line);
        }

        int index = 0;
        return ParseBlock(lines, ref index, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static List<Statement> ParseBlock(List<string> lines, ref int index, HashSet<string> stopWords)
    {
        var statements = new List<Statement>();
        while (index < lines.Count)
        {
            string line = lines[index].Trim();
            string upper = line.ToUpperInvariant();
            if (stopWords.Contains(upper) || upper == "ELSE" || upper == "END IF" ||
                upper == "ENDIF" || upper == "END WHILE" || upper == "ENDWHILE" ||
                upper.StartsWith("NEXT ") || upper == "NEXT" || upper == "END FOR" || upper == "ENDFOR")
                break;

            if (upper == "END" || upper == "STOP")
            {
                index++;
                break;
            }

            if (upper.StartsWith("IF ", StringComparison.Ordinal) || upper.StartsWith("IF(", StringComparison.Ordinal))
            {
                string condition = Regex.Replace(line.Substring(2).Trim(), @"\s+THEN\s*$", "", RegexOptions.IgnoreCase).Trim();
                index++;
                var trueBranch = ParseBlock(lines, ref index, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ELSE", "END IF", "ENDIF" });
                var falseBranch = new List<Statement>();
                if (index < lines.Count && lines[index].Trim().Equals("ELSE", StringComparison.OrdinalIgnoreCase))
                {
                    index++;
                    falseBranch = ParseBlock(lines, ref index, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "END IF", "ENDIF" });
                }
                if (index < lines.Count && (lines[index].Trim().Equals("END IF", StringComparison.OrdinalIgnoreCase) ||
                                             lines[index].Trim().Equals("ENDIF", StringComparison.OrdinalIgnoreCase)))
                    index++;
                statements.Add(new IfStatement(condition, trueBranch, falseBranch));
                continue;
            }

            if (upper.StartsWith("WHILE ", StringComparison.Ordinal) || upper.StartsWith("WHILE(", StringComparison.Ordinal))
            {
                string condition = line.Substring(5).Trim();
                index++;
                var body = ParseBlock(lines, ref index, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "END WHILE", "ENDWHILE" });
                if (index < lines.Count) index++;
                statements.Add(new WhileStatement(condition, body));
                continue;
            }

            var forMatch = Regex.Match(line, @"^FOR\s+([A-Za-z_]\w*)\s*=\s*(.*?)\s+TO\s+(.*?)(?:\s+STEP\s+(.*))?$", RegexOptions.IgnoreCase);
            if (forMatch.Success)
            {
                index++;
                var body = ParseBlock(lines, ref index, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NEXT", "END FOR", "ENDFOR" });
                if (index < lines.Count) index++;
                statements.Add(new ForStatement(forMatch.Groups[1].Value, forMatch.Groups[2].Value,
                    forMatch.Groups[3].Value, forMatch.Groups[4].Success ? forMatch.Groups[4].Value : "1", body));
                continue;
            }

            index++;
            string normalizedUpper = upper;
            if (normalizedUpper == "ELSE" || normalizedUpper.StartsWith("END IF") ||
                normalizedUpper.StartsWith("END WHILE") || normalizedUpper.StartsWith("END FOR"))
                continue;
            statements.Add(new SimpleStatement(line));
        }

        return statements;
    }

    private static void ExecuteBlock(List<Statement> statements, ExecutionContext context)
    {
        foreach (Statement statement in statements)
        {
            if (++context.Steps > MaxSteps)
            {
                context.Failed = true;
                return;
            }

            statement.Execute(context);
            if (context.Failed)
                return;
        }
    }

    private static string FlowchartToPseudocode(IReadOnlyList<FlowchartBlockDefinition> blocks)
    {
        if (blocks == null || blocks.Count < 2 ||
            !blocks[0].Type.Equals("Start", StringComparison.OrdinalIgnoreCase) ||
            !blocks[blocks.Count - 1].Type.Equals("End", StringComparison.OrdinalIgnoreCase))
            return "";

        var lines = new List<string> { "START" };
        int openDecision = 0;
        bool inElse = false;

        for (int i = 1; i < blocks.Count - 1; i++)
        {
            string type = blocks[i].Type ?? "";
            string text = (blocks[i].Text ?? "").Trim();
            if (type.Equals("Input", StringComparison.OrdinalIgnoreCase))
            {
                text = Regex.Replace(text, @"^(INPUT|READ|GET|ACCEPT)\s+", "", RegexOptions.IgnoreCase);
                foreach (string variable in text.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0))
                    lines.Add("INPUT " + variable);
            }
            else if (type.Equals("Process", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(text);
            }
            else if (type.Equals("Output", StringComparison.OrdinalIgnoreCase))
            {
                if (text.StartsWith("TRUE:", StringComparison.OrdinalIgnoreCase))
                    text = text.Substring(5).Trim();
                else if (text.StartsWith("FALSE:", StringComparison.OrdinalIgnoreCase))
                {
                    if (openDecision > 0 && !inElse)
                    {
                        lines.Add("ELSE");
                        inElse = true;
                    }
                    text = text.Substring(6).Trim();
                }
                if (text.Length > 0 && !Regex.IsMatch(text, @"^(OUTPUT|PRINT|DISPLAY|WRITE)\b", RegexOptions.IgnoreCase))
                    text = "OUTPUT " + text;
                if (text.Length > 0) lines.Add(text);
                // A conventional true/false pair closes after the false output.
                if (inElse && openDecision > 0)
                {
                    lines.Add("END IF");
                    openDecision--;
                    inElse = false;
                }
            }
            else if (type.Equals("Decision", StringComparison.OrdinalIgnoreCase))
            {
                string condition = text.Trim().TrimEnd('?');
                lines.Add("IF " + condition + " THEN");
                openDecision++;
                inElse = false;
            }
            else
            {
                return "";
            }
        }

        while (openDecision-- > 0)
        {
            if (!inElse) lines.Add("ELSE");
            lines.Add("END IF");
            inElse = false;
        }
        lines.Add("END");
        return string.Join("\n", lines);
    }

    private abstract class Statement
    {
        public abstract void Execute(ExecutionContext context);
    }

    private sealed class SimpleStatement : Statement
    {
        private readonly string _line;
        public SimpleStatement(string line) => _line = line;

        public override void Execute(ExecutionContext c)
        {
            string line = _line.Trim();
            if (line.Length == 0 || line.Equals("START", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("BEGIN", StringComparison.OrdinalIgnoreCase))
                return;

            var input = Regex.Match(line, @"^(INPUT|READ|GET|ACCEPT)\s+(.+)$", RegexOptions.IgnoreCase);
            if (input.Success)
            {
                string inputList = Regex.Replace(input.Groups[2].Value,
                    @"^\s*(""[^""]*""|'[^']*')\s*,?\s*", "");
                foreach (string part in inputList.Split(','))
                {
                    string variable = part.Trim();
                    if (variable.Length == 0) { c.Failed = true; return; }
                    if (c.InputIndex >= c.Inputs.Length) { c.Failed = true; return; }
                    c.Variables[variable] = c.Inputs[c.InputIndex++];
                }
                return;
            }

            var output = Regex.Match(line, @"^(OUTPUT|PRINT|DISPLAY|WRITE)\s+(.+)$", RegexOptions.IgnoreCase);
            if (output.Success)
            {
                string expression = output.Groups[2].Value.Trim();
                if (expression.StartsWith("TO ", StringComparison.OrdinalIgnoreCase))
                    expression = expression.Substring(3).Trim();

                // Multi-word, unquoted labels (for example OUTPUT NOT ELIGIBLE) are common in
                // pseudocode. Treat them as a literal only when none of the words is a variable.
                var labelWords = Regex.Matches(expression, @"[A-Za-z_]\w*").Select(m => m.Value).ToArray();
                if (labelWords.Length > 1 &&
                    Regex.IsMatch(expression, @"^[A-Za-z_]\w*(?:\s+[A-Za-z_]\w*)+$") &&
                    labelWords.All(word => !c.Variables.ContainsKey(word)))
                {
                    c.Outputs.Add(expression.Trim());
                    return;
                }

                c.Outputs.Add(ExpressionEvaluator.Evaluate(expression, c.Variables, outputMode: true));
                return;
            }

            var set = Regex.Match(line, @"^SET\s+([A-Za-z_]\w*)\s+TO\s+(.+)$", RegexOptions.IgnoreCase);
            if (set.Success)
            {
                c.Variables[set.Groups[1].Value] = ExpressionEvaluator.Evaluate(set.Groups[2].Value, c.Variables);
                return;
            }

            var assignment = Regex.Match(line, @"^([A-Za-z_]\w*)\s*(?::=|←|=(?!=))\s*(.+)$");
            if (assignment.Success)
            {
                c.Variables[assignment.Groups[1].Value] = ExpressionEvaluator.Evaluate(assignment.Groups[2].Value, c.Variables);
                return;
            }

            // Accept a harmless standalone END marker and reject unknown executable syntax.
            if (line.Equals("END", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("STOP", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("COMMENT ", StringComparison.OrdinalIgnoreCase))
                return;

            c.Failed = true;
        }
    }

    private sealed class IfStatement : Statement
    {
        private readonly string _condition;
        private readonly List<Statement> _yes;
        private readonly List<Statement> _no;
        public IfStatement(string condition, List<Statement> yes, List<Statement> no)
        {
            _condition = condition;
            _yes = yes;
            _no = no;
        }
        public override void Execute(ExecutionContext c) =>
            ExecuteBlock(ExpressionEvaluator.ToBoolean(ExpressionEvaluator.Evaluate(_condition, c.Variables)) ? _yes : _no, c);
    }

    private sealed class WhileStatement : Statement
    {
        private readonly string _condition;
        private readonly List<Statement> _body;
        public WhileStatement(string condition, List<Statement> body) { _condition = condition; _body = body; }
        public override void Execute(ExecutionContext c)
        {
            int iterations = 0;
            while (!c.Failed && ExpressionEvaluator.ToBoolean(ExpressionEvaluator.Evaluate(_condition, c.Variables)))
            {
                if (++iterations > MaxLoopIterations) { c.Failed = true; return; }
                ExecuteBlock(_body, c);
            }
        }
    }

    private sealed class ForStatement : Statement
    {
        private readonly string _variable, _start, _end, _step;
        private readonly List<Statement> _body;
        public ForStatement(string variable, string start, string end, string step, List<Statement> body)
        {
            _variable = variable; _start = start; _end = end; _step = step; _body = body;
        }
        public override void Execute(ExecutionContext c)
        {
            double start = ExpressionEvaluator.ToNumber(ExpressionEvaluator.Evaluate(_start, c.Variables));
            double end = ExpressionEvaluator.ToNumber(ExpressionEvaluator.Evaluate(_end, c.Variables));
            double step = ExpressionEvaluator.ToNumber(ExpressionEvaluator.Evaluate(_step, c.Variables));
            if (step == 0) { c.Failed = true; return; }
            int iterations = 0;
            for (double value = start; (step > 0 ? value <= end : value >= end) && !c.Failed; value += step)
            {
                if (++iterations > MaxLoopIterations) { c.Failed = true; return; }
                c.Variables[_variable] = value;
                ExecuteBlock(_body, c);
            }
        }
    }

    private sealed class ExecutionContext
    {
        public readonly Dictionary<string, object> Variables = new(StringComparer.OrdinalIgnoreCase);
        public readonly double[] Inputs;
        public readonly List<object> Outputs;
        public int InputIndex;
        public int Steps;
        public bool Failed;
        public ExecutionContext(double[] inputs, List<object> outputs) { Inputs = inputs; Outputs = outputs; }
    }

    private static class ExpressionEvaluator
    {
        public static object Evaluate(string expression, Dictionary<string, object> variables, bool outputMode = false)
        {
            var parser = new Parser(expression, variables, outputMode);
            object value = parser.ParseOr();
            if (!parser.AtEnd)
                throw new FormatException("Unexpected token in expression.");
            return value;
        }

        public static bool ToBoolean(object value)
        {
            if (value is bool boolean) return boolean;
            if (IsNumber(value)) return Math.Abs(Convert.ToDouble(value, CultureInfo.InvariantCulture)) > 0.0000001;
            if (value is string s)
                return s.Equals("true", StringComparison.OrdinalIgnoreCase);
            return false;
        }

        public static double ToNumber(object value)
        {
            if (IsNumber(value)) return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (value is bool b) return b ? 1 : 0;
            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any,
                CultureInfo.InvariantCulture, out double result)) return result;
            throw new FormatException("A numeric expression was expected.");
        }

        private sealed class Parser
        {
            private static readonly Regex TokenPattern = new(
                @"\s*(>=|<=|==|!=|<>|&&|\|\||:=|[()+\-*/%><=!]|(?:\d+(?:\.\d*)?|\.\d+)|[A-Za-z_][A-Za-z0-9_]*|""(?:\\.|[^""])*""|'(?:\\.|[^'])*')",
                RegexOptions.Compiled);
            private readonly List<string> _tokens = new();
            private readonly Dictionary<string, object> _variables;
            private readonly bool _outputMode;
            private int _index;
            public bool AtEnd => _index >= _tokens.Count;

            public Parser(string expression, Dictionary<string, object> variables, bool outputMode)
            {
                _variables = variables;
                _outputMode = outputMode;
                string text = (expression ?? "").Trim();
                int position = 0;
                while (position < text.Length)
                {
                    Match m = TokenPattern.Match(text, position);
                    if (!m.Success || m.Index != position)
                        throw new FormatException("Unrecognized expression syntax.");
                    _tokens.Add(m.Groups[1].Value);
                    position = m.Index + m.Length;
                }
                if (_tokens.Count == 0) throw new FormatException("Empty expression.");
            }

            public object ParseOr()
            {
                object left = ParseAnd();
                while (TakeWord("OR") || Take("||"))
                {
                    object right = ParseAnd();
                    left = ToBoolean(left) || ToBoolean(right);
                }
                return left;
            }

            private object ParseAnd()
            {
                object left = ParseComparison();
                while (TakeWord("AND") || Take("&&"))
                {
                    object right = ParseComparison();
                    left = ToBoolean(left) && ToBoolean(right);
                }
                return left;
            }

            private object ParseComparison()
            {
                object left = ParseAdd();
                if (!AtEnd)
                {
                    string op = Peek();
                    if (op == ">=" || op == "<=" || op == ">" || op == "<" ||
                        op == "==" || op == "=" || op == "!=" || op == "<>")
                    {
                        _index++;
                        object right = ParseAdd();
                        if (IsNumber(left) && IsNumber(right))
                        {
                            double a = ToNumber(left), b = ToNumber(right);
                            return op switch
                            {
                                ">=" => a >= b, "<=" => a <= b, ">" => a > b, "<" => a < b,
                                "!=" or "<>" => Math.Abs(a - b) > 0.0000001,
                                _ => Math.Abs(a - b) <= 0.0000001
                            };
                        }
                        string ls = Convert.ToString(left, CultureInfo.InvariantCulture) ?? "";
                        string rs = Convert.ToString(right, CultureInfo.InvariantCulture) ?? "";
                        int compare = string.Compare(ls, rs, StringComparison.OrdinalIgnoreCase);
                        return op switch
                        {
                            ">=" => compare >= 0, "<=" => compare <= 0, ">" => compare > 0, "<" => compare < 0,
                            "!=" or "<>" => compare != 0, _ => compare == 0
                        };
                    }
                }
                return left;
            }

            private object ParseAdd()
            {
                object left = ParseMultiply();
                while (!AtEnd && (Peek() == "+" || Peek() == "-"))
                {
                    string op = _tokens[_index++];
                    object right = ParseMultiply();
                    if (op == "+" && (left is string || right is string))
                        left = Convert.ToString(left, CultureInfo.InvariantCulture) + Convert.ToString(right, CultureInfo.InvariantCulture);
                    else
                        left = op == "+" ? ToNumber(left) + ToNumber(right) : ToNumber(left) - ToNumber(right);
                }
                return left;
            }

            private object ParseMultiply()
            {
                object left = ParseUnary();
                while (!AtEnd && (Peek() == "*" || Peek() == "/" || Peek() == "%"))
                {
                    string op = _tokens[_index++];
                    double right = ToNumber(ParseUnary());
                    double lhs = ToNumber(left);
                    if ((op == "/" || op == "%") && Math.Abs(right) < 0.0000001)
                        throw new DivideByZeroException();
                    left = op == "*" ? lhs * right : op == "/" ? lhs / right : lhs % right;
                }
                return left;
            }

            private object ParseUnary()
            {
                if (Take("-")) return -ToNumber(ParseUnary());
                if (Take("+")) return ToNumber(ParseUnary());
                if (Take("!") || TakeWord("NOT")) return !ToBoolean(ParseUnary());
                return ParsePrimary();
            }

            private object ParsePrimary()
            {
                if (Take("("))
                {
                    object nested = ParseOr();
                    if (!Take(")")) throw new FormatException("Missing closing parenthesis.");
                    return nested;
                }
                if (AtEnd) throw new FormatException("Missing expression value.");
                string token = _tokens[_index++];
                if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double number))
                    return number;

                if ((token.StartsWith("\"") && token.EndsWith("\"")) ||
                    (token.StartsWith("'") && token.EndsWith("'")))
                    return token.Substring(1, token.Length - 2);

                if (token.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return true;
                if (token.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return false;
                if (_variables.TryGetValue(token, out object value)) return value;
                if (_outputMode) return token;
                throw new FormatException($"Unknown variable '{token}'.");
            }

            private bool Take(string token)
            {
                if (!AtEnd && Peek() == token) { _index++; return true; }
                return false;
            }
            private bool TakeWord(string word)
            {
                if (!AtEnd && Peek().Equals(word, StringComparison.OrdinalIgnoreCase)) { _index++; return true; }
                return false;
            }
            private string Peek() => _tokens[_index];
        }
    }
}

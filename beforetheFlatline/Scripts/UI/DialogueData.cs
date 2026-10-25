namespace SchoolYardGame;

/// <summary>Which side of the screen the speaker stands on.</summary>
public enum DialogueSide
{
    Left,
    Right
}

/// <summary>
/// A selectable reply. <paramref name="Goto"/> is the index of the line to jump to
/// (use -1 to just continue to the next line).
/// </summary>
public sealed record DialogueChoice(string Label, int Goto = -1);

/// <summary>
/// One line of dialogue.
/// <list type="bullet">
/// <item><description><b>Text</b> may contain BBCode, e.g. "[shake]...[/shake]" or "[i]...[/i]".</description></item>
/// <item><description><b>Choices</b>, when set, are shown once the text finishes typing.</description></item>
/// <item><description><b>Next</b> overrides which line follows (-1 = the next one in the list).</description></item>
/// </list>
/// </summary>
public sealed record DialogueLine(
    string Speaker,
    string Text,
    DialogueSide Side,
    DialogueChoice[] Choices = null,
    int Next = -1);

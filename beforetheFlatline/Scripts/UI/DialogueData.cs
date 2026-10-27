namespace SchoolYardGame;

public enum DialogueSide
{
    Left,
    Right
}

// /// A selectable reply.
public sealed record DialogueChoice(string Label, int Goto = -1);

// /// One line of dialogue.
public sealed record DialogueLine(
    string Speaker,
    string Text,
    DialogueSide Side,
    DialogueChoice[] Choices = null,
    int Next = -1);

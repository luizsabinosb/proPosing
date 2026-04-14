using System.Linq;

namespace ProPosing.Avalonia.Models;

public sealed record PoseFeedback(
    string Status,   // "correct" | "adjustment_needed" | "incorrect" | "no_detection"
    string Message,
    IReadOnlyList<string> Hints)
{
    public static readonly PoseFeedback NoDetection =
        new("no_detection", "Aguardando detecção...", []);

    public static PoseFeedback FromErrors(IReadOnlyList<string> errors, string correctMessage)
    {
        if (errors.Count == 0)
            return new("correct", correctMessage, []);
        if (errors.Count == 1)
            return new("adjustment_needed", errors[0], []);
        // First error is the main message; remaining are secondary corrections.
        return new("incorrect", errors[0], errors.Skip(1).ToList());
    }
}

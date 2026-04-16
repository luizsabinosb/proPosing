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
        // All errors go to hints; message stays empty so all items render uniformly.
        return new(errors.Count == 1 ? "adjustment_needed" : "incorrect", string.Empty, errors.ToList());
    }
}

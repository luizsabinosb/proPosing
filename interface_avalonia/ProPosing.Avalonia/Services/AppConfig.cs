using System.IO;

namespace ProPosing.Avalonia.Services;

public sealed class AppConfig
{
    // No Windows o launcher padrão é "python"; em macOS/Linux, "python3".
    private static string DefaultPythonPath =>
        OperatingSystem.IsWindows() ? "python" : "python3";

    public int CameraIndex { get; init; } = 0;
    public int InferenceStride { get; init; } = 3;
    public int TargetFps { get; init; } = 30;
    public string PythonPath { get; init; } = DefaultPythonPath;

    /// <summary>
    /// Kiosk mode (máquina de academia, sem operador): pula o login e abre em
    /// fullscreen. Ativado por um arquivo marcador "kiosk.mode" ao lado do
    /// executável (sobrevive a auto-start sem precisar de env vars) ou pela
    /// variável PROPOSING_KIOSK=1 (conveniente em desenvolvimento).
    /// </summary>
    public bool KioskMode { get; init; }

    public static AppConfig LoadFromEnvironment()
    {
        return new AppConfig
        {
            CameraIndex = ParseInt("CAMERA_INDEX", 0),
            InferenceStride = ParseInt("INFERENCE_STRIDE", 3),
            TargetFps = ParseInt("TARGET_FPS", 30),
            PythonPath = Environment.GetEnvironmentVariable("PYTHON_PATH") ?? DefaultPythonPath,
            KioskMode = Environment.GetEnvironmentVariable("PROPOSING_KIOSK") == "1"
                        || File.Exists(Path.Combine(AppContext.BaseDirectory, "kiosk.mode")),
        };
    }

    private static int ParseInt(string key, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}

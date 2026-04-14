namespace ProPosing.Avalonia.Services;

public sealed class AppConfig
{
    public int CameraIndex { get; init; } = 0;
    public int InferenceStride { get; init; } = 3;
    public int TargetFps { get; init; } = 30;
    public string PythonPath { get; init; } = "python3";

    public static AppConfig LoadFromEnvironment()
    {
        return new AppConfig
        {
            CameraIndex = ParseInt("CAMERA_INDEX", 0),
            InferenceStride = ParseInt("INFERENCE_STRIDE", 3),
            TargetFps = ParseInt("TARGET_FPS", 30),
            PythonPath = Environment.GetEnvironmentVariable("PYTHON_PATH") ?? "python3",
        };
    }

    private static int ParseInt(string key, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}

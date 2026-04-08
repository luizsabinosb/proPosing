namespace ProPosing.Avalonia.Services;

public sealed class AppConfig
{
    public string ApiHost { get; init; } = "localhost";
    public int ApiPort { get; init; } = 8000;
    public int CameraIndex { get; init; } = 0;
    public int InferenceStride { get; init; } = 3;
    public int RequestTimeoutSeconds { get; init; } = 10;
    public int TargetFps { get; init; } = 30;

    public string ApiBaseUrl => $"http://{ApiHost}:{ApiPort}";

    public static AppConfig LoadFromEnvironment()
    {
        return new AppConfig
        {
            ApiHost = Environment.GetEnvironmentVariable("API_HOST") ?? "localhost",
            ApiPort = ParseInt("API_PORT", 8000),
            CameraIndex = ParseInt("CAMERA_INDEX", 0),
            InferenceStride = ParseInt("INFERENCE_STRIDE", 3),
            RequestTimeoutSeconds = ParseInt("REQUEST_TIMEOUT_SECONDS", 10),
            TargetFps = ParseInt("TARGET_FPS", 30),
        };
    }

    private static int ParseInt(string key, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}

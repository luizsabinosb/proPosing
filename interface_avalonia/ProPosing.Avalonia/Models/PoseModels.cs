using System.Text.Json.Serialization;

namespace ProPosing.Avalonia.Models;

public sealed class LandmarkPoint
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("z")]
    public double Z { get; set; }

    [JsonPropertyName("visibility")]
    public double? Visibility { get; set; }
}

public sealed class PoseEvaluateRequest
{
    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("pose_mode")]
    public string PoseMode { get; set; } = "enquadramento";

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}

public sealed class PoseEvaluateResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("pose_quality")]
    public string? PoseQuality { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "no_detection";

    [JsonPropertyName("landmarks")]
    public List<LandmarkPoint> Landmarks { get; set; } = [];

    [JsonPropertyName("annotated_image")]
    public string? AnnotatedImage { get; set; }

    [JsonPropertyName("processing_time_ms")]
    public int ProcessingTimeMs { get; set; }

    [JsonPropertyName("image_width")]
    public int? ImageWidth { get; set; }

    [JsonPropertyName("image_height")]
    public int? ImageHeight { get; set; }
}

public sealed class PoseSelectRequest
{
    [JsonPropertyName("pose_mode")]
    public string PoseMode { get; set; } = "enquadramento";

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}

public sealed class PoseSelectResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("pose_mode")]
    public string PoseMode { get; set; } = string.Empty;

    [JsonPropertyName("pose_name")]
    public string PoseName { get; set; } = string.Empty;
}

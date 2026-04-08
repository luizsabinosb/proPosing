using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using ProPosing.Avalonia.Models;

namespace ProPosing.Avalonia.Services;

public sealed class PoseApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = null,
    };

    public PoseApiClient(AppConfig config)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds),
        };
    }

    public string BaseUrl => _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "http://localhost:8000";

    public async Task<bool> HealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<PoseEvaluateResponse> EvaluatePoseAsync(
        string imageBase64,
        string poseMode,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new PoseEvaluateRequest
        {
            Image = imageBase64,
            PoseMode = poseMode,
            SessionId = sessionId,
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/pose/evaluate",
            payload,
            _serializerOptions,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Erro ao avaliar pose: status {(int)response.StatusCode} - {body}");
        }

        var parsed = JsonSerializer.Deserialize<PoseEvaluateResponse>(body, _serializerOptions);
        if (parsed is null)
        {
            throw new InvalidOperationException("Resposta de avaliação inválida.");
        }

        return parsed;
    }

    public async Task<PoseSelectResponse> SelectPoseAsync(
        string poseMode,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new PoseSelectRequest
        {
            PoseMode = poseMode,
            SessionId = sessionId,
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/pose/select",
            payload,
            _serializerOptions,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Erro ao selecionar pose: status {(int)response.StatusCode} - {body}");
        }

        var parsed = JsonSerializer.Deserialize<PoseSelectResponse>(body, _serializerOptions);
        if (parsed is null)
        {
            throw new InvalidOperationException("Resposta de seleção inválida.");
        }

        return parsed;
    }
}

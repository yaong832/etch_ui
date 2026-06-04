using System.Net.Http;
using System.Threading;
using System.Text;
using System.Text.Json;

namespace etch_ui.Services;

public sealed class EtchFlaskClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public string BaseUrl { get; set; } = "http://127.0.0.1:5000";

    public async Task<bool> TryHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await _http
                .GetAsync($"{BaseUrl.TrimEnd('/')}/api/sensors", cancellationToken)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryPostEtchSensorDataAsync(EtchTelemetryPayload payload)
    {
        try
        {
            string json = JsonSerializer.Serialize(payload, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _http.PostAsync($"{BaseUrl.TrimEnd('/')}/api/etch/sensor-data", content)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<EtchAiDiagnosis?> TryGetAiLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await _http
                .GetAsync($"{BaseUrl.TrimEnd('/')}/api/etch/ai/latest", cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<EtchAiDiagnosis>(json, JsonReadOptions);
        }
        catch
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Dispose() => _http.Dispose();
}

public sealed class EtchAiDiagnosis
{
    public bool Success { get; set; }
    public double AnomalyScore { get; set; }
    public string? SuggestedAction { get; set; }
    public string? Note { get; set; }
    public bool Stub { get; set; }
    public string? UpdatedAt { get; set; }
}

public sealed class EtchTelemetryPayload
{
    public int EquipmentId { get; set; } = 1;
    public bool PowerOn { get; set; } = true;
    public bool Connected { get; set; }
    /// <summary>EtherCAT 실측 샘플이 있을 때만 true — Flask·웹에 센서 수치 노출.</summary>
    public bool SensorsLive { get; set; }
    /// <summary>live | demo | offline — Flask에서 실가공 이력과 데모 이력 분리 저장.</summary>
    public string DataSource { get; set; } = "offline";
    /// <summary>시뮬 허용 + TwinCAT 미사용 데모 모드.</summary>
    public bool BenchMode { get; set; }
    public string LastUpdate { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    /// <summary>챔버/LL 압력 (mTorr). JSON 필드명은 pressure 유지.</summary>
    public double Pressure { get; set; }
    public double Vibration { get; set; }
    public bool AccessSafe { get; set; }
    public string EquipmentState { get; set; } = string.Empty;
    public string? AlarmCode { get; set; }
    public bool InterlockOk { get; set; }
    public string? Username { get; set; }
    /// <summary>모듈별 상태 (LP · BM · TM · PM · EFEM).</summary>
    public List<ModuleTelemetryModule>? Modules { get; set; }
}

/// <summary>Flask JSON용 모듈 상태 (camelCase).</summary>
public sealed class ModuleTelemetryModule
{
    public string Id { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool? DoorClosed { get; set; }
    public bool? HasWafer { get; set; }
    public string? Detail { get; set; }
}

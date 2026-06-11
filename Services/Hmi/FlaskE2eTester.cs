using System.Net.Http;
using System.Text.Json;
using etch_ui.Services.Simulation;

namespace etch_ui.Services.Hmi;

/// <summary>
/// 실행 중인 etchflask에 WPF와 동일 계약으로 HTTP E2E 검증 (A1~A5 + 이벤트·이력).
/// </summary>
public static class FlaskE2eTester
{
    private static readonly JsonSerializerOptions JsonRead = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public sealed class Result
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? Report { get; init; }
        public bool AiReady { get; init; }
        public string? AiEngine { get; init; }
    }

    public static Result Run(string baseUrl, int simTicks = 80, bool requireMlReady = false)
    {
        string url = baseUrl.TrimEnd('/');
        var log = new List<string>();

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var flask = new EtchFlaskClient { BaseUrl = url };

            if (!flask.TryHealthCheckAsync().GetAwaiter().GetResult())
            {
                return Fail($"flask unreachable: {url}/api/sensors");
            }

            log.Add("A1 GET /api/sensors → 200");

            EtchTelemetryPayload demoPayload = BuildDemoPayload(simTicks);
            if (!flask.TryPostEtchSensorDataAsync(demoPayload).GetAwaiter().GetResult())
            {
                return Fail("POST /api/etch/sensor-data failed");
            }

            log.Add("POST /api/etch/sensor-data (demo RUNNING) → OK");

            string sensorsJson = http.GetStringAsync($"{url}/api/sensors").GetAwaiter().GetResult();
            using (JsonDocument sensorsDoc = JsonDocument.Parse(sensorsJson))
            {
                JsonElement root = sensorsDoc.RootElement;
                if (!TryGetString(root, "dataSource", out string? ds) || ds != "demo")
                {
                    return Fail($"A1 dataSource expected demo, got {ds ?? "null"}");
                }

                if (!TryGetString(root, "equipmentState", out string? state)
                    || !string.Equals(state, "RUNNING", StringComparison.OrdinalIgnoreCase))
                {
                    return Fail($"A1 equipmentState expected RUNNING, got {state ?? "null"}");
                }
            }

            log.Add("A5 sensors snapshot dataSource=demo equipmentState=RUNNING");

            if (!ValidateModules(http, url, log))
            {
                return Fail("modules/latest validation failed");
            }

            if (!ValidateRecipe(http, url, log))
            {
                return Fail("recipe/active validation failed");
            }

            (bool aiOk, bool aiReady, string? engine) = ValidateAi(http, url, log, requireMlReady);
            if (!aiOk)
            {
                return Fail("ai/status or ai/latest validation failed");
            }

            if (!PostAndListEvents(http, url, flask, log))
            {
                return Fail("events POST/GET failed");
            }

            if (!ValidateHistory(http, url, log))
            {
                return Fail("history validation failed");
            }

            return new Result
            {
                Success = true,
                AiReady = aiReady,
                AiEngine = engine,
                Message = "ok flask e2e",
                Report = string.Join(Environment.NewLine, log)
            };
        }
        catch (Exception ex)
        {
            return Fail($"exception: {ex.Message}");
        }
    }

    private static EtchTelemetryPayload BuildDemoPayload(int simTicks)
    {
        if (simTicks <= 0)
        {
            simTicks = 80;
        }

        var sim = new TmTransferSimulator();
        sim.StartDemoLoop();
        int motion = Scheduling.EquipmentCapacityConfig.Default.VacuumMotionStepsPerUiTick;
        for (int i = 0; i < simTicks; i++)
        {
            sim.Tick(motion);
        }

        var ctx = new ModuleStateAggregator.Context
        {
            EquipmentState = "RUNNING",
            MaintenanceMode = false,
            HasLiveSensorData = false,
            InterlockOk = true,
            BenchMode = true,
            AccessSafe = true,
            AccessInputValid = true,
            Transfer = sim
        };

        List<ModuleTelemetryModule> modules =
            HmiTelemetryPayloadFactory.FromModuleSnapshots(ModuleStateAggregator.Build(ctx));

        return HmiTelemetryPayloadFactory.Create(
            "demo",
            false,
            true,
            false,
            false,
            "RUNNING",
            null,
            true,
            "e2e",
            25,
            40,
            5,
            0.1,
            true,
            modules,
            HmiTelemetryPayloadFactory.DefaultRecipe());
    }

    private static bool ValidateModules(HttpClient http, string url, List<string> log)
    {
        string json = http.GetStringAsync($"{url}/api/etch/modules/latest?source=demo").GetAwaiter().GetResult();
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("success", out JsonElement ok) || !ok.GetBoolean())
        {
            return false;
        }

        if (!root.TryGetProperty("count", out JsonElement count) || count.GetInt32() < 1)
        {
            return false;
        }

        log.Add($"A2 modules/latest count={count.GetInt32()}");
        return true;
    }

    private static bool ValidateRecipe(HttpClient http, string url, List<string> log)
    {
        string json = http.GetStringAsync($"{url}/api/etch/recipe/active?source=demo").GetAwaiter().GetResult();
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("success", out JsonElement ok) || !ok.GetBoolean())
        {
            return false;
        }

        if (!root.TryGetProperty("recipe", out JsonElement recipe)
            || !recipe.TryGetProperty("id", out JsonElement id)
            || string.IsNullOrWhiteSpace(id.GetString()))
        {
            return false;
        }

        log.Add($"A3 recipe/active id={id.GetString()}");
        return true;
    }

    private static (bool Ok, bool Ready, string? Engine) ValidateAi(
        HttpClient http,
        string url,
        List<string> log,
        bool requireMlReady)
    {
        string statusJson = http.GetStringAsync($"{url}/api/etch/ai/status").GetAwaiter().GetResult();
        using JsonDocument statusDoc = JsonDocument.Parse(statusJson);
        JsonElement statusRoot = statusDoc.RootElement;

        bool ready = statusRoot.TryGetProperty("ready", out JsonElement readyEl) && readyEl.GetBoolean();
        string engine = statusRoot.TryGetProperty("engine", out JsonElement engEl)
            ? engEl.GetString() ?? "unknown"
            : "unknown";

        if (requireMlReady && (!ready || !string.Equals(engine, "sklearn", StringComparison.OrdinalIgnoreCase)))
        {
            log.Add($"A4 ai/status ready={ready} engine={engine} (ML required)");
            return (false, ready, engine);
        }

        log.Add($"A4 ai/status ready={ready} engine={engine}");

        string latestJson = http.GetStringAsync($"{url}/api/etch/ai/latest").GetAwaiter().GetResult();
        using JsonDocument latestDoc = JsonDocument.Parse(latestJson);
        JsonElement latestRoot = latestDoc.RootElement;
        if (!latestRoot.TryGetProperty("success", out JsonElement latestOk) || !latestOk.GetBoolean())
        {
            return (false, ready, engine);
        }

        bool hasScore = latestRoot.TryGetProperty("anomaly_score", out _)
                        || latestRoot.TryGetProperty("anomalyScore", out _);
        if (!hasScore)
        {
            return (false, ready, engine);
        }

        log.Add("A4 ai/latest success + anomaly score");
        return (true, ready, engine);
    }

    private static bool PostAndListEvents(HttpClient http, string url, EtchFlaskClient flask, List<string> log)
    {
        var items = new List<FlaskEventItem>
        {
            new()
            {
                Time = DateTime.UtcNow.ToString("o"),
                Kind = "hmi_event",
                Message = "flask-e2e probe",
                EquipmentState = "RUNNING",
                Username = "e2e"
            }
        };

        if (!flask.TryPostEtchEventsAsync(items, "demo").GetAwaiter().GetResult())
        {
            return false;
        }

        string json = http.GetStringAsync($"{url}/api/etch/events?source=demo&limit=20").GetAwaiter().GetResult();
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("success", out JsonElement ok) || !ok.GetBoolean())
        {
            return false;
        }

        log.Add("POST /api/etch/events + GET events → OK");
        return true;
    }

    private static bool ValidateHistory(HttpClient http, string url, List<string> log)
    {
        string json = http.GetStringAsync($"{url}/api/etch/history?source=demo&limit=10").GetAwaiter().GetResult();
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("success", out JsonElement ok) || !ok.GetBoolean())
        {
            return false;
        }

        int count = 0;
        if (root.TryGetProperty("items", out JsonElement items) && items.ValueKind == JsonValueKind.Array)
        {
            count = items.GetArrayLength();
        }

        log.Add($"GET /api/etch/history items={count}");
        return true;
    }

    private static bool TryGetString(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out JsonElement el))
        {
            return false;
        }

        value = el.GetString();
        return true;
    }

    private static Result Fail(string message) => new() { Success = false, Message = message };
}

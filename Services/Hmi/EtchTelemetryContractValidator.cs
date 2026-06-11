using System.Text.Json;

namespace etch_ui.Services.Hmi;

/// <summary>Flask POST sensor-data JSON 계약 검증 (WPF ↔ etchflask drift 방지).</summary>
public static class EtchTelemetryContractValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly HashSet<string> AllowedDataSources =
        new(StringComparer.OrdinalIgnoreCase) { "live", "demo", "offline" };

    private static readonly string[] RequiredJsonKeys =
    [
        "equipmentId",
        "dataSource",
        "sensorsLive",
        "benchMode",
        "maintenanceMode",
        "equipmentState",
        "interlockOk",
        "temperature",
        "humidity",
        "pressure",
        "vibration",
        "modules",
        "recipe"
    ];

    public static bool TryValidate(EtchTelemetryPayload payload, out string error)
    {
        if (string.IsNullOrWhiteSpace(payload.DataSource)
            || !AllowedDataSources.Contains(payload.DataSource))
        {
            error = $"telemetry: invalid dataSource '{payload.DataSource}'";
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload.EquipmentState))
        {
            error = "telemetry: equipmentState required";
            return false;
        }

        if (payload.Modules is null || payload.Modules.Count == 0)
        {
            error = "telemetry: modules[] required";
            return false;
        }

        foreach (ModuleTelemetryModule module in payload.Modules)
        {
            if (string.IsNullOrWhiteSpace(module.Id) || string.IsNullOrWhiteSpace(module.State))
            {
                error = "telemetry: module id/state required";
                return false;
            }
        }

        if (payload.Recipe is null || string.IsNullOrWhiteSpace(payload.Recipe.Id))
        {
            error = "telemetry: recipe.id required";
            return false;
        }

        if (payload.DataSource.Equals("live", StringComparison.OrdinalIgnoreCase) && !payload.SensorsLive)
        {
            error = "telemetry: live requires sensorsLive=true";
            return false;
        }

        if (payload.DataSource.Equals("offline", StringComparison.OrdinalIgnoreCase) && payload.SensorsLive)
        {
            error = "telemetry: offline must not set sensorsLive";
            return false;
        }

        if (!TryValidateJsonShape(payload, out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateJsonShape(EtchTelemetryPayload payload, out string error)
    {
        try
        {
            string json = JsonSerializer.Serialize(payload, JsonOptions);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            foreach (string key in RequiredJsonKeys)
            {
                if (!root.TryGetProperty(key, out _))
                {
                    error = $"telemetry-json: missing '{key}'";
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            error = $"telemetry-json: {ex.Message}";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

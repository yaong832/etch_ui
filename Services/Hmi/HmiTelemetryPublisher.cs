namespace etch_ui.Services.Hmi;

/// <summary>Flask 텔레메트리 POST + 로컬 SQLite 백업.</summary>
public sealed class HmiTelemetryPublisher
{
    private readonly EtchFlaskClient _flask;
    private readonly HmiTelemetryStore _store;

    public HmiTelemetryPublisher(EtchFlaskClient flask, HmiTelemetryStore store)
    {
        _flask = flask;
        _store = store;
    }

    public async Task<bool> PublishAsync(EtchTelemetryPayload payload)
    {
        if (!string.Equals(payload.DataSource, "offline", StringComparison.OrdinalIgnoreCase))
        {
            _store.InsertSample(
                payload.DataSource ?? "demo",
                payload.EquipmentState ?? "IDLE",
                payload.AlarmCode,
                payload.Temperature,
                payload.Humidity,
                payload.Pressure,
                payload.Vibration,
                payload.InterlockOk,
                payload.MaintenanceMode,
                payload.Username);
        }

        return await _flask.TryPostEtchSensorDataAsync(payload).ConfigureAwait(false);
    }
}

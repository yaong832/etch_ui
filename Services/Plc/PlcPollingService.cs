using etch_ui.Plc;

namespace etch_ui.Services.Plc;

/// <summary>EtherCAT ADS 폴링·재연결 쿨다운 (MainWindow UI 루프에서 호출).</summary>
public sealed class PlcPollingService
{
    private readonly PlcAdsService _plc;
    private int _reconnectCooldown;

    public PlcPollingService(PlcAdsService plc) => _plc = plc;

    public PlcAdsService Plc => _plc;

    public bool IsConnected => _plc.IsConnected;

    public bool TryConnect(int adsPort) => _plc.TryConnect(adsPort);

    public void Disconnect() => _plc.Disconnect();

    public string? LastError => _plc.LastError;

    /// <summary>연결됨 → 스냅샷 읽기. 미연결 → 재연결 스케줄.</summary>
    public PlcPollResult PollConnected()
    {
        if (_plc.IsConnected)
        {
            if (_plc.TryReadSnapshot(out PlcProcessSnapshot snap))
            {
                return PlcPollResult.FromSnapshot(snap);
            }

            return PlcPollResult.LinkLost(_plc.LastError ?? "EtherCAT 읽기 실패");
        }

        if (_reconnectCooldown > 0)
        {
            _reconnectCooldown--;
            return PlcPollResult.WaitingReconnect;
        }

        return PlcPollResult.NeedReconnect;
    }

    public void ArmReconnectCooldown(int ticks = 3) => _reconnectCooldown = ticks;

    public bool TryReconnect(int adsPort) => _plc.TryConnect(adsPort);

    public void Dispose() => _plc.Dispose();
}

public enum PlcPollKind
{
    Snapshot,
    LinkLost,
    NeedReconnect,
    WaitingReconnect
}

public readonly struct PlcPollResult
{
    public PlcPollKind Kind { get; init; }
    public PlcProcessSnapshot Snapshot { get; init; }
    public string? Error { get; init; }

    public static PlcPollResult FromSnapshot(PlcProcessSnapshot snap) =>
        new() { Kind = PlcPollKind.Snapshot, Snapshot = snap };

    public static PlcPollResult LinkLost(string error) =>
        new() { Kind = PlcPollKind.LinkLost, Error = error };

    public static PlcPollResult NeedReconnect => new() { Kind = PlcPollKind.NeedReconnect };
    public static PlcPollResult WaitingReconnect => new() { Kind = PlcPollKind.WaitingReconnect };
}

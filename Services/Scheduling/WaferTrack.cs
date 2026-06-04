using etch_ui.Equipment.Models;

namespace etch_ui.Services.Scheduling;

/// <summary>스케줄러가 추적하는 웨이퍼 1매.</summary>
public sealed class WaferTrack
{
    private static int _nextId = 1;

    public int Id { get; }
    public LoadPortId OriginPort { get; }
    public EquipmentRegion NextProcessFoupRegion { get; }

    /// <summary>PM2~4 중 하나에서 동일 식각 공정 완료.</summary>
    public bool HasCompletedEtch { get; set; }

    /// <summary>PM1 Strip(후공정) 완료.</summary>
    public bool HasCompletedStrip { get; set; }

    public WaferTrack(LoadPortId originPort, EquipmentRegion nextProcessFoup)
    {
        Id = _nextId++;
        OriginPort = originPort;
        NextProcessFoupRegion = nextProcessFoup;
    }

    public bool IsEtchComplete => HasCompletedEtch;
}

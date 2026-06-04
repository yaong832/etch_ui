namespace etch_ui.Equipment.Models;

/// <summary>클러스터 툴 모니터링 단위 (참고 UI: EFEM · LP · BM · TM · PM).</summary>
public enum EquipmentModuleId
{
    Efem,
    /// <summary>EFEM 내 대기압 TM (FOUP·Aligner·BM 핸드오프).</summary>
    EfemRobot,
    Aligner,
    SideStorage,
    LoadPort1,
    LoadPort2,
    LoadPort3,
    BufferModule,
    TransferModule,
    Pm1,
    Pm2,
    Pm3,
    Pm4
}

namespace etch_ui.Equipment.Models;

/// <summary>TM/웨이퍼가 위치하는 장비 영역 (SemiconductorUi와 동일 개념).</summary>
public enum EquipmentRegion
{
    None,
    /// <summary>EFEM 내 대기압 TM (로봇 피벗).</summary>
    EfemRobot,
    /// <summary>클러스터 진공 TM (로봇 피벗).</summary>
    TM,
    /// <summary>PM1 — 감광액 제거(Strip).</summary>
    ChamberA,
    /// <summary>PM2 — 식각(Etch).</summary>
    ChamberB,
    /// <summary>PM3 — 식각(Etch).</summary>
    ChamberC,
    /// <summary>PM4 — 식각(Etch).</summary>
    ChamberD,
    /// <summary>Load Port 1 FOUP.</summary>
    FoupA,
    /// <summary>Load Port 2 FOUP.</summary>
    FoupB,
    /// <summary>Load Port 3 FOUP.</summary>
    FoupC,
    LoadLock,
    Aligner,
    SideStorage,
    /// <summary>LP1 Side Stg 출구 — 다음 공정 FOUP(직결).</summary>
    NextProcessFoupA,
    /// <summary>LP2 Side Stg 출구 — 다음 공정 FOUP.</summary>
    NextProcessFoupB,
    /// <summary>LP3 Side Stg 출구 — 다음 공정 FOUP.</summary>
    NextProcessFoupC,
    /// <summary>레거시·도식 호환. 신규 이송은 NextProcessFoup* 사용.</summary>
    ExternalProcess
}

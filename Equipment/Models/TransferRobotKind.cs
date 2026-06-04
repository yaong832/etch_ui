namespace etch_ui.Equipment.Models;

/// <summary>이송 로봇 구분 — EFEM 대기압 TM vs 클러스터 진공 TM.</summary>
public enum TransferRobotKind
{
    /// <summary>EFEM 내 대기압 TM (LP · Aligner · BM 대기압측 · Side Stg).</summary>
    EfemAtmospheric,

    /// <summary>BM 진공측 ~ PM 슬릿 (클러스터 TM).</summary>
    VacuumTm
}

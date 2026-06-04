using etch_ui.Services.Simulation;

namespace etch_ui.Services;

/// <summary>공정 스텝 사다리 ↔ 장비 상태·<see cref="TmTransferSimulator.SimPhase"/>.</summary>
public static class ProcessStepMapper
{
    public readonly record struct StepState(int Index, bool Warning, string ActiveCaption, string Detail);

    private static readonly string[] DefaultLabels =
    [
        "① LL / 도어 대기",
        "② 이송·픽업·드롭",
        "③ PM 가공·이송",
        "④ 경고·환경 편향",
        "⑤ 점검·알람·Maint"
    ];

    public static IReadOnlyList<string> DefaultStepLabels => DefaultLabels;

    public static StepState FromEquipmentState(string stateUpper, bool maintenanceMode)
    {
        if (maintenanceMode)
        {
            return new(4, false, "⑤ 유지보수", "공정 Start 차단 · 정비 작업");
        }

        return stateUpper switch
        {
            "MAINTENANCE" => new(4, false, "⑤ 유지보수", "정비 모드"),
            "ALARM" => new(4, false, "⑤ 알람", "Alarm Reset · 인터락 확인"),
            "WARNING" => new(3, true, "④ 경고", "온·습도 편향 — 모니터링 강화"),
            "RUNNING" => new(2, false, "③ 운전", "가상 이송 스케줄 동작 중"),
            "READY" => new(1, false, "② 준비", "Start 가능 · 인터락 OK"),
            _ => new(0, false, "① 대기", "EtherCAT·Load Lock 확인")
        };
    }

    public static StepState FromSimPhase(TmTransferSimulator.SimPhase phase, string phaseHint)
    {
        (int index, string caption) = phase switch
        {
            TmTransferSimulator.SimPhase.Idle => (0, "① 대기"),
            TmTransferSimulator.SimPhase.WaitDoorPickupOpen or TmTransferSimulator.SimPhase.WaitDoorPickupClose
                or TmTransferSimulator.SimPhase.WaitDoorDropoffOpen or TmTransferSimulator.SimPhase.WaitDoorDropoffClose
                => (0, "① 가상 도어"),
            TmTransferSimulator.SimPhase.MoveToPickup or TmTransferSimulator.SimPhase.MoveToDropoff
                => (1, "② TM 이동"),
            TmTransferSimulator.SimPhase.PickupExtend or TmTransferSimulator.SimPhase.PickupGrip
                or TmTransferSimulator.SimPhase.PickupRetract => (1, "② 픽업"),
            TmTransferSimulator.SimPhase.DropoffExtend or TmTransferSimulator.SimPhase.DropoffRelease
                or TmTransferSimulator.SimPhase.DropoffRetract => (1, "② 드롭"),
            TmTransferSimulator.SimPhase.RotateBlade => (1, "② 블레이드 회전"),
            _ => (2, "③ 이송")
        };

        return new(index, false, caption, phaseHint);
    }
}

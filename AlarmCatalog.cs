namespace etch_ui;

/// <summary>WPF HMI 알람 코드 — 인터락·AI-2·현장 조치 가이드 단일 기준.</summary>
public static class AlarmCatalog
{
    public readonly record struct AlarmInfo(
        string Code,
        string Title,
        string Detail,
        string Action,
        string InterlockModule);

    public static AlarmInfo? TryGet(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return code.Trim().ToUpperInvariant() switch
        {
            "A001" => new AlarmInfo(
                "A001",
                "통신 / EtherCAT",
                "EtherCAT(TwinCAT ADS) 연결이 끊겼거나, 시뮬 미허용 상태에서 유효한 공정 데이터를 읽지 못했습니다.",
                "TwinCAT 실행·ADS 포트(기본 851)·방화벽을 확인하세요. 데모만 필요하면 헤더「시뮬 허용」을 켭니다.",
                "EFEM · BM"),
            "A002" => new AlarmInfo(
                "A002",
                "압력",
                "챔버/Load Lock 압력(mTorr)이 정상 대역을 벗어났습니다.",
                "누설·펌프/밸브·진공 게이지·레시피 setpoint를 점검하세요. 인터락 해제 전 압력 안정을 확인합니다.",
                "BM · TM · PM"),
            "A003" => new AlarmInfo(
                "A003",
                "진동",
                "진동이 허용 한계를 초과했습니다.",
                "픽업·고정·외란 원인을 확인하고 기계 상태를 점검하세요.",
                "TM"),
            "A004" => new AlarmInfo(
                "A004",
                "Load Lock 접촉",
                "Load Lock 문이 열렸거나 접촉 센서가 닫힘을 감지하지 못했습니다.",
                "Load Lock을 닫고 접촉 센서(DI 비트5)가 ON인지 확인하세요. RUNNING 중 열림 시 가상 이송이 즉시 정지됩니다.",
                "BM"),
            "A005" => new AlarmInfo(
                "A005",
                "온도",
                "주변 환경 온도가 정상 범위를 벗어났습니다.",
                "냉난방·배기·열원을 확인하세요. WARNING 구간에서는 공정 유지 시 모니터링을 강화합니다.",
                "EFEM"),
            "A006" => new AlarmInfo(
                "A006",
                "습도",
                "환경 습도가 정상 범위를 벗어났습니다.",
                "제습/가습·누설·환경 관리를 확인하세요. WARNING 구간에서는 공정 유지 시 모니터링을 강화합니다.",
                "EFEM"),
            _ => new AlarmInfo(
                code.Trim().ToUpperInvariant(),
                "기타",
                "등록되지 않은 알람 코드입니다.",
                "설비 매뉴얼 및 유지보수 담당에게 문의하세요.",
                "—"),
        };
    }

    public static bool IsEnvironmentWarningCode(string? code) =>
        code is "A005" or "A006";

    public static string FormatLine(string? code)
    {
        AlarmInfo? info = TryGet(code);
        return info is null ? string.Empty : $"{info.Value.Code} {info.Value.Title}: {info.Value.Detail}";
    }

    public static string FormatBanner(string? code)
    {
        AlarmInfo? info = TryGet(code);
        if (info is null)
        {
            return string.IsNullOrWhiteSpace(code)
                ? "⛔ ALARM — 이송 정지"
                : $"⛔ ALARM {code} — 이송 정지";
        }

        AlarmInfo ai = info.Value;
        return $"⛔ ALARM {ai.Code} · {ai.Title} — {ai.Detail}";
    }

    public static string FormatDetailWithAction(string? code)
    {
        AlarmInfo? info = TryGet(code);
        if (info is null)
        {
            return "알람 상세 없음 — Reset 후 로그·인터락을 확인하세요.";
        }

        AlarmInfo ai = info.Value;
        return $"{ai.Detail}\n▶ 조치: {ai.Action}\n▶ 관련 모듈: {ai.InterlockModule}";
    }
}

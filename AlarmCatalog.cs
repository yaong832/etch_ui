namespace etch_ui;

/// <summary>WPF HMI에서 표시하는 알람 코드별 설명(현장 조치 가이드).</summary>
public static class AlarmCatalog
{
    public readonly record struct AlarmInfo(string Code, string Title, string Detail, string Action);

    public static AlarmInfo? TryGet(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return code.Trim().ToUpperInvariant() switch
        {
            "A001" => new AlarmInfo(code, "통신 / EtherCAT", "EtherCAT(TwinCAT ADS) 연결이 끊겼거나, 시뮬 미허용 상태에서 유효한 공정 데이터를 읽지 못했습니다.",
                "TwinCAT 실행·ADS 포트(기본 851)·방화벽을 확인하세요. 데모만 필요하면「시뮬 허용」을 켭니다."),
            "A002" => new AlarmInfo(code, "압력", "챔버/Load Lock 압력(mTorr)이 정상 대역을 벗어났습니다.",
                "누설, 펌프/밸브, 진공 게이지·레시피 setpoint를 점검하세요."),
            "A003" => new AlarmInfo(code, "진동", "진동이 허용 한계를 초과했습니다.",
                "픽업·고정·외란원인을 확인하고 기계 상태를 점검하세요."),
            "A004" => new AlarmInfo(code, "도어 / 유도형", "유도형 센서(Inductive) OFF — 도어 열림 또는 인터락 미충족.",
                "도어가 닫혀 유도형 센서가 ON인지 확인하세요. (DI 비트5=true = 닫힘)"),
            "A005" => new AlarmInfo(code, "온도", "주변 환경 온도가 정상 범위를 벗어났습니다.",
                "냉난방·배기·열원을 확인하세요. 공정 중이면 WARNING 후 조치를 검토합니다."),
            "A006" => new AlarmInfo(code, "습도", "환경 습도가 정상 범위를 벗어났습니다.",
                "제습/가습·누설·환경 관리를 확인하세요."),
            _ => new AlarmInfo(code, "기타", "등록되지 않은 알람 코드입니다.", "설비 매뉴얼 및 유지보수 담당에게 문의하세요."),
        };
    }

    public static string FormatLine(string? code)
    {
        var info = TryGet(code);
        return info is null ? "" : $"{info.Value.Code} {info.Value.Title}: {info.Value.Detail}";
    }
}

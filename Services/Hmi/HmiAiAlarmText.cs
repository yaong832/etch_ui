using etch_ui.Services;

namespace etch_ui.Services.Hmi;

/// <summary>AI 예측 알람 문구 — AlarmCatalog 조치와 일관.</summary>
public static class HmiAiAlarmText
{
    public static string FormatPredictedLine(EtchAiDiagnosis? diag)
    {
        if (diag is null || !diag.Success)
        {
            return "예상 알람: —";
        }

        string pred = diag.PredictedAlarm?.Trim().ToUpperInvariant() ?? "NONE";
        if (pred is "" or "NONE")
        {
            return "예상 알람: 없음 (정상 추세)";
        }

        AlarmCatalog.AlarmInfo? info = AlarmCatalog.TryGet(pred);
        string conf = $"신뢰 {diag.PredictionConfidence:P0} (조언만)";
        if (info is null)
        {
            return $"예상 알람: {pred} · {conf}";
        }

        return $"예상 알람: {pred} · {info.Value.Title} · {conf} · ▶ {info.Value.Action}";
    }
}

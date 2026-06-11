using System.Windows.Media;
using etch_ui.Services;

namespace etch_ui.Services.Hmi;

/// <summary>헤더 AI 엔진 칩 (ML / 규칙 / OFF).</summary>
public static class HmiAiEnginePresenter
{
    public sealed record Presentation(string Text, Brush Brush, string Hint);

    public static Presentation Describe(
        bool flaskReachable,
        FlaskAiStatusSnapshot? status,
        EtchAiDiagnosis? latest)
    {
        if (!flaskReachable)
        {
            return new Presentation(
                "OFF",
                Brushes.DimGray,
                "Flask 미연결 — run_flask.bat 후 헤더 Flask 칩 OK 확인");
        }

        if (status?.Ready == true && string.Equals(status.Engine, "sklearn", StringComparison.OrdinalIgnoreCase))
        {
            return new Presentation(
                "ML",
                Brushes.ForestGreen,
                "sklearn 모델 로드됨 — GET /api/etch/ai/status ready");
        }

        if (latest is { Success: true, Stub: false })
        {
            return new Presentation(
                "ML",
                Brushes.ForestGreen,
                "최근 AI 진단이 ML 추론 결과입니다");
        }

        if (latest is { Success: true, Stub: true })
        {
            return new Presentation(
                "규칙",
                Brushes.DarkGoldenrod,
                "규칙 스텁 — models/etch 배포 후 Flask 재시작");
        }

        if (status is not null && string.Equals(status.Engine, "sklearn", StringComparison.OrdinalIgnoreCase))
        {
            return new Presentation(
                "ML",
                Brushes.ForestGreen,
                status.Message ?? "AI 엔진 sklearn");
        }

        if (status is not null)
        {
            return new Presentation(
                "규칙",
                Brushes.DarkGoldenrod,
                status.Message ?? "ML 미로드 — 규칙 폴백");
        }

        return new Presentation(
            "대기",
            Brushes.SteelBlue,
            "sensor-data 수신 후 AI 진단이 갱신됩니다");
    }
}

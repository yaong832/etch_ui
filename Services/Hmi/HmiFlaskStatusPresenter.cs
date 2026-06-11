using System.Windows.Media;

namespace etch_ui.Services.Hmi;

/// <summary>Flask 헤더 칩 — 연결·마지막 성공 시각·힌트.</summary>
public static class HmiFlaskStatusPresenter
{
    public readonly record struct FlaskPresentation(string Text, Brush Brush, string Hint);

    public static FlaskPresentation Describe(
        bool probeDone,
        bool reachable,
        DateTime? lastSuccessUtc,
        string baseUrl)
    {
        string url = string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:5000" : baseUrl.Trim();

        if (!probeDone)
        {
            return new FlaskPresentation(
                "확인 중…",
                Brushes.Goldenrod,
                $"Flask 연결 확인 중 — {url}");
        }

        if (reachable)
        {
            string time = lastSuccessUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "—";
            return new FlaskPresentation(
                $"OK · {time}",
                Brushes.LimeGreen,
                $"Flask 연결됨 · 마지막 전송 {time} · {url}");
        }

        string last = lastSuccessUtc.HasValue
            ? $"마지막 OK {lastSuccessUtc.Value.ToLocalTime():HH:mm:ss}"
            : "이번 세션 미연결";
        return new FlaskPresentation(
            "OFF",
            Brushes.OrangeRed,
            $"{last} · 로컬 DB 저장 중 · C:\\etchflask\\run_flask.bat 또는 {url}");
    }
}

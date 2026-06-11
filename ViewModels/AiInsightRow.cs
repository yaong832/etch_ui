using System.Windows.Media;

namespace etch_ui.ViewModels;

/// <summary>AI·센서·스케줄 조언 1줄 (Codex 스타일 세분화 표시).</summary>
public sealed class AiInsightRow
{
    public required string Category { get; init; }
    public required string Signal { get; init; }
    public required string Detail { get; init; }
    public required string SeverityLabel { get; init; }
    public Brush SeverityBrush { get; init; } = Brushes.DimGray;

    public string DisplayLine => $"[{Category}] {Signal} — {Detail}";
}

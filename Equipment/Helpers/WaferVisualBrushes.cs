using System.Windows.Media;
using etch_ui.Services.Scheduling;

namespace etch_ui.Equipment.Helpers;

/// <summary>공정 전·진행 중·완료 웨이퍼 색 (도식용).</summary>
public static class WaferVisualBrushes
{
    private static readonly Brush Pending = new SolidColorBrush(Color.FromRgb(245, 222, 179));
    private static readonly Brush Processing = new SolidColorBrush(Color.FromRgb(251, 191, 36));
    private static readonly Brush EtchDone = new SolidColorBrush(Color.FromRgb(110, 231, 183));
    private static readonly Brush Complete = new SolidColorBrush(Color.FromRgb(147, 197, 253));
    private static readonly Brush Empty = Brushes.Transparent;

    static WaferVisualBrushes()
    {
        Pending.Freeze();
        Processing.Freeze();
        EtchDone.Freeze();
        Complete.Freeze();
    }

    public static Brush ForWafer(WaferTrack? wafer, int processTicksRemaining = 0)
    {
        if (wafer is null)
        {
            return Empty;
        }

        if (processTicksRemaining > 0)
        {
            return Processing;
        }

        if (wafer.HasCompletedStrip)
        {
            return Complete;
        }

        if (wafer.HasCompletedEtch)
        {
            return EtchDone;
        }

        return Pending;
    }

    public static Brush ForFoupInventory() => Pending;
}

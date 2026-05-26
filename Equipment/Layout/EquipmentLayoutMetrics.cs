using System.Windows;

namespace etch_ui.Equipment.Layout;

/// <summary>설계 좌표(픽셀). Viewbox로 창 크기에 맞춤 — SemiconductorUi Form1Configurator 배치 기준.</summary>
public static class EquipmentLayoutMetrics
{
    public const double DesignWidth = 920;
    public const double DesignHeight = 640;

    public static readonly Point TmCenter = new(460, 320);
    public const double TmBodyRadius = 42;
    public const double ChamberGap = 28;
    public static readonly Size ChamberSize = new(118, 88);
    public static readonly Size FoupSize = new(100, 72);
    public const double FoupRadius = 200;

    public static Point ChamberAPosition => new(
        TmCenter.X - TmBodyRadius - ChamberGap - ChamberSize.Width,
        TmCenter.Y - ChamberSize.Height / 2);

    public static Point ChamberBPosition => new(
        TmCenter.X - ChamberSize.Width / 2,
        TmCenter.Y - TmBodyRadius - ChamberGap - ChamberSize.Height);

    public static Point ChamberCPosition => new(
        TmCenter.X + TmBodyRadius + ChamberGap,
        TmCenter.Y - ChamberSize.Height / 2);

    public static Point FoupAPosition => Polar(TmCenter, FoupRadius, 225);
    public static Point FoupBPosition => Polar(TmCenter, FoupRadius, 315);

    public static Point LoadLockPosition => new(72, TmCenter.Y - 44);

    private static Point Polar(Point center, double radius, double degrees)
    {
        double rad = degrees * Math.PI / 180.0;
        return new Point(
            center.X + radius * Math.Cos(rad) - FoupSize.Width / 2,
            center.Y + radius * Math.Sin(rad) - FoupSize.Height / 2);
    }
}

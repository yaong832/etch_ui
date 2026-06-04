using System.Windows;
using etch_ui.Equipment.Models;

namespace etch_ui.Equipment.Layout;

/// <summary>
/// 클러스터 툴 평면 배치 (참고 UI: LP · EFEM · BM · TM · PM1~4).
/// Viewbox로 스케일 — 좌표는 설계 픽셀 기준.
/// </summary>
public static class EquipmentLayoutMetrics
{
    public const double DesignWidth = 1000;
    public const double DesignHeight = 680;

    public static readonly Point TmCenter = new(548, 318);
    public const double TmHostSize = 112;
    public static readonly double TmPivot = TmHostSize / 2;

    public static readonly Size LoadPortSize = new(84, 50);
    public static readonly Point LoadPort1Position = new(14, 227);
    public static readonly Point LoadPort2Position = new(14, 293);
    public static readonly Point LoadPort3Position = new(14, 359);

    public static readonly Point EfemPosition = new(112, 204);
    public static readonly Size EfemSize = new(208, 228);

    public static readonly Point EfemRobotCenter = new(
        EfemPosition.X + EfemSize.Width / 2,
        EfemPosition.Y + EfemSize.Height / 2);
    public const double EfemRobotHostSize = 76;
    public static readonly double EfemRobotPivot = EfemRobotHostSize / 2;

    public static readonly Point AlignerPosition = new(136, 400);
    public static readonly Size AlignerSize = new(92, 56);

    public static readonly Point SideStoragePosition = new(244, 400);
    public static readonly Size SideStorageSize = new(72, 52);

    public static readonly Point NextProcessFoup1Position = new(102, 170);
    public static readonly Point NextProcessFoup2Position = new(102, 236);
    public static readonly Point NextProcessFoup3Position = new(102, 302);
    public static readonly Size NextProcessFoupSize = new(56, 40);

    public static readonly Point ExternalProcessPosition = new(228, 72);
    public static readonly Size ExternalProcessSize = new(72, 48);

    public static readonly Point BufferPosition = new(348, 268);
    public static readonly Size BufferSize = new(92, 100);

    public static readonly Size PmSize = new(124, 96);
    public static readonly Point Pm1Position = new(568, 58);   // Strip
    public static readonly Point Pm2Position = new(698, 168);  // Etch
    public static readonly Point Pm3Position = new(698, 328);  // Etch
    public static readonly Point Pm4Position = new(568, 458);  // Etch

    public static Point GetPortCenter(EquipmentRegion region)
    {
        return region switch
        {
            EquipmentRegion.FoupA => CenterOf(LoadPort1Position, LoadPortSize),
            EquipmentRegion.FoupB => CenterOf(LoadPort2Position, LoadPortSize),
            EquipmentRegion.FoupC => CenterOf(LoadPort3Position, LoadPortSize),
            EquipmentRegion.Aligner => CenterOf(AlignerPosition, AlignerSize),
            EquipmentRegion.SideStorage => CenterOf(SideStoragePosition, SideStorageSize),
            EquipmentRegion.NextProcessFoupA => CenterOf(NextProcessFoup1Position, NextProcessFoupSize),
            EquipmentRegion.NextProcessFoupB => CenterOf(NextProcessFoup2Position, NextProcessFoupSize),
            EquipmentRegion.NextProcessFoupC => CenterOf(NextProcessFoup3Position, NextProcessFoupSize),
            EquipmentRegion.ExternalProcess => CenterOf(ExternalProcessPosition, ExternalProcessSize),
            EquipmentRegion.ChamberA => CenterOf(Pm1Position, PmSize),
            EquipmentRegion.ChamberB => CenterOf(Pm2Position, PmSize),
            EquipmentRegion.ChamberC => CenterOf(Pm3Position, PmSize),
            EquipmentRegion.ChamberD => CenterOf(Pm4Position, PmSize),
            EquipmentRegion.LoadLock => CenterOf(BufferPosition, BufferSize),
            EquipmentRegion.EfemRobot => EfemRobotCenter,
            EquipmentRegion.TM => TmCenter,
            _ => TmCenter
        };
    }

    public static Point TmHostTopLeft => new(TmCenter.X - TmPivot, TmCenter.Y - TmPivot);

    public static Point EfemRobotHostTopLeft => new(
        EfemRobotCenter.X - EfemRobotPivot,
        EfemRobotCenter.Y - EfemRobotPivot);

    private static Point CenterOf(Point topLeft, Size size) =>
        new(topLeft.X + size.Width / 2, topLeft.Y + size.Height / 2);

    /// <summary>이송 트랙 폴리라인 (LP1 → EFEM → BM → TM).</summary>
    public static Point[] MainTransferPath
    {
        get
        {
            Point lp1 = GetPortCenter(EquipmentRegion.FoupA);
            Point efemOut = new(EfemPosition.X + EfemSize.Width, EfemPosition.Y + EfemSize.Height / 2);
            Point bm = GetPortCenter(EquipmentRegion.LoadLock);
            return [lp1, efemOut, bm, TmCenter];
        }
    }
}

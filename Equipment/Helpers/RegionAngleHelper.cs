using System.Windows;
using etch_ui.Equipment.Layout;
using etch_ui.Equipment.Models;

namespace etch_ui.Equipment.Helpers;

public static class RegionAngleHelper
{
    /// <summary>활성 로봇 피벗에서 목표 Region 포트를 향하는 팔 각도(도).</summary>
    public static double ToDegrees(
        EquipmentRegion region,
        TransferRobotKind robot,
        bool hardwareIdleAt90 = false)
    {
        if (region == EquipmentRegion.TM && robot == TransferRobotKind.VacuumTm)
        {
            return hardwareIdleAt90 ? -90 : -125;
        }

        if (region == EquipmentRegion.EfemRobot && robot == TransferRobotKind.EfemAtmospheric)
        {
            return -90;
        }

        Point port = EquipmentLayoutMetrics.GetPortCenter(region);
        Point pivot = robot == TransferRobotKind.EfemAtmospheric
            ? EquipmentLayoutMetrics.EfemRobotCenter
            : EquipmentLayoutMetrics.TmCenter;
        return Math.Atan2(port.Y - pivot.Y, port.X - pivot.X) * 180.0 / Math.PI;
    }

    public static string FormatLabel(EquipmentRegion region, TransferRobotKind robot) => region switch
    {
        EquipmentRegion.EfemRobot => robot == TransferRobotKind.EfemAtmospheric ? "EFEM·TM" : "TM",
        EquipmentRegion.FoupA => "Load Port 1",
        EquipmentRegion.FoupB => "Load Port 2",
        EquipmentRegion.FoupC => "Load Port 3",
        EquipmentRegion.Aligner => "Aligner",
        EquipmentRegion.SideStorage => "Side Storage",
        EquipmentRegion.NextProcessFoupA => "LP1 · 다음 FOUP",
        EquipmentRegion.NextProcessFoupB => "LP2 · 다음 FOUP",
        EquipmentRegion.NextProcessFoupC => "LP3 · 다음 FOUP",
        EquipmentRegion.ExternalProcess => "다음 공정",
        EquipmentRegion.ChamberA => "PM1 · Strip",
        EquipmentRegion.ChamberB => "PM2 · Etch",
        EquipmentRegion.ChamberC => "PM3 · Etch",
        EquipmentRegion.ChamberD => "PM4 · Etch",
        EquipmentRegion.LoadLock => "BM · Load Lock",
        EquipmentRegion.TM => "TM (진공)",
        _ => "-"
    };

    public static string FormatLabel(EquipmentRegion region) =>
        FormatLabel(region, TransferRobotKind.VacuumTm);
}

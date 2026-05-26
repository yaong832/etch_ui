using etch_ui.Equipment.Models;

namespace etch_ui.Equipment.Helpers;

public static class RegionAngleHelper
{
    /// <summary>라디안 → WPF RotateTransform용 도(시계 방향, Y-down 캔버스).</summary>
    public static double ToDegrees(EquipmentRegion region, bool hardwareIdleAt90 = false)
    {
        double rad = region switch
        {
            EquipmentRegion.ChamberA => Math.PI,
            EquipmentRegion.ChamberB => -Math.PI / 2,
            EquipmentRegion.ChamberC => 0,
            EquipmentRegion.FoupA => 3 * Math.PI / 4,
            EquipmentRegion.FoupB => Math.PI / 4,
            EquipmentRegion.LoadLock => Math.PI,
            EquipmentRegion.TM => hardwareIdleAt90 ? Math.PI / 2 : 3 * Math.PI / 4,
            _ => Math.PI / 2
        };

        return rad * 180.0 / Math.PI;
    }

    public static string FormatLabel(EquipmentRegion region) => region switch
    {
        EquipmentRegion.FoupA => "FOUP A",
        EquipmentRegion.FoupB => "FOUP B",
        EquipmentRegion.ChamberA => "Chamber A",
        EquipmentRegion.ChamberB => "Chamber B",
        EquipmentRegion.ChamberC => "Chamber C",
        EquipmentRegion.LoadLock => "Load Lock",
        EquipmentRegion.TM => "TM",
        _ => "-"
    };
}

using etch_ui.Equipment.Models;

namespace etch_ui.Equipment.Helpers;

/// <summary>이송 구간이 EFEM 대기압 TM vs 진공 TM 담당인지 판별.</summary>
public static class TransferLegClassifier
{
    public static bool IsAtmosphericRegion(EquipmentRegion region) =>
        region is EquipmentRegion.FoupA
            or EquipmentRegion.FoupB
            or EquipmentRegion.FoupC
            or EquipmentRegion.Aligner
            or EquipmentRegion.SideStorage
            or EquipmentRegion.ExternalProcess
            or EquipmentRegion.NextProcessFoupA
            or EquipmentRegion.NextProcessFoupB
            or EquipmentRegion.NextProcessFoupC
            or EquipmentRegion.EfemRobot;

    public static bool IsVacuumRegion(EquipmentRegion region) =>
        region is EquipmentRegion.TM
            or EquipmentRegion.LoadLock
            or EquipmentRegion.ChamberA
            or EquipmentRegion.ChamberB
            or EquipmentRegion.ChamberC
            or EquipmentRegion.ChamberD;

    /// <summary>pickup→dropoff 한 구간의 담당 로봇.</summary>
    public static TransferRobotKind RobotForLeg(EquipmentRegion pickup, EquipmentRegion dropoff)
    {
        if (IsVacuumChamberLeg(pickup, dropoff))
        {
            return TransferRobotKind.VacuumTm;
        }

        return TransferRobotKind.EfemAtmospheric;
    }

    private static bool IsVacuumChamberLeg(EquipmentRegion pickup, EquipmentRegion dropoff)
    {
        if (pickup == EquipmentRegion.LoadLock && IsPm(dropoff))
        {
            return true;
        }

        if (IsPm(pickup) && dropoff == EquipmentRegion.LoadLock)
        {
            return true;
        }

        return false;
    }

    private static bool IsPm(EquipmentRegion region) =>
        region is EquipmentRegion.ChamberA
            or EquipmentRegion.ChamberB
            or EquipmentRegion.ChamberC
            or EquipmentRegion.ChamberD;
}

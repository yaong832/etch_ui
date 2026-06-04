using etch_ui.Equipment.Models;



namespace etch_ui.Services.Scheduling;



/// <summary>클러스터 가상 장비 슬롯·FOUP·Side Stg 통합 상태.</summary>

public sealed class ClusterEquipmentState

{

    public ClusterEquipmentState(EquipmentCapacityConfig capacity)

    {

        Capacity = capacity;

        FoupPorts =

        [

            new FoupPortState(LoadPortId.Lp1),

            new FoupPortState(LoadPortId.Lp2),

            new FoupPortState(LoadPortId.Lp3)

        ];

        PickScheduler = new FoupPickScheduler(FoupPorts, capacity.FoupSlotCount);

        SideStorage = new SideStorageBuffer(capacity.SideStorageSlotCount);

        AlignerBuffer = new WaferSlotBuffer(capacity.AlignerSlotCount);

        LoadLockBuffer = new WaferSlotBuffer(capacity.LoadLockSlotCount);



        Pm1 = new PmChamberState { Region = EquipmentRegion.ChamberA, IsEtchPm = false };

        Pm2 = new PmChamberState { Region = EquipmentRegion.ChamberB, IsEtchPm = true };

        Pm3 = new PmChamberState { Region = EquipmentRegion.ChamberC, IsEtchPm = true };

        Pm4 = new PmChamberState { Region = EquipmentRegion.ChamberD, IsEtchPm = true };



        Chambers = new Dictionary<EquipmentRegion, PmChamberState>

        {

            [EquipmentRegion.ChamberA] = Pm1,

            [EquipmentRegion.ChamberB] = Pm2,

            [EquipmentRegion.ChamberC] = Pm3,

            [EquipmentRegion.ChamberD] = Pm4

        };

    }



    public EquipmentCapacityConfig Capacity { get; }

    public FoupPortState[] FoupPorts { get; }

    public FoupPickScheduler PickScheduler { get; }

    public SideStorageBuffer SideStorage { get; }

    public WaferSlotBuffer AlignerBuffer { get; }

    public WaferSlotBuffer LoadLockBuffer { get; }

    public PmChamberState Pm1 { get; }

    public PmChamberState Pm2 { get; }

    public PmChamberState Pm3 { get; }

    public PmChamberState Pm4 { get; }

    public IReadOnlyDictionary<EquipmentRegion, PmChamberState> Chambers { get; }

    public LotCompletionTracker Lot { get; } = new();

    public void ResetForDemo()

    {

        foreach (var port in FoupPorts)

        {

            port.IsMounted = true;

            port.RemainingInFoup = Capacity.FoupSlotCount;

            port.InFlightCount = 0;

        }



        AlignerBuffer.Clear();

        LoadLockBuffer.Clear();

        Pm1.ClearWafer();

        Pm2.ClearWafer();

        Pm3.ClearWafer();

        Pm4.ClearWafer();

        Pm1.ReservedForIncoming = false;

        Pm2.ReservedForIncoming = false;

        Pm3.ReservedForIncoming = false;

        Pm4.ReservedForIncoming = false;



        while (SideStorage.TryDequeue(out _))
        {
        }

        Lot.Reset(FoupPorts.Length * Capacity.FoupSlotCount);
    }

    /// <summary>Side Stg 25매 만석 시 카세트 교체 — 전량 출하 후 빈 카세트.</summary>
    public int PerformSideStorageCassetteSwap()
    {
        if (!SideStorage.IsFull)
        {
            return 0;
        }

        int shipped = 0;
        while (SideStorage.TryDequeue(out WaferTrack? wafer))
        {
            FoupPortState? port = FoupPorts.FirstOrDefault(p => p.PortId == wafer.OriginPort);
            port?.OnWaferLeftClusterToNextProcess();
            Lot.RecordWaferCompleted();
            shipped++;
        }

        return shipped;
    }

    public bool IsSideStorageAwaitingCassetteSwap => SideStorage.IsFull;

    public bool IsLotComplete()
    {
        if (!Lot.IsTargetMet)
        {
            return false;
        }

        foreach (FoupPortState port in FoupPorts)
        {
            if (port.RemainingInFoup > 0 || port.InFlightCount > 0)
            {
                return false;
            }
        }

        return !HasWafersInEquipment();
    }

    public bool HasWafersInEquipment() =>
        AlignerBuffer.HasWafer
        || LoadLockBuffer.HasWafer
        || SideStorage.HasWafer
        || Chambers.Values.Any(ch => ch.CurrentWafer is not null);

    public void DecrementProcessTimes()
    {
        AlignerBuffer.DecrementProcessTimes();



        foreach (PmChamberState ch in Chambers.Values)

        {

            if (ch.CurrentWafer is null || ch.RemainingProcessTicks <= 0)

            {

                continue;

            }



            ch.RemainingProcessTicks--;

            if (ch.RemainingProcessTicks != 0)

            {

                continue;

            }



            if (ch.IsEtchPm)

            {

                ch.CurrentWafer.HasCompletedEtch = true;

            }

            else if (ch.Region == EquipmentRegion.ChamberA)

            {

                ch.CurrentWafer.HasCompletedStrip = true;

            }

        }

    }



    public PmChamberState? GetChamber(EquipmentRegion region) =>

        Chambers.TryGetValue(region, out PmChamberState? ch) ? ch : null;



    public bool IsRegionReservedInQueue(EquipmentRegion region, IEnumerable<TransferJob> queued, TransferJob? active)

    {

        if (active is not null && (active.Pickup == region || active.Dropoff == region))

        {

            return true;

        }



        return queued.Any(j => j.Pickup == region || j.Dropoff == region);

    }

}



using System.Windows.Media;
using etch_ui.Equipment.Models;
using etch_ui.Equipment.ViewModels;

namespace etch_ui.Services;

/// <summary>
/// 메인 HMI 런타임 상태 → 장비 도식 ViewModel.
/// 하드웨어 서보(IEG3268) 연동 전: 도어(유도형)·상태·램프 기반. 서보 좌표는 2단계에서 연결.
/// </summary>
public sealed class EquipmentMotionBridge
{
    private readonly EquipmentMotionViewModel _motion;
    private EquipmentRegion _simTmTarget = EquipmentRegion.ChamberB;
    private int _simPhaseTick;
    private int _hwPollTick;

    public EquipmentMotionBridge(EquipmentMotionViewModel motion)
    {
        _motion = motion;
    }

    public void Sync(
        bool hasLiveData,
        bool useSimulation,
        bool ethercatConnected,
        bool doorClosed,
        bool doorInputValid,
        string equipmentState,
        bool lampReady,
        bool lampRun,
        bool lampWarn,
        bool lampAlarm)
    {
        _hwPollTick++;
        bool hwMode = hasLiveData && !useSimulation && ethercatConnected;

        _motion.LoadLockDoorClosed = !doorInputValid || doorClosed;
        _motion.ChamberADoorClosed = _motion.LoadLockDoorClosed;
        _motion.ChamberBDoorClosed = _motion.LoadLockDoorClosed;
        _motion.ChamberCDoorClosed = _motion.LoadLockDoorClosed;

        bool running = equipmentState.Equals("RUNNING", StringComparison.OrdinalIgnoreCase);
        bool warning = equipmentState.Equals("WARNING", StringComparison.OrdinalIgnoreCase);
        bool ready = equipmentState.Equals("READY", StringComparison.OrdinalIgnoreCase);

        UpdateChamberLamps(running, warning, ready, lampRun);

        EquipmentRegion region;
        double extension;
        bool carrying;

        if (hwMode)
        {
            // TODO: IEG3268 Axis1/2 → DetermineRegionFromPosition (SemiconductorUi 포팅)
            region = InferRegionFromProcessState(running, ready);
            extension = running ? 1.3 : 0.55;
            carrying = running;
            _motion.ServoHint = "EtherCAT · TM 논리 동기 (서보 좌표 2단계)";
        }
        else if (useSimulation && running)
        {
            _simPhaseTick++;
            if (_simPhaseTick % 120 == 0)
            {
                _simTmTarget = _simTmTarget switch
                {
                    EquipmentRegion.ChamberB => EquipmentRegion.ChamberA,
                    EquipmentRegion.ChamberA => EquipmentRegion.ChamberC,
                    EquipmentRegion.ChamberC => EquipmentRegion.FoupA,
                    _ => EquipmentRegion.ChamberB
                };
            }

            region = _simTmTarget;
            extension = 1.15;
            carrying = true;
            _motion.ServoHint = "시뮬 · TM 경로 데모";
        }
        else
        {
            region = ready ? EquipmentRegion.ChamberB : EquipmentRegion.TM;
            extension = 0.65;
            carrying = false;
            _motion.ServoHint = hasLiveData ? "대기" : "센서 미연결";
        }

        _motion.SetTargets(region, extension, carrying, hwMode);
        _motion.ChamberBHasWafer = running || ready;
        _motion.ChamberAHasWafer = running;
        _motion.ChamberCHasWafer = warning;
    }

    private void UpdateChamberLamps(bool running, bool warning, bool ready, bool lampRun)
    {
        if (running || lampRun)
        {
            _motion.SetChamberLamp(0, ChamberLampVisual.Processing);
            _motion.SetChamberLamp(1, ChamberLampVisual.Processing);
            _motion.SetChamberLamp(2, ChamberLampVisual.Off);
            return;
        }

        if (warning)
        {
            bool on = (_hwPollTick / 10) % 2 == 0;
            _motion.SetChamberLamp(0, on ? ChamberLampVisual.CompletedBlinkOn : ChamberLampVisual.CompletedBlinkOff);
            _motion.SetChamberLamp(1, ChamberLampVisual.Processing);
            _motion.SetChamberLamp(2, ChamberLampVisual.Off);
            return;
        }

        if (ready)
        {
            bool blink = (_hwPollTick / 10) % 2 == 0;
            _motion.SetChamberLamp(1, blink ? ChamberLampVisual.CompletedBlinkOn : ChamberLampVisual.CompletedBlinkOff);
            _motion.SetChamberLamp(0, ChamberLampVisual.Off);
            _motion.SetChamberLamp(2, ChamberLampVisual.Off);
            return;
        }

        _motion.SetChamberLamp(0, ChamberLampVisual.Off);
        _motion.SetChamberLamp(1, ChamberLampVisual.Off);
        _motion.SetChamberLamp(2, ChamberLampVisual.Off);
    }

    private static EquipmentRegion InferRegionFromProcessState(bool running, bool ready)
    {
        if (running)
        {
            return EquipmentRegion.ChamberB;
        }

        return ready ? EquipmentRegion.ChamberA : EquipmentRegion.TM;
    }
}

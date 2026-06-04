using System.Windows.Media;
using System.Windows.Threading;
using etch_ui.Equipment.Models;
using etch_ui.Equipment.ViewModels;

namespace etch_ui.Services;

/// <summary>TM 블레이드 각도·신장 보간 (WinForms TmVisualizationControl 16ms와 동일).</summary>
public sealed class EquipmentMotionAnimator : IDisposable
{
    private readonly EquipmentMotionViewModel _motion;
    private readonly DispatcherTimer _timer;
    private double _vacuumCurrentAngle;
    private double _vacuumCurrentExtension;
    private double _efemCurrentAngle;
    private double _efemCurrentExtension;
    private int _blinkTick;
    private readonly int[] _chamberBlinkPhase = new int[3];

    public EquipmentMotionAnimator(EquipmentMotionViewModel motion)
    {
        _motion = motion;
        _vacuumCurrentAngle = motion.VacuumBladeAngleDegrees;
        _vacuumCurrentExtension = motion.VacuumBladeExtension;
        _efemCurrentAngle = motion.EfemBladeAngleDegrees;
        _efemCurrentExtension = motion.EfemBladeExtension;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void PulseBlinkCounters()
    {
        for (int i = 0; i < 3; i++)
        {
            _chamberBlinkPhase[i]++;
        }
    }

    public void Dispose() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        _blinkTick++;
        _vacuumCurrentAngle = LerpAngle(_vacuumCurrentAngle, _motion.VacuumTargetAngleDegrees, 0.05);
        _vacuumCurrentExtension = Lerp(_vacuumCurrentExtension, _motion.VacuumTargetExtension, 0.075);

        _efemCurrentAngle = LerpAngle(_efemCurrentAngle, _motion.EfemTargetAngleDegrees, 0.05);
        _efemCurrentExtension = Lerp(_efemCurrentExtension, _motion.EfemTargetExtension, 0.075);

        _motion.ApplyInterpolatedFrame(
            _vacuumCurrentAngle,
            _vacuumCurrentExtension,
            _motion.VacuumTargetCarrying,
            _efemCurrentAngle,
            _efemCurrentExtension,
            _motion.EfemTargetCarrying);
    }

    private static double Lerp(double current, double target, double ratio) =>
        current + (target - current) * ratio;

    private static double LerpAngle(double current, double target, double ratio)
    {
        double diff = NormalizeAngleDiff(target - current);
        return current + diff * ratio;
    }

    private static double NormalizeAngleDiff(double diff)
    {
        while (diff > 180) diff -= 360;
        while (diff < -180) diff += 360;
        return diff;
    }
}

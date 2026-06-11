using System.Windows.Media;
using System.Windows.Threading;
using etch_ui.Equipment.Models;
using etch_ui.Equipment.ViewModels;

namespace etch_ui.Services;

/// <summary>TM 블레이드 각도·신장 보간 (WinForms TmVisualizationControl 16ms와 동일).</summary>
public sealed class EquipmentMotionAnimator : IDisposable
{
    /// <summary>좌·우 TM UI 보간 — 시뮬이 단계 내 각도·신장을 보간하므로 동일 계수 사용.</summary>
    private const double BladeAngleLerp = 0.18;
    private const double BladeExtendLerp = 0.18;
    private const double VacuumRotateAngleLerp = 0.42;

    private readonly EquipmentMotionViewModel _motion;
    private readonly Action? _onFrame;
    private readonly DispatcherTimer _timer;
    private double _vacuumCurrentAngle;
    private double _vacuumCurrentExtension;
    private double _efemCurrentAngle;
    private double _efemCurrentExtension;
    private int _blinkTick;
    private readonly int[] _chamberBlinkPhase = new int[3];

    public EquipmentMotionAnimator(EquipmentMotionViewModel motion, Action? onFrame = null)
    {
        _motion = motion;
        _onFrame = onFrame;
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
        _onFrame?.Invoke();
        _blinkTick++;
        double vacAngleLerp = _motion.VacuumIsRotatingBlade ? VacuumRotateAngleLerp : BladeAngleLerp;
        _vacuumCurrentAngle = LerpAngle(_vacuumCurrentAngle, _motion.VacuumTargetAngleDegrees, vacAngleLerp);
        _vacuumCurrentExtension = Lerp(_vacuumCurrentExtension, _motion.VacuumTargetExtension, BladeExtendLerp);

        double efemAngleLerp = _motion.EfemIsRotatingBlade ? VacuumRotateAngleLerp : BladeAngleLerp;
        _efemCurrentAngle = LerpAngle(_efemCurrentAngle, _motion.EfemTargetAngleDegrees, efemAngleLerp);
        _efemCurrentExtension = Lerp(_efemCurrentExtension, _motion.EfemTargetExtension, BladeExtendLerp);

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

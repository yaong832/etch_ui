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
    private double _currentAngle;
    private double _currentExtension;
    private int _blinkTick;
    private readonly int[] _chamberBlinkPhase = new int[3];

    public EquipmentMotionAnimator(EquipmentMotionViewModel motion)
    {
        _motion = motion;
        _currentAngle = motion.BladeAngleDegrees;
        _currentExtension = motion.BladeExtension;
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
        double angleDiff = NormalizeAngleDiff(_motion.TargetAngleDegrees - _currentAngle);
        _currentAngle += angleDiff * 0.2;
        _currentExtension += (_motion.TargetExtension - _currentExtension) * 0.4;
        bool carrying = _motion.TargetCarrying;
        _motion.ApplyInterpolatedFrame(_currentAngle, _currentExtension, carrying);
    }

    private static double NormalizeAngleDiff(double diff)
    {
        while (diff > 180) diff -= 360;
        while (diff < -180) diff += 360;
        return diff;
    }
}

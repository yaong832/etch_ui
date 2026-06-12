using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace etch_ui.Equipment.Views;

/// <summary>EFEM·진공 TM 블레이드(팔·패들·포크) 도식 스타일.</summary>
internal static class BladeVisualHelper
{
    public const double ArmHeight = 7;
    public const double PaddleHeight = 13;
    public const double PaddleWidth = 24;
    public const double TipOverlap = 9;
    public const double ProngWidth = 2.4;
    public const double ProngHeight = 9;

    public static void ApplyEfemChrome(
        Shape hub,
        Rectangle railFront,
        Rectangle railBack,
        Rectangle railTop,
        Rectangle railBottom,
        Rectangle tipFront,
        Rectangle tipBack,
        Rectangle tipTop,
        Rectangle tipBottom,
        Rectangle centerPad,
        Rectangle slotA,
        Rectangle slotB,
        Rectangle prongFrontL,
        Rectangle prongFrontR,
        Rectangle prongBackL,
        Rectangle prongBackR)
    {
        ApplyHub(hub, Color.FromRgb(0x0F, 0x76, 0x6E), Color.FromRgb(0x5E, 0xEA, 0xD4));
        ApplyArm(railFront, railBack, railTop, railBottom, "#2F3D45", "#5B6B73", "#8FA4AD");
        ApplyPaddle(tipFront, tipBack, tipTop, tipBottom, centerPad, "#3D4F58", "#6E848E", "#A8BEC8");
        ApplyProngs(prongFrontL, prongFrontR, prongBackL, prongBackR, "#7A9099");
        ApplyWaferSeat(slotA, slotB, "#134E4A", "#5EEAD4");
    }

    public static void ApplyVacuumChrome(
        Shape hub,
        Rectangle railFront,
        Rectangle railBack,
        Rectangle railTop,
        Rectangle railBottom,
        Rectangle tipFront,
        Rectangle tipBack,
        Rectangle tipTop,
        Rectangle tipBottom,
        Rectangle centerPad,
        Rectangle slotA,
        Rectangle slotB,
        Rectangle prongFrontL,
        Rectangle prongFrontR,
        Rectangle prongBackL,
        Rectangle prongBackR)
    {
        ApplyHub(hub, Color.FromRgb(0x47, 0x55, 0x69), Color.FromRgb(0xCB, 0xD5, 0xE1));
        ApplyArm(railFront, railBack, railTop, railBottom, "#2B3540", "#4B5A66", "#7B8C98");
        ApplyPaddle(tipFront, tipBack, tipTop, tipBottom, centerPad, "#35424C", "#5C6D79", "#93A6B3");
        ApplyProngs(prongFrontL, prongFrontR, prongBackL, prongBackR, "#6B7C88");
        ApplyWaferSeat(slotA, slotB, "#1E293B", "#38BDF8");
    }

    private static void ApplyHub(Shape hub, Color core, Color rim)
    {
        hub.Fill = new RadialGradientBrush(core, rim) { RadiusX = 0.85, RadiusY = 0.85 };
        hub.Stroke = new SolidColorBrush(Color.FromArgb(200, 15, 23, 42));
        hub.StrokeThickness = 1.1;
    }

    private static void ApplyArm(
        Rectangle railFront,
        Rectangle railBack,
        Rectangle railTop,
        Rectangle railBottom,
        string dark,
        string mid,
        string light)
    {
        Brush brush = CreateLinearBrush(dark, mid, light, isVertical: true);
        foreach (Rectangle rail in new[] { railFront, railBack, railTop, railBottom })
        {
            rail.Fill = brush;
            rail.Stroke = new SolidColorBrush(Color.FromArgb(160, 15, 23, 42));
            rail.StrokeThickness = 0.7;
            rail.RadiusX = 1.5;
            rail.RadiusY = 1.5;
        }
    }

    private static void ApplyPaddle(
        Rectangle tipFront,
        Rectangle tipBack,
        Rectangle tipTop,
        Rectangle tipBottom,
        Rectangle centerPad,
        string dark,
        string mid,
        string light)
    {
        Brush brush = CreateLinearBrush(dark, mid, light, isVertical: true);
        foreach (Rectangle tip in new[] { tipFront, tipBack, tipTop, tipBottom, centerPad })
        {
            tip.Fill = brush;
            tip.Stroke = new SolidColorBrush(Color.FromArgb(170, 15, 23, 42));
            tip.StrokeThickness = 0.8;
            tip.RadiusX = 2.5;
            tip.RadiusY = 2.5;
        }
    }

    private static void ApplyProngs(Rectangle frontL, Rectangle frontR, Rectangle backL, Rectangle backR, string fill)
    {
        foreach (Rectangle prong in new[] { frontL, frontR, backL, backR })
        {
            ApplyProng(prong, fill);
        }
    }

    public static void ApplyProng(Rectangle prong, string fill)
    {
        prong.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fill)!);
        prong.Stroke = new SolidColorBrush(Color.FromArgb(140, 15, 23, 42));
        prong.StrokeThickness = 0.5;
        prong.RadiusX = 0.8;
        prong.RadiusY = 0.8;
    }

    private static void ApplyWaferSeat(Rectangle slotA, Rectangle slotB, string fill, string stroke)
    {
        foreach (Rectangle slot in new[] { slotA, slotB })
        {
            slot.Fill = Brushes.Transparent;
            slot.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stroke)!);
            slot.StrokeThickness = 1.1;
            slot.StrokeDashArray = new DoubleCollection { 2.2, 1.4 };
            slot.RadiusX = 6;
            slot.RadiusY = 6;
            slot.Width = 16;
            slot.Height = 16;
            slot.Opacity = 0.85;
        }
    }

    private static LinearGradientBrush CreateLinearBrush(string dark, string mid, string light, bool isVertical)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = isVertical ? new Point(0, 0) : new Point(0, 0.5),
            EndPoint = isVertical ? new Point(0, 1) : new Point(1, 0.5)
        };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(dark)!, 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(mid)!, 0.45));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(light)!, 1));
        return brush;
    }

    public static void LayoutFrontPaddle(
        double hubX,
        double armLen,
        double armY,
        Rectangle paddle,
        Rectangle prongL,
        Rectangle prongR,
        Rectangle? slot,
        TextBlock? label,
        double slotTipInset)
    {
        double paddleLeft = hubX + armLen - TipOverlap;
        Canvas.SetLeft(paddle, paddleLeft);
        Canvas.SetTop(paddle, armY);
        paddle.Width = PaddleWidth;
        paddle.Height = PaddleHeight;

        double prongY = armY + (PaddleHeight - ProngHeight) / 2;
        Canvas.SetLeft(prongL, paddleLeft + PaddleWidth - 17);
        Canvas.SetTop(prongL, prongY);
        prongL.Width = ProngWidth;
        prongL.Height = ProngHeight;
        Canvas.SetLeft(prongR, paddleLeft + PaddleWidth - 7);
        Canvas.SetTop(prongR, prongY);
        prongR.Width = ProngWidth;
        prongR.Height = ProngHeight;

        if (slot is null)
        {
            return;
        }

        double slotCenter = hubX + armLen - slotTipInset;
        Canvas.SetLeft(slot, slotCenter - slot.Width / 2);
        Canvas.SetTop(slot, armY + (PaddleHeight - slot.Height) / 2);
        if (label is not null)
        {
            Canvas.SetLeft(label, slotCenter - 12);
            Canvas.SetTop(label, armY - 13);
        }
    }

    public static void LayoutBackPaddle(
        double hubX,
        double armLen,
        double armY,
        Rectangle paddle,
        Rectangle prongL,
        Rectangle prongR,
        Rectangle slot,
        TextBlock? label,
        double slotTipInset)
    {
        double paddleLeft = hubX - armLen - PaddleWidth + TipOverlap;
        Canvas.SetLeft(paddle, paddleLeft);
        Canvas.SetTop(paddle, armY);
        paddle.Width = PaddleWidth;
        paddle.Height = PaddleHeight;

        double prongY = armY + (PaddleHeight - ProngHeight) / 2;
        Canvas.SetLeft(prongL, paddleLeft + 5);
        Canvas.SetTop(prongL, prongY);
        prongL.Width = ProngWidth;
        prongL.Height = ProngHeight;
        Canvas.SetLeft(prongR, paddleLeft + 15);
        Canvas.SetTop(prongR, prongY);
        prongR.Width = ProngWidth;
        prongR.Height = ProngHeight;

        double slotCenter = hubX - armLen + slotTipInset;
        Canvas.SetLeft(slot, slotCenter - slot.Width / 2);
        Canvas.SetTop(slot, armY + (PaddleHeight - slot.Height) / 2);
        if (label is not null)
        {
            Canvas.SetLeft(label, slotCenter - 12);
            Canvas.SetTop(label, armY - 13);
        }
    }

    public static double ArmTop(double armY) => armY + (PaddleHeight - ArmHeight) / 2;
}

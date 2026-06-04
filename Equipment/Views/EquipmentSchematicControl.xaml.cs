using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using etch_ui.Equipment.Layout;
using etch_ui.Equipment.ViewModels;

namespace etch_ui.Equipment.Views;

public partial class EquipmentSchematicControl : UserControl
{
    private const double TmBaseBlade = 56;
    private const double TmMaxBlade = 178;
    private const double TmRetractBlade = 48;
    private const double EfemBaseBlade = 48;
    private const double EfemMaxBlade = 132;
    private const double EfemHubX = 150;
    private const double VacHubX = 170;
    private const double BladeWaferDiameter = 14;
    private const double EfemWaferDiameter = 12;
    private EquipmentMotionViewModel? _bound;

    public EquipmentSchematicControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += (_, _) => HookMotion();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RepositionAll();
        DrawTracks();
        HookMotion();
    }

    private void HookMotion()
    {
        if (_bound != null)
        {
            _bound.PropertyChanged -= OnMotionPropertyChanged;
        }

        _bound = DataContext as EquipmentMotionViewModel;
        if (_bound != null)
        {
            _bound.PropertyChanged += OnMotionPropertyChanged;
            RefreshRobots();
        }
    }

    private void OnMotionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EquipmentMotionViewModel.VacuumBladeAngleDegrees)
            or nameof(EquipmentMotionViewModel.VacuumBladeExtension)
            or nameof(EquipmentMotionViewModel.VacuumCarryingWafer)
            or nameof(EquipmentMotionViewModel.VacuumBladeSlotA)
            or nameof(EquipmentMotionViewModel.VacuumBladeSlotB)
            or nameof(EquipmentMotionViewModel.VacuumActiveBladeSlot)
            or nameof(EquipmentMotionViewModel.VacuumIsRotatingBlade)
            or nameof(EquipmentMotionViewModel.EfemBladeAngleDegrees)
            or nameof(EquipmentMotionViewModel.EfemBladeExtension)
            or nameof(EquipmentMotionViewModel.EfemCarryingWafer)
            or nameof(EquipmentMotionViewModel.EfemBladeSlotA)
            or nameof(EquipmentMotionViewModel.EfemBladeSlotB)
            or nameof(EquipmentMotionViewModel.EfemActiveBladeSlot)
            or nameof(EquipmentMotionViewModel.EfemBladeCapacity)
            or nameof(EquipmentMotionViewModel.IsVacuumDualBlade)
            or nameof(EquipmentMotionViewModel.VacuumBladeCapacity)
            or nameof(EquipmentMotionViewModel.IsEfemDualBlade)
            or nameof(EquipmentMotionViewModel.IsEfemRobotActive)
            or nameof(EquipmentMotionViewModel.IsVacuumTmActive))
        {
            RefreshRobots();
        }
    }

    private void RefreshRobots()
    {
        if (_bound == null)
        {
            return;
        }

        EfemRobotHost.Visibility = Visibility.Visible;
        TmHost.Visibility = Visibility.Visible;
        EfemRobotHost.Opacity = _bound.IsEfemRobotActive ? 1.0 : 0.45;
        TmHost.Opacity = _bound.IsVacuumTmActive ? 1.0 : 0.45;

        RefreshEfemRobot();
        RefreshVacuumRobot();
    }

    private void RefreshEfemRobot()
    {
        if (_bound == null)
        {
            return;
        }

        const double armY = 15;
        const double armH = 10;
        const double slotTipInset = 10;

        double efemNorm = Math.Clamp((_bound.EfemBladeExtension - 0.4) / 1.2, 0, 1);
        EfemRotate.Angle = _bound.EfemBladeAngleDegrees;
        double efemLenExtend = EfemBaseBlade + efemNorm * (EfemMaxBlade - EfemBaseBlade);

        if (_bound.IsEfemDualBlade)
        {
            SetSingleBladeVis(
                EfemBladeRailTop,
                EfemBladeRailBottom,
                EfemBladeTipTop,
                EfemBladeTipBottom,
                EfemBladeCenterPad,
                Visibility.Collapsed);
            SetDualBladeVis(
                EfemBladeHubPad,
                EfemBladeRailFront,
                EfemBladeRailBack,
                EfemBladeTipFront,
                EfemBladeTipBack,
                EfemBladeSlotA,
                EfemBladeSlotB,
                EfemBladeSlotALabel,
                EfemBladeSlotBLabel,
                Visibility.Visible);

            EfemBladeSlotA.Opacity = _bound.EfemActiveBladeSlot == 0 || _bound.EfemBladeSlotA ? 0.9 : 0.35;
            EfemBladeSlotB.Opacity = _bound.EfemActiveBladeSlot == 1 || _bound.EfemBladeSlotB ? 0.9 : 0.35;

            bool efemBusy = _bound.IsEfemRobotActive;
            int activeSlot = _bound.EfemActiveBladeSlot;
            double frontLen = ResolveArmLength(efemBusy, activeSlot == 1, efemLenExtend);
            double backLen = ResolveArmLength(efemBusy, activeSlot == 0, efemLenExtend);

            LayoutSymmetricDualArm(
                front: true,
                frontLen,
                backLen,
                EfemHubX,
                armY,
                armH,
                slotTipInset,
                EfemBladeRailFront,
                EfemBladeRailBack,
                EfemBladeTipFront,
                EfemBladeTipBack,
                EfemBladeSlotA,
                EfemBladeSlotB,
                EfemBladeSlotALabel,
                EfemBladeSlotBLabel);

            double waferCenterY = armY + armH / 2;
            if (_bound.EfemBladeSlotB)
            {
                PlaceWaferDisc(WaferOnEfemBladeB, EfemHubX + frontLen - slotTipInset, waferCenterY, BladeWaferDiameter);
            }

            if (_bound.EfemBladeSlotA)
            {
                PlaceWaferDisc(WaferOnEfemBlade, EfemHubX - backLen + slotTipInset, waferCenterY, BladeWaferDiameter);
            }
        }
        else
        {
            SetDualBladeVis(
                EfemBladeHubPad,
                EfemBladeRailFront,
                EfemBladeRailBack,
                EfemBladeTipFront,
                EfemBladeTipBack,
                EfemBladeSlotA,
                EfemBladeSlotB,
                EfemBladeSlotALabel,
                EfemBladeSlotBLabel,
                Visibility.Collapsed);
            SetSingleBladeVis(
                EfemBladeRailTop,
                EfemBladeRailBottom,
                EfemBladeTipTop,
                EfemBladeTipBottom,
                EfemBladeCenterPad,
                Visibility.Visible);

            double efemLen = efemLenExtend;
            EfemBladeRailTop.Width = efemLen;
            EfemBladeRailBottom.Visibility = Visibility.Collapsed;
            EfemBladeTipBottom.Visibility = Visibility.Collapsed;
            Canvas.SetLeft(EfemBladeRailTop, EfemHubX);
            Canvas.SetTop(EfemBladeRailTop, 8);
            Canvas.SetLeft(EfemBladeTipTop, EfemHubX + efemLen - 6);
            Canvas.SetLeft(EfemBladeCenterPad, EfemHubX + efemLen - 14);
            double slotLeft = EfemHubX + efemLen - 4;
            if (_bound.EfemBladeSlotA || _bound.EfemCarryingWafer)
            {
                PlaceWaferDisc(WaferOnEfemBlade, slotLeft, 8 + 5, BladeWaferDiameter);
            }

            WaferOnEfemBladeB.Visibility = Visibility.Collapsed;
        }

        Panel.SetZIndex(WaferOnEfemBlade, 20);
        Panel.SetZIndex(WaferOnEfemBladeB, 20);
        bool showEfemA = _bound.EfemBladeSlotA
            || (!_bound.IsEfemDualBlade && _bound.EfemCarryingWafer);
        WaferOnEfemBlade.Visibility = showEfemA ? Visibility.Visible : Visibility.Collapsed;
        WaferOnEfemBladeB.Visibility = _bound.EfemBladeSlotB ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshVacuumRobot()
    {
        if (_bound == null)
        {
            return;
        }

        const double vacArmY = 15;
        const double vacArmH = 10;
        const double slotTipInset = 10;

        double vacNorm = Math.Clamp((_bound.VacuumBladeExtension - 0.4) / 1.2, 0, 1);
        TmRotate.Angle = _bound.VacuumBladeAngleDegrees;
        double vacLenExtend = TmBaseBlade + vacNorm * (TmMaxBlade - TmBaseBlade);
        bool dualVac = _bound.IsVacuumDualBlade;

        if (dualVac)
        {
            SetSingleBladeVis(
                BladeRailTop,
                BladeRailBottom,
                BladeTipTop,
                BladeTipBottom,
                BladeCenterPad,
                Visibility.Collapsed);
            SetDualBladeVis(
                BladeHubPad,
                BladeRailFront,
                BladeRailBack,
                BladeTipFront,
                BladeTipBack,
                BladeSlotA,
                BladeSlotB,
                BladeSlotALabel,
                BladeSlotBLabel,
                Visibility.Visible);
            BladeSlotA.Opacity = _bound.VacuumActiveBladeSlot == 0 || _bound.VacuumBladeSlotA ? 0.9 : 0.35;
            BladeSlotB.Opacity = _bound.VacuumActiveBladeSlot == 1 || _bound.VacuumBladeSlotB ? 0.9 : 0.35;
            BladeHubPad.Opacity = _bound.VacuumIsRotatingBlade ? 0.6 : 0.95;

            bool vacBusy = _bound.IsVacuumTmActive;
            int activeSlot = _bound.VacuumActiveBladeSlot;
            double frontLen = ResolveArmLength(vacBusy, activeSlot == 1, vacLenExtend);
            double backLen = ResolveArmLength(vacBusy, activeSlot == 0, vacLenExtend);

            LayoutSymmetricDualArm(
                front: true,
                frontLen,
                backLen,
                VacHubX,
                vacArmY,
                vacArmH,
                slotTipInset,
                BladeRailFront,
                BladeRailBack,
                BladeTipFront,
                BladeTipBack,
                BladeSlotA,
                BladeSlotB,
                BladeSlotALabel,
                BladeSlotBLabel);

            double waferCenterY = vacArmY + vacArmH / 2;
            if (_bound.VacuumBladeSlotB)
            {
                PlaceWaferDisc(WaferOnBladeB, VacHubX + frontLen - slotTipInset, waferCenterY, BladeWaferDiameter);
            }

            if (_bound.VacuumBladeSlotA)
            {
                PlaceWaferDisc(WaferOnBlade, VacHubX - backLen + slotTipInset, waferCenterY, BladeWaferDiameter);
            }
        }
        else
        {
            SetDualBladeVis(
                BladeHubPad,
                BladeRailFront,
                BladeRailBack,
                BladeTipFront,
                BladeTipBack,
                BladeSlotA,
                BladeSlotB,
                BladeSlotALabel,
                BladeSlotBLabel,
                Visibility.Collapsed);
            SetSingleBladeVis(
                BladeRailTop,
                BladeRailBottom,
                BladeTipTop,
                BladeTipBottom,
                BladeCenterPad,
                Visibility.Visible);

            double vacLen = vacLenExtend;
            BladeRailTop.Width = vacLen;
            Canvas.SetLeft(BladeRailTop, VacHubX);
            Canvas.SetTop(BladeRailTop, 8);
            double tipLeft = VacHubX + vacLen - 6;
            Canvas.SetLeft(BladeTipTop, tipLeft);
            Canvas.SetLeft(BladeCenterPad, VacHubX + vacLen - 14);
            double slotLeft = VacHubX + vacLen - 4;
            BladeSlotB.Visibility = Visibility.Collapsed;

            if (_bound.VacuumBladeSlotA || _bound.VacuumCarryingWafer)
            {
                PlaceWaferDisc(WaferOnBlade, slotLeft, 8 + 5, BladeWaferDiameter);
            }

            WaferOnBladeB.Visibility = Visibility.Collapsed;
        }

        Panel.SetZIndex(WaferOnBlade, 30);
        Panel.SetZIndex(WaferOnBladeB, 30);
        bool showSlotA = _bound.VacuumBladeSlotA
            || (!_bound.IsVacuumDualBlade && _bound.VacuumCarryingWafer);
        WaferOnBlade.Visibility = showSlotA ? Visibility.Visible : Visibility.Collapsed;
        WaferOnBladeB.Visibility = _bound.VacuumBladeSlotB ? Visibility.Visible : Visibility.Collapsed;
    }

    private static double ResolveArmLength(bool robotBusy, bool isActiveArm, double extendLen)
    {
        if (!robotBusy)
        {
            return TmBaseBlade;
        }

        return isActiveArm ? extendLen : TmRetractBlade;
    }

    private static void SetSingleBladeVis(
        Rectangle railTop,
        Rectangle railBottom,
        Rectangle tipTop,
        Rectangle tipBottom,
        Rectangle centerPad,
        Visibility visibility)
    {
        railTop.Visibility = visibility;
        railBottom.Visibility = visibility;
        tipTop.Visibility = visibility;
        tipBottom.Visibility = visibility;
        centerPad.Visibility = visibility;
    }

    private static void SetDualBladeVis(
        Rectangle hub,
        Rectangle railFront,
        Rectangle railBack,
        Rectangle tipFront,
        Rectangle tipBack,
        Rectangle slotA,
        Rectangle slotB,
        TextBlock labelA,
        TextBlock labelB,
        Visibility visibility)
    {
        hub.Visibility = visibility;
        railFront.Visibility = visibility;
        railBack.Visibility = visibility;
        tipFront.Visibility = visibility;
        tipBack.Visibility = visibility;
        slotA.Visibility = visibility;
        slotB.Visibility = visibility;
        labelA.Visibility = visibility;
        labelB.Visibility = visibility;
    }

    private static void LayoutSymmetricDualArm(
        bool front,
        double frontLen,
        double backLen,
        double hubX,
        double armY,
        double armH,
        double slotTipInset,
        Rectangle railFront,
        Rectangle railBack,
        Rectangle tipFront,
        Rectangle tipBack,
        Rectangle slotA,
        Rectangle slotB,
        TextBlock labelA,
        TextBlock labelB)
    {
        railFront.Width = frontLen;
        railFront.Height = armH;
        Canvas.SetLeft(railFront, hubX);
        Canvas.SetTop(railFront, armY);
        Canvas.SetLeft(tipFront, hubX + frontLen - 8);
        Canvas.SetTop(tipFront, armY - 1);
        Canvas.SetLeft(slotB, hubX + frontLen - slotTipInset);
        Canvas.SetTop(slotB, armY - 1);
        Canvas.SetLeft(labelB, hubX + frontLen - slotTipInset);
        Canvas.SetTop(labelB, armY - 13);

        railBack.Width = backLen;
        railBack.Height = armH;
        Canvas.SetLeft(railBack, hubX - backLen);
        Canvas.SetTop(railBack, armY);
        Canvas.SetLeft(tipBack, hubX - backLen - 16);
        Canvas.SetTop(tipBack, armY - 1);
        Canvas.SetLeft(slotA, hubX - backLen + slotTipInset - 14);
        Canvas.SetTop(slotA, armY - 1);
        Canvas.SetLeft(labelA, hubX - backLen + slotTipInset - 14);
        Canvas.SetTop(labelA, armY - 13);

        _ = front;
    }

    private static void PlaceWaferDisc(Ellipse wafer, double centerX, double centerY, double diameter)
    {
        wafer.Width = diameter;
        wafer.Height = diameter;
        Canvas.SetLeft(wafer, centerX - diameter / 2);
        Canvas.SetTop(wafer, centerY - diameter / 2);
    }

    private void RepositionAll()
    {
        Place(EfemBox, EquipmentLayoutMetrics.EfemPosition, EquipmentLayoutMetrics.EfemSize);
        Place(AlignerBox, EquipmentLayoutMetrics.AlignerPosition, EquipmentLayoutMetrics.AlignerSize);
        Place(SideStorageBox, EquipmentLayoutMetrics.SideStoragePosition, EquipmentLayoutMetrics.SideStorageSize);
        Place(LoadPort1Box, EquipmentLayoutMetrics.LoadPort1Position, EquipmentLayoutMetrics.LoadPortSize);
        Place(LoadPort2Box, EquipmentLayoutMetrics.LoadPort2Position, EquipmentLayoutMetrics.LoadPortSize);
        Place(LoadPort3Box, EquipmentLayoutMetrics.LoadPort3Position, EquipmentLayoutMetrics.LoadPortSize);
        Place(LoadLockBox, EquipmentLayoutMetrics.BufferPosition, EquipmentLayoutMetrics.BufferSize);
        Place(ChamberABox, EquipmentLayoutMetrics.Pm1Position, EquipmentLayoutMetrics.PmSize);
        Place(ChamberBBox, EquipmentLayoutMetrics.Pm2Position, EquipmentLayoutMetrics.PmSize);
        Place(ChamberCBox, EquipmentLayoutMetrics.Pm3Position, EquipmentLayoutMetrics.PmSize);
        Place(Pm4Box, EquipmentLayoutMetrics.Pm4Position, EquipmentLayoutMetrics.PmSize);

        Point efemTopLeft = EquipmentLayoutMetrics.EfemRobotHostTopLeft;
        Canvas.SetLeft(EfemRobotHost, efemTopLeft.X);
        Canvas.SetTop(EfemRobotHost, efemTopLeft.Y);

        Point tmTopLeft = EquipmentLayoutMetrics.TmHostTopLeft;
        Canvas.SetLeft(TmHost, tmTopLeft.X);
        Canvas.SetTop(TmHost, tmTopLeft.Y);
        Canvas.SetLeft(TmLabel, EquipmentLayoutMetrics.TmCenter.X - 48);
        Canvas.SetTop(TmLabel, EquipmentLayoutMetrics.TmCenter.Y + EquipmentLayoutMetrics.TmPivot + 8);
    }

    private static void Place(FrameworkElement el, Point pos, Size size)
    {
        el.Width = size.Width;
        el.Height = size.Height;
        Canvas.SetLeft(el, pos.X);
        Canvas.SetTop(el, pos.Y);
    }

    private void DrawTracks()
    {
        Point lp1 = EquipmentLayoutMetrics.GetPortCenter(Equipment.Models.EquipmentRegion.FoupA);
        Point efemPivot = EquipmentLayoutMetrics.EfemRobotCenter;
        Point bm = EquipmentLayoutMetrics.GetPortCenter(Equipment.Models.EquipmentRegion.LoadLock);
        Point tm = EquipmentLayoutMetrics.TmCenter;
        Point[] main = [lp1, efemPivot, bm, tm];
        MainTrack.Points = new PointCollection(main);
        MainTrackDash.Points = new PointCollection(main);

        PmSpokesLayer.Children.Clear();
        foreach (var region in new[]
                 {
                     Equipment.Models.EquipmentRegion.ChamberA,
                     Equipment.Models.EquipmentRegion.ChamberB,
                     Equipment.Models.EquipmentRegion.ChamberC,
                     Equipment.Models.EquipmentRegion.ChamberD
                 })
        {
            Point pm = EquipmentLayoutMetrics.GetPortCenter(region);
            PmSpokesLayer.Children.Add(new Line
            {
                X1 = tm.X,
                Y1 = tm.Y,
                X2 = pm.X,
                Y2 = pm.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(90, 37, 99, 235)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 6 }
            });
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using etch_ui.Equipment.Layout;
using etch_ui.Equipment.ViewModels;

namespace etch_ui.Equipment.Views;

public partial class EquipmentSchematicControl : UserControl
{
    private const double TmBaseBlade = 50;
    private const double TmMaxBlade = 168;
    private const double TmRetractBlade = 28;
    private const double EfemBaseBlade = 36;
    private const double EfemMaxBlade = 118;
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

        double efemNorm = Math.Clamp((_bound.EfemBladeExtension - 0.4) / 1.2, 0, 1);
        EfemRotate.Angle = _bound.EfemBladeAngleDegrees;
        double efemLen = EfemBaseBlade + efemNorm * (EfemMaxBlade - EfemBaseBlade);
        EfemBladeRailTop.Width = efemLen;
        EfemBladeRailBottom.Width = EfemBaseBlade;
        Canvas.SetLeft(EfemBladeTipTop, Math.Max(0, efemLen - 2));
        Canvas.SetLeft(EfemBladeTipBottom, Math.Max(0, EfemBaseBlade - 2));
        Canvas.SetLeft(EfemBladeCenterPad, Math.Max(0, efemLen - 8));
        double efemWaferCenterX = Math.Max(EfemWaferDiameter / 2, efemLen - 2);
        double efemWaferCenterY = 6 + EfemWaferDiameter / 2;
        PlaceWaferDisc(WaferOnEfemBlade, efemWaferCenterX, efemWaferCenterY, EfemWaferDiameter);
        WaferOnEfemBlade.Visibility = _bound.EfemCarryingWafer ? Visibility.Visible : Visibility.Collapsed;
        WaferOnEfemBladeB.Visibility = Visibility.Collapsed;
        Panel.SetZIndex(WaferOnEfemBlade, 20);
    }

    private void RefreshVacuumRobot()
    {
        if (_bound == null)
        {
            return;
        }

        const double vacHubX = 170;
        const double vacArmY = 15;
        const double vacArmH = 10;
        const double slotTipInset = 10;

        double vacNorm = Math.Clamp((_bound.VacuumBladeExtension - 0.4) / 1.2, 0, 1);
        TmRotate.Angle = _bound.VacuumBladeAngleDegrees;
        double vacLenExtend = TmBaseBlade + vacNorm * (TmMaxBlade - TmBaseBlade);
        bool dualVac = _bound.IsVacuumDualBlade;

        if (dualVac)
        {
            Visibility singleHidden = Visibility.Collapsed;
            BladeRailTop.Visibility = singleHidden;
            BladeRailBottom.Visibility = singleHidden;
            BladeTipTop.Visibility = singleHidden;
            BladeTipBottom.Visibility = singleHidden;
            BladeCenterPad.Visibility = singleHidden;

            Visibility dualVis = Visibility.Visible;
            BladeHubPad.Visibility = dualVis;
            BladeRailFront.Visibility = dualVis;
            BladeRailBack.Visibility = dualVis;
            BladeTipFront.Visibility = dualVis;
            BladeTipBack.Visibility = dualVis;
            BladeSlotA.Visibility = dualVis;
            BladeSlotB.Visibility = dualVis;
            BladeSlotALabel.Visibility = dualVis;
            BladeSlotBLabel.Visibility = dualVis;
            BladeSlotA.Opacity = _bound.VacuumActiveBladeSlot == 0 || _bound.VacuumBladeSlotA ? 0.9 : 0.35;
            BladeSlotB.Opacity = _bound.VacuumActiveBladeSlot == 1 || _bound.VacuumBladeSlotB ? 0.9 : 0.35;
            BladeHubPad.Opacity = _bound.VacuumIsRotatingBlade ? 0.6 : 0.95;

            bool vacBusy = _bound.IsVacuumTmActive;
            int activeSlot = _bound.VacuumActiveBladeSlot;
            double frontLen = vacBusy && activeSlot == 1 ? vacLenExtend : TmRetractBlade;
            double backLen = vacBusy && activeSlot == 0 ? vacLenExtend : TmRetractBlade;

            LayoutDualVacuumArm(
                front: true,
                armLen: frontLen,
                vacHubX,
                vacArmY,
                vacArmH,
                slotTipInset);
            LayoutDualVacuumArm(
                front: false,
                armLen: backLen,
                vacHubX,
                vacArmY,
                vacArmH,
                slotTipInset);

            double waferCenterY = vacArmY + vacArmH / 2;
            if (_bound.VacuumBladeSlotB)
            {
                double frontTipX = vacHubX + frontLen - slotTipInset;
                PlaceWaferDisc(WaferOnBladeB, frontTipX, waferCenterY, BladeWaferDiameter);
            }

            if (_bound.VacuumBladeSlotA)
            {
                double backTipX = vacHubX - backLen + slotTipInset;
                PlaceWaferDisc(WaferOnBlade, backTipX, waferCenterY, BladeWaferDiameter);
            }
        }
        else
        {
            Visibility dualHidden = Visibility.Collapsed;
            BladeHubPad.Visibility = dualHidden;
            BladeRailFront.Visibility = dualHidden;
            BladeRailBack.Visibility = dualHidden;
            BladeTipFront.Visibility = dualHidden;
            BladeTipBack.Visibility = dualHidden;
            BladeSlotA.Visibility = dualHidden;
            BladeSlotB.Visibility = dualHidden;
            BladeSlotALabel.Visibility = dualHidden;
            BladeSlotBLabel.Visibility = dualHidden;

            BladeRailTop.Visibility = Visibility.Visible;
            BladeRailBottom.Visibility = Visibility.Collapsed;
            BladeTipTop.Visibility = Visibility.Visible;
            BladeTipBottom.Visibility = Visibility.Collapsed;
            BladeCenterPad.Visibility = Visibility.Visible;

            double vacLen = vacLenExtend;
            BladeRailTop.Width = vacLen;
            Canvas.SetLeft(BladeRailTop, vacHubX);
            Canvas.SetTop(BladeRailTop, 8);
            double tipLeft = vacHubX + vacLen - 6;
            Canvas.SetLeft(BladeTipTop, tipLeft);
            Canvas.SetLeft(BladeCenterPad, vacHubX + vacLen - 14);
            double slotLeft = vacHubX + vacLen - 4;
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

    private void LayoutDualVacuumArm(
        bool front,
        double armLen,
        double hubX,
        double armY,
        double armH,
        double slotTipInset)
    {
        if (front)
        {
            BladeRailFront.Width = armLen;
            BladeRailFront.Height = armH;
            Canvas.SetLeft(BladeRailFront, hubX);
            Canvas.SetTop(BladeRailFront, armY);
            Canvas.SetLeft(BladeTipFront, hubX + armLen - 8);
            Canvas.SetTop(BladeTipFront, armY - 1);
            Canvas.SetLeft(BladeSlotB, hubX + armLen - slotTipInset);
            Canvas.SetTop(BladeSlotB, armY - 1);
            Canvas.SetLeft(BladeSlotBLabel, hubX + armLen - slotTipInset);
            Canvas.SetTop(BladeSlotBLabel, armY - 13);
            return;
        }

        BladeRailBack.Width = armLen;
        BladeRailBack.Height = armH;
        Canvas.SetLeft(BladeRailBack, hubX - armLen);
        Canvas.SetTop(BladeRailBack, armY);
        Canvas.SetLeft(BladeTipBack, hubX - armLen - 16);
        Canvas.SetTop(BladeTipBack, armY - 1);
        Canvas.SetLeft(BladeSlotA, hubX - armLen + slotTipInset - 14);
        Canvas.SetTop(BladeSlotA, armY - 1);
        Canvas.SetLeft(BladeSlotALabel, hubX - armLen + slotTipInset - 14);
        Canvas.SetTop(BladeSlotALabel, armY - 13);
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

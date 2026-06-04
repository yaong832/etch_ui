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
    private const double EfemBaseBlade = 36;
    private const double EfemMaxBlade = 118;
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
            or nameof(EquipmentMotionViewModel.EfemBladeAngleDegrees)
            or nameof(EquipmentMotionViewModel.EfemBladeExtension)
            or nameof(EquipmentMotionViewModel.EfemCarryingWafer)
            or nameof(EquipmentMotionViewModel.IsVacuumDualBlade)
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

        double efemNorm = Math.Clamp((_bound.EfemBladeExtension - 0.4) / 1.2, 0, 1);
        EfemRotate.Angle = _bound.EfemBladeAngleDegrees;
        double efemLen = EfemBaseBlade + efemNorm * (EfemMaxBlade - EfemBaseBlade);
        EfemBladeRailTop.Width = efemLen;
        EfemBladeRailBottom.Width = efemLen;
        Canvas.SetLeft(EfemBladeTipTop, Math.Max(0, efemLen - 2));
        Canvas.SetLeft(EfemBladeTipBottom, Math.Max(0, efemLen - 2));
        Canvas.SetLeft(EfemBladeCenterPad, Math.Max(0, efemLen - 8));
        double efemWaferLeft = Math.Max(0, efemLen - 6);
        Canvas.SetLeft(WaferOnEfemBlade, efemWaferLeft);
        Canvas.SetLeft(WaferOnEfemBladeB, efemWaferLeft);
        WaferOnEfemBlade.Visibility = _bound.EfemCarryingWafer ? Visibility.Visible : Visibility.Collapsed;
        WaferOnEfemBladeB.Visibility = Visibility.Collapsed;

        double vacNorm = Math.Clamp((_bound.VacuumBladeExtension - 0.4) / 1.2, 0, 1);
        TmRotate.Angle = _bound.VacuumBladeAngleDegrees;
        double vacLen = TmBaseBlade + vacNorm * (TmMaxBlade - TmBaseBlade);
        BladeRailTop.Width = vacLen;
        BladeRailBottom.Width = vacLen;
        Canvas.SetLeft(BladeTipTop, Math.Max(0, vacLen - 6));
        Canvas.SetLeft(BladeTipBottom, Math.Max(0, vacLen - 6));
        Canvas.SetLeft(BladeCenterPad, Math.Max(0, vacLen - 14));
        Canvas.SetLeft(BladeSlotA, Math.Max(0, vacLen + 6));
        Canvas.SetLeft(BladeSlotB, Math.Max(0, vacLen + 6));
        BladeSlotB.Visibility = _bound.IsVacuumDualBlade ? Visibility.Visible : Visibility.Collapsed;
        double vacWaferLeft = Math.Max(0, vacLen - 2);
        Canvas.SetLeft(WaferOnBlade, vacWaferLeft);
        Canvas.SetLeft(WaferOnBladeB, vacWaferLeft);
        WaferOnBlade.Visibility = _bound.VacuumCarryingWafer ? Visibility.Visible : Visibility.Collapsed;
        WaferOnBladeB.Visibility = Visibility.Collapsed;
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using etch_ui.Equipment.Layout;
using etch_ui.Equipment.ViewModels;

namespace etch_ui.Equipment.Views;

public partial class EquipmentSchematicControl : UserControl
{
    private const double BaseBladeLength = 52;
    private const double MaxBladeLength = 165;
    private EquipmentMotionViewModel? _bound;

    public EquipmentSchematicControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += (_, _) => HookMotion();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RepositionStaticZones();
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
            RefreshBlade();
        }
    }

    private void OnMotionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EquipmentMotionViewModel.BladeAngleDegrees)
            or nameof(EquipmentMotionViewModel.BladeExtension)
            or nameof(EquipmentMotionViewModel.CarryingWafer))
        {
            RefreshBlade();
        }
    }

    private void RefreshBlade()
    {
        if (_bound == null || BladeRect == null || TmRotate == null)
        {
            return;
        }

        TmRotate.Angle = _bound.BladeAngleDegrees;
        double norm = (_bound.BladeExtension - 0.4) / 1.2;
        norm = Math.Clamp(norm, 0, 1);
        double len = BaseBladeLength + norm * (MaxBladeLength - BaseBladeLength);
        BladeRect.Width = len;
        WaferOnBlade.Visibility = _bound.CarryingWafer ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RepositionStaticZones()
    {
        Canvas.SetLeft(ChamberABox, EquipmentLayoutMetrics.ChamberAPosition.X);
        Canvas.SetTop(ChamberABox, EquipmentLayoutMetrics.ChamberAPosition.Y);
        Canvas.SetLeft(ChamberBBox, EquipmentLayoutMetrics.ChamberBPosition.X);
        Canvas.SetTop(ChamberBBox, EquipmentLayoutMetrics.ChamberBPosition.Y);
        Canvas.SetLeft(ChamberCBox, EquipmentLayoutMetrics.ChamberCPosition.X);
        Canvas.SetTop(ChamberCBox, EquipmentLayoutMetrics.ChamberCPosition.Y);
        Canvas.SetLeft(FoupABox, EquipmentLayoutMetrics.FoupAPosition.X);
        Canvas.SetTop(FoupABox, EquipmentLayoutMetrics.FoupAPosition.Y);
        Canvas.SetLeft(FoupBBox, EquipmentLayoutMetrics.FoupBPosition.X);
        Canvas.SetTop(FoupBBox, EquipmentLayoutMetrics.FoupBPosition.Y);
        Canvas.SetLeft(LoadLockBox, EquipmentLayoutMetrics.LoadLockPosition.X);
        Canvas.SetTop(LoadLockBox, EquipmentLayoutMetrics.LoadLockPosition.Y);
        double tmX = EquipmentLayoutMetrics.TmCenter.X - 42;
        double tmY = EquipmentLayoutMetrics.TmCenter.Y - 42;
        Canvas.SetLeft(TmHost, tmX);
        Canvas.SetTop(TmHost, tmY);
        Canvas.SetLeft(TmLabel, EquipmentLayoutMetrics.TmCenter.X - 36);
        Canvas.SetTop(TmLabel, EquipmentLayoutMetrics.TmCenter.Y + 52);
    }
}

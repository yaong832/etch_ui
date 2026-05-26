using System.Runtime.InteropServices;

namespace etch_ui.Plc;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct AnalogInputData
{
    public short PressureSensor;
    public short VibrationSensor;
    public short TemperatureSensor;
    public short HumiditySensor;
    public short Reserve4;
    public short Reserve5;
    public short Reserve6;
    public short Reserve7;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct DigitalIoBits
{
    public ushort Bits;
}

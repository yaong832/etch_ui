using TwinCAT.Ads;

namespace etch_ui.Plc;

public sealed record PlcProcessSnapshot(
    double TemperatureC,
    double HumidityPercent,
    double PressureMtorr,
    short PressureRaw,
    bool PressureSignalValid,
    double VibrationG,
    /// <summary>유도형 true=닫힘(정상). AccessInputValid일 때만 의미 있음.</summary>
    bool AccessSafe,
    /// <summary>NX_ID5342 디지털 입력을 읽었는지.</summary>
    bool AccessInputValid,
    ushort DigitalInputBits);

/// <summary>
/// TwinCAT ADS 연동(스마트팜 WinForm과 동일 심볼). 실패 시 상위에서 시뮬레이션으로 전환.
/// </summary>
public sealed class PlcAdsService : IDisposable
{
    public const int DefaultPort = 851;

    private readonly object _sync = new();
    private AdsClient? _client;
    private uint _analogHandle;
    private uint _digitalInHandle;
    private uint _digitalOutHandle;
    private bool _hasAnalog;
    private bool _hasDigitalIn;
    private bool _hasDigitalOut;

    private const string AnalogInputSymbol = "GVL.NX_AD4203";
    private const string DigitalInputSymbol = "GVL.NX_ID5342";
    private const string DigitalOutputSymbol = "GVL.NX_OD5121";

    /// <summary>유도형(도어) 센서 DI 비트 — Inductive_Sensor, NX_ID5342 비트5.</summary>
    private const int InductiveDoorDiBit = 5;

    public bool IsConnected { get; private set; }

    public string? LastError { get; private set; }

    public bool TryConnect(int port = DefaultPort)
    {
        Disconnect();

        lock (_sync)
        {
            try
            {
                _client = new AdsClient
                {
                    Timeout = 800,
                };
                _client.Connect(port);

                _analogHandle = _client.CreateVariableHandle(AnalogInputSymbol);
                _hasAnalog = true;

                string[] inputSymbols = { DigitalInputSymbol, "NX_ID5342" };
                _hasDigitalIn = false;
                foreach (string symbol in inputSymbols)
                {
                    try
                    {
                        _digitalInHandle = _client.CreateVariableHandle(symbol);
                        _hasDigitalIn = true;
                        break;
                    }
                    catch
                    {
                        if (symbol == inputSymbols[^1])
                        {
                            _hasDigitalIn = false;
                        }
                    }
                }

                string[] outputSymbols = { DigitalOutputSymbol, "NX_OD5121" };
                _hasDigitalOut = false;
                foreach (string symbol in outputSymbols)
                {
                    try
                    {
                        _digitalOutHandle = _client.CreateVariableHandle(symbol);
                        _hasDigitalOut = true;
                        break;
                    }
                    catch
                    {
                        if (symbol == outputSymbols[^1])
                        {
                            _hasDigitalOut = false;
                        }
                    }
                }

                IsConnected = true;
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                IsConnected = false;
                CleanupHandlesUnsafe();
                return false;
            }
        }
    }

    public bool TryReadSnapshot(out PlcProcessSnapshot snapshot)
    {
        snapshot = default!;
        lock (_sync)
        {
            if (_client is null || !IsConnected || !_hasAnalog)
            {
                return false;
            }

            if (!_client.IsConnected)
            {
                LastError = "TwinCAT ADS 연결 끊김";
                MarkDisconnectedUnsafe();
                return false;
            }

            try
            {
                object? analogObj = _client.ReadAny(_analogHandle, typeof(AnalogInputData));
                if (analogObj is not AnalogInputData analogData)
                {
                    LastError = "Analog 데이터 형식 오류";
                    MarkDisconnectedUnsafe();
                    return false;
                }

                (double humi, double temp, double pressureMtorr, bool pressValid, double vibPct) =
                    PlcAnalogScaling.ToEngineering(analogData);
                if (!pressValid)
                {
                    pressureMtorr = 0;
                }

                double vibG = PlcAnalogScaling.VibrationPercentToG(vibPct);

                ushort bits = 0;
                bool accessInputValid = false;
                if (_hasDigitalIn)
                {
                    object? dio = _client.ReadAny(_digitalInHandle, typeof(DigitalIoBits));
                    if (dio is DigitalIoBits typed)
                    {
                        bits = typed.Bits;
                        accessInputValid = true;
                    }
                }

                // 비트5 Inductive_Sensor: true=닫힘(정상), false=열림(인터락 미충족)
                bool doorClosed = accessInputValid && (bits & (1 << InductiveDoorDiBit)) != 0;
                bool accessSafe = doorClosed;
                snapshot = new PlcProcessSnapshot(
                    temp, humi, pressureMtorr, analogData.PressureSensor, pressValid, vibG,
                    accessSafe, accessInputValid, bits);
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                MarkDisconnectedUnsafe();
                return false;
            }
        }
    }

    /// <summary>ADS/EtherCAT 링크 상실 시 세션 정리(플래그·핸들만, Dispose는 Disconnect).</summary>
    private void MarkDisconnectedUnsafe()
    {
        IsConnected = false;
        CleanupHandlesUnsafe();
    }

    public void WriteDigitalOutputLamps(ushort lampBits)
    {
        lock (_sync)
        {
            if (_client is null || !IsConnected || !_hasDigitalOut)
            {
                return;
            }

            try
            {
                var output = new DigitalIoBits { Bits = lampBits };
                _client.WriteAny(_digitalOutHandle, output);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                MarkDisconnectedUnsafe();
            }
        }
    }

    public void Disconnect()
    {
        lock (_sync)
        {
            CleanupHandlesUnsafe();
            if (_client is not null)
            {
                try
                {
                    _client.Dispose();
                }
                catch
                {
                    // ignore
                }

                _client = null;
            }

            IsConnected = false;
        }
    }

    private void CleanupHandlesUnsafe()
    {
        if (_client is null)
        {
            return;
        }

        void tryDelete(uint handle)
        {
            if (handle == 0)
            {
                return;
            }

            try
            {
                _client.DeleteVariableHandle(handle);
            }
            catch
            {
                // ignore
            }
        }

        if (_hasAnalog)
        {
            tryDelete(_analogHandle);
        }

        if (_hasDigitalIn)
        {
            tryDelete(_digitalInHandle);
        }

        if (_hasDigitalOut)
        {
            tryDelete(_digitalOutHandle);
        }

        _hasAnalog = false;
        _hasDigitalIn = false;
        _hasDigitalOut = false;
        _analogHandle = 0;
        _digitalInHandle = 0;
        _digitalOutHandle = 0;
    }

    public void Dispose()
    {
        Disconnect();
    }
}

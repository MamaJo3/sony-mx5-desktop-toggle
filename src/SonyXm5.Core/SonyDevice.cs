using System;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;

namespace SonyXm5.Core;

/// <summary>Finds and connects to a Sony headphone's control service over Bluetooth RFCOMM.</summary>
public static class SonyDevice
{
    /// <summary>
    /// The Sony headphone control RFCOMM service ("Serial HPC"). Same across WH-1000XM5 units,
    /// so we discover by service rather than a hardcoded MAC address.
    /// </summary>
    public static readonly Guid ControlServiceUuid = new("956c7b26-d49a-4ba8-b03f-b17d393cb6e2");

    /// <summary>
    /// Connect to the first paired+connected headphone exposing the control service.
    /// Returns null if none is reachable.
    /// </summary>
    public static async Task<StreamSocket> ConnectAsync()
    {
        string selector = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.FromUuid(ControlServiceUuid));
        var found = await DeviceInformation.FindAllAsync(selector);
        foreach (var di in found)
        {
            try
            {
                var svc = await RfcommDeviceService.FromIdAsync(di.Id);
                if (svc is null) continue;
                var sock = new StreamSocket();
                await sock.ConnectAsync(svc.ConnectionHostName, svc.ConnectionServiceName);
                return sock;
            }
            catch { /* try the next candidate */ }
        }
        return null;
    }
}

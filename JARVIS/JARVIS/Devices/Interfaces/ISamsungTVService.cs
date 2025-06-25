using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JARVIS.Devices.CommandHandlers;

namespace JARVIS.Devices.Interfaces
{
    /// <summary>
    /// Defines the operations for controlling a Samsung TV.
    /// </summary>
    public interface ISamsungTVService : IAsyncDisposable
    {
        /// <summary>Pair & open a WebSocket session.</summary>
        Task ConnectAsync();

        /// <summary>Close the WebSocket session.</summary>
        Task DisconnectAsync();

        /// <summary>Send a raw key-press command (e.g. "KEY_VOLUMEUP").</summary>
        Task SendCommandAsync(string keyName);

        /// <summary>Launch a Tizen app by its AppId.</summary>
        Task LaunchAppAsync(string appId);

        /// <summary>Retrieve the list of installed apps.</summary>
        Task<IReadOnlyList<SamsungAppInfo>> GetInstalledAppsAsync();
    }
}

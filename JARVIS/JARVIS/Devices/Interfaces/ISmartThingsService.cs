using System.Collections.Generic;
using System.Threading.Tasks;

namespace JARVIS.Devices.Interfaces
{
    public record DeviceInfo(string DeviceId, string Label);

    public interface ISmartThingsService
    {
        Task<IEnumerable<DeviceInfo>> GetDevicesAsync();
        Task SendCommandAsync(string deviceId, string capability, string command, object[] args = null);
    }
}

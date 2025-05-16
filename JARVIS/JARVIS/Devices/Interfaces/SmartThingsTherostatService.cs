using System.Threading.Tasks;
using JARVIS.Devices.Interfaces;
using JARVIS.Services; // where ISmartThingsService lives

namespace JARVIS.Devices
{
    public class SmartThingsThermostatService : IThermostatService
    {
        private readonly ISmartThingsService _st;

        public SmartThingsThermostatService(ISmartThingsService smartThingsService)
        {
            _st = smartThingsService;
        }

        public Task SetTemperatureAsync(string zoneId, double temp)
            => _st.SetThermostatAsync(zoneId, temp);

        public async Task<double?> GetCurrentTemperatureAsync(string zoneId)
        {
            var statusDoc = await _st.GetDeviceStatusAsync(zoneId);
            var root = statusDoc.RootElement;

            // navigate the JSON to the temperature value
            double temp = root
                .GetProperty("components")
                .GetProperty("main")
                .GetProperty("temperatureMeasurement")
                .GetProperty("temperature")
                .GetProperty("value")
                .GetDouble();

            return temp;
        }

        public Task TurnOnHeat(double temp)
            => _st.SetThermostatAsync("Thermostat", temp);

        public Task TurnOnAC(double temp)
            => _st.SetThermostatAsync("Thermostat", temp);
    }
}

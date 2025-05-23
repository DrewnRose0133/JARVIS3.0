﻿using System.Threading.Tasks;

namespace JARVIS.Devices.Interfaces
{
    public interface IThermostatService
    {
        Task SetTemperatureAsync(string zoneId, double temp);
        Task<double?> GetCurrentTemperatureAsync(string zoneId);
        Task TurnOnHeat(double temp);
        Task TurnOnAC(double temp);
    }
}

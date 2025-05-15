using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JARVIS.Devices.Interfaces
{
    public interface ICommandHandler
    {
        Task<string?> HandleAsync(string input);
    }
}

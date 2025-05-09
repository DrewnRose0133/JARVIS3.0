using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JARVIS.Services;

namespace JARVIS.Services
{
    public class StartupEngine
    {
        public static VisualizerSocketServer InitializeVisualizer()
        {
            var server = new VisualizerSocketServer();
            server.Start();
            server.Broadcast("Idle");
            return server;
        }
    }
}

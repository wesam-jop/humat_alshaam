using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Adventure3D.Configuration;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace Adventure3D.Networking
{
    /// <summary>Creates drivers and pipelines identically on both ends (pipeline ids must match).</summary>
    public static class TransportFactory
    {
        public struct Pipelines
        {
            public NetworkPipeline Reliable;
            public NetworkPipeline Unreliable;
        }

        public static NetworkDriver CreateDriver(NetworkProfile p, bool applySimulation, out Pipelines pipelines)
        {
            var settings = new NetworkSettings(Allocator.Temp);
            settings.WithNetworkConfigParameters(
                connectTimeoutMS: p.ConnectTimeoutMs,
                maxConnectAttempts: p.MaxConnectAttempts,
                disconnectTimeoutMS: p.DisconnectTimeoutMs,
                heartbeatTimeoutMS: p.HeartbeatTimeoutMs);

            int delay = applySimulation ? p.SimulatedLatencyMs : 0;
            int jitter = applySimulation ? p.SimulatedJitterMs : 0;
            int loss = applySimulation ? p.SimulatedPacketLossPercent : 0;
            settings.WithSimulatorStageParameters(
                maxPacketCount: 1024,
                mode: ApplyMode.AllPackets,
                packetDelayMs: delay,
                packetJitterMs: jitter,
                packetDropPercentage: loss);

            var driver = NetworkDriver.Create(settings);
            pipelines = new Pipelines
            {
                Reliable = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage), typeof(SimulatorPipelineStage)),
                Unreliable = driver.CreatePipeline(typeof(SimulatorPipelineStage)),
            };
            return driver;
        }
    }

    public static class AddressTools
    {
        static readonly Regex HostName = new Regex(@"^(?=.{1,253}$)[A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)*$");

        public enum Kind { Invalid, IPv4, IPv6, HostName }

        public static Kind Classify(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return Kind.Invalid;
            address = address.Trim();
            if (IPAddress.TryParse(address, out var ip))
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    // IPAddress.TryParse accepts "1" or "1.2"; require dotted quad for clarity.
                    return address.Split('.').Length == 4 ? Kind.IPv4 : Kind.Invalid;
                }
                if (ip.AddressFamily == AddressFamily.InterNetworkV6) return Kind.IPv6;
            }
            if (Regex.IsMatch(address, @"^[0-9.]+$")) return Kind.Invalid; // malformed IPv4
            return HostName.IsMatch(address) ? Kind.HostName : Kind.Invalid;
        }

        public static bool TryParsePort(string text, out ushort port)
        {
            port = 0;
            if (!int.TryParse(text, out int p) || p < 1 || p > 65535) return false;
            port = (ushort)p;
            return true;
        }

        /// <summary>IPv4 addresses of active LAN interfaces, shown to the host so others can join.</summary>
        public static List<string> LocalIPv4Addresses()
        {
            var list = new List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                            list.Add(ua.Address.ToString());
                }
            }
            catch (System.Exception)
            {
                // Some platforms restrict interface enumeration; the port is still shown.
            }
            if (list.Count == 0) list.Add("127.0.0.1");
            return list;
        }
    }
}

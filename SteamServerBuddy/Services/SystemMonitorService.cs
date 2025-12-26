using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace SteamServerBuddy.Services
{
    public class SystemMonitorService
    {
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;
        private readonly Queue<double> _netInHistory = new();
        private readonly Queue<double> _netOutHistory = new();
        private long _lastBytesReceived;
        private long _lastBytesSent;
        private DateTime _lastNetCheck = DateTime.Now;
        private const int NET_HISTORY_SIZE = 30; // 30 seconds rolling

        public SystemMonitorService()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                // First call returns 0, need to prime
                _cpuCounter.NextValue();
            }
            catch
            {
                // Performance counters may not be available
            }

            InitializeNetworkCounters();
        }

        private void InitializeNetworkCounters()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && 
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                
                foreach (var ni in interfaces)
                {
                    var stats = ni.GetIPv4Statistics();
                    _lastBytesReceived += stats.BytesReceived;
                    _lastBytesSent += stats.BytesSent;
                }
            }
            catch { }
        }

        #region Host-Level Metrics

        public float GetCpuPercent()
        {
            try
            {
                return _cpuCounter?.NextValue() ?? 0;
            }
            catch { return 0; }
        }

        public (double UsedGB, double TotalGB, double Percent) GetRamInfo()
        {
            try
            {
                double availableMB = _ramCounter?.NextValue() ?? 0;
                
                // Get total RAM via WMI
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    double totalKB = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                    double totalGB = totalKB / 1024 / 1024;
                    double usedGB = totalGB - (availableMB / 1024);
                    double percent = (usedGB / totalGB) * 100;
                    return (Math.Round(usedGB, 1), Math.Round(totalGB, 1), Math.Round(percent, 1));
                }
            }
            catch { }
            return (0, 0, 0);
        }

        public (double FreeGB, double TotalGB, double Percent, string DriveLetter) GetDiskInfo(string steamAppsPath = null)
        {
            try
            {
                // Find drive containing steamapps or use C:
                string targetDrive = "C:\\";
                if (!string.IsNullOrEmpty(steamAppsPath) && Directory.Exists(steamAppsPath))
                {
                    targetDrive = Path.GetPathRoot(steamAppsPath);
                }

                var drive = new DriveInfo(targetDrive);
                if (drive.IsReady)
                {
                    double freeGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
                    double totalGB = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
                    double usedPercent = ((totalGB - freeGB) / totalGB) * 100;
                    return (Math.Round(freeGB, 1), Math.Round(totalGB, 1), Math.Round(usedPercent, 1), drive.Name.TrimEnd('\\'));
                }
            }
            catch { }
            return (0, 0, 0, "C:");
        }

        public (double InMbps, double OutMbps, List<double> InHistory, List<double> OutHistory) GetNetworkIO()
        {
            try
            {
                long currentBytesReceived = 0;
                long currentBytesSent = 0;

                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && 
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                foreach (var ni in interfaces)
                {
                    var stats = ni.GetIPv4Statistics();
                    currentBytesReceived += stats.BytesReceived;
                    currentBytesSent += stats.BytesSent;
                }

                var elapsed = (DateTime.Now - _lastNetCheck).TotalSeconds;
                if (elapsed < 0.5) elapsed = 1; // Prevent division issues

                double inMbps = ((currentBytesReceived - _lastBytesReceived) * 8 / elapsed) / 1_000_000;
                double outMbps = ((currentBytesSent - _lastBytesSent) * 8 / elapsed) / 1_000_000;

                _lastBytesReceived = currentBytesReceived;
                _lastBytesSent = currentBytesSent;
                _lastNetCheck = DateTime.Now;

                // Add to history
                _netInHistory.Enqueue(Math.Round(inMbps, 2));
                _netOutHistory.Enqueue(Math.Round(outMbps, 2));

                while (_netInHistory.Count > NET_HISTORY_SIZE) _netInHistory.Dequeue();
                while (_netOutHistory.Count > NET_HISTORY_SIZE) _netOutHistory.Dequeue();

                return (Math.Round(inMbps, 2), Math.Round(outMbps, 2), _netInHistory.ToList(), _netOutHistory.ToList());
            }
            catch { }
            return (0, 0, new List<double>(), new List<double>());
        }

        public string GetUptime()
        {
            try
            {
                var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
            }
            catch { return "0d 0h"; }
        }

        #endregion

        #region Process-Level Metrics

        public ProcessMetrics GetProcessMetrics(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                return GetProcessMetrics(process);
            }
            catch
            {
                return null;
            }
        }

        public ProcessMetrics GetProcessMetrics(Process process)
        {
            if (process == null || process.HasExited) return null;

            try
            {
                process.Refresh();
                
                var metrics = new ProcessMetrics
                {
                    Pid = process.Id,
                    ProcessName = process.ProcessName,
                    WorkingSetMB = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1),
                    PrivateBytesMB = Math.Round(process.PrivateMemorySize64 / 1024.0 / 1024.0, 1),
                    ThreadCount = process.Threads.Count,
                    HandleCount = process.HandleCount,
                    StartTime = process.StartTime,
                    RunningTime = GetRunningTimeString(process.StartTime)
                };

                // CPU % requires two samples
                try
                {
                    var startTime = DateTime.UtcNow;
                    var startCpuUsage = process.TotalProcessorTime;
                    System.Threading.Thread.Sleep(100);
                    process.Refresh();
                    var endTime = DateTime.UtcNow;
                    var endCpuUsage = process.TotalProcessorTime;
                    
                    var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                    var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                    var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                    
                    metrics.CpuPercent = Math.Round(cpuUsageTotal * 100, 1);
                }
                catch { metrics.CpuPercent = 0; }

                return metrics;
            }
            catch
            {
                return null;
            }
        }

        private string GetRunningTimeString(DateTime startTime)
        {
            var running = DateTime.Now - startTime;
            if (running.TotalDays >= 1)
                return $"{(int)running.TotalDays}d {running.Hours}h";
            if (running.TotalHours >= 1)
                return $"{(int)running.TotalHours}h {running.Minutes}m";
            return $"{(int)running.TotalMinutes}m";
        }

        public bool KillProcess(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                process.Kill();
                return true;
            }
            catch { return false; }
        }

        #endregion

        #region Threshold Helpers

        public static string GetThresholdColor(double percent)
        {
            if (percent < 70) return "#48BB78"; // Green
            if (percent < 85) return "#ECC94B"; // Yellow
            return "#E53E3E"; // Red
        }

        public static bool IsWarning(double percent) => percent >= 70 && percent < 85;
        public static bool IsCritical(double percent) => percent >= 85;

        #endregion
    }

    public class ProcessMetrics
    {
        public int Pid { get; set; }
        public string ProcessName { get; set; }
        public double CpuPercent { get; set; }
        public double WorkingSetMB { get; set; }
        public double PrivateBytesMB { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public DateTime StartTime { get; set; }
        public string RunningTime { get; set; }
    }
}

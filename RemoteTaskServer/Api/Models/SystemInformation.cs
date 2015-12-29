﻿#region

using System.Collections.Generic;
using System.Web.Script.Serialization;

#endregion

namespace UlteriusServer.Api.Models

{
    public static class SystemInformation
    {
        public static List<float> CpuUsage { get; set; }
        public static ulong TotalMemory { get; set; }
        public static ulong AvailableMemory { get; set; }
        public static ulong UsedMemory { get; set; }
        public static int RunningProcesses { get; set; }
        public static double UpTime { get; set; }
        public static bool RunningAsAdmin { get; set; }
        public static string JSON { get; set; }
        //public static List<ProcessInformation> ProcessCpuUsage { get; set; }

        public static string ToJson()
        {
            var json =
                new JavaScriptSerializer().Serialize(
                    new
                    {
                        cpuUsage = CpuUsage,
                        totalMemory = TotalMemory,
                        availableMemory = AvailableMemory,
                        usedMemory = UsedMemory,
                        runningProceses = RunningProcesses,
                        upTime = UpTime,
                        runningAsAdmin = RunningAsAdmin
                      //  processCpuUsage = ProcessCpuUsage
                    });

            return json;
        }
    }
}
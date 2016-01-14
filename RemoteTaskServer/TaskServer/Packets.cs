﻿#region

using System;
using Newtonsoft.Json;
using UlteriusServer.Utilities;

#endregion

namespace UlteriusServer.TaskServer

{
    [Serializable]
    public class Packets
    {
        private readonly Settings settings = new Settings();
        public string action;
        public string apiKey;
        public string args;
        public PacketType packetType;
        public string senderID;

        public Packets(string packetJson)
        {
            JsPacket deserializedPacket = null;
            try
            {
                deserializedPacket = JsonConvert.DeserializeObject<JsPacket>(packetJson);
            }
            catch (Exception)
            {
                packetType = PacketType.InvalidOrEmptyPacket;
            }

            if (deserializedPacket != null)
            {
                apiKey = deserializedPacket.apiKey.Trim();
                action = deserializedPacket.action.Trim().ToLower();
                args = deserializedPacket.args?.Trim() ?? "";
                senderID = "client";
                var key = settings.Read("TaskServer", "ApiKey", "");

                if (!string.IsNullOrEmpty(key))
                {
                    if (key.Equals(apiKey))
                    {
                        switch (action)
                        {
                            case "authenticate":
                                packetType = PacketType.Authenticate;
                                break;
                            case "requestprocessinformation":
                                Console.WriteLine("Request Process Information");
                                packetType = PacketType.RequestProcess;
                                break;
                            case "requestcpuinformation":
                                Console.WriteLine("Request CPU Information");
                                packetType = PacketType.RequestCpuInformation;
                                break;
                            case "requestosinformation":
                                Console.WriteLine("Request OS Information");
                                packetType = PacketType.RequestOsInformation;
                                break;
                            case "requestnetworkinformation":
                                Console.WriteLine("Request Network Information");
                                packetType = PacketType.RequestNetworkInformation;
                                break;
                            case "requestsysteminformation":
                                Console.WriteLine("Request System Information");
                                packetType = PacketType.RequestSystemInformation;
                                break;
                            case "startprocess":
                                Console.WriteLine("Starting Process " + args);
                                packetType = PacketType.StartProcess;
                                break;
                            case "killprocess":
                                Console.WriteLine("Killing Process " + args);
                                packetType = PacketType.KillProcess;
                                break;
                            case "generatenewkey":
                                Console.WriteLine("Creating New Api Key");
                                packetType = PacketType.GenerateNewKey;
                                break;
                            case "togglewebserver":
                                Console.WriteLine("Toggling Web TServer");
                                packetType = PacketType.UseWebServer;
                                break;
                            case "changewebserverport":
                                Console.WriteLine("Changing Web TServer Port");
                                packetType = PacketType.ChangeWebServerPort;
                                break;
                            case "changewebfilepath":
                                Console.WriteLine("Changing Web File Path");
                                packetType = PacketType.ChangeWebFilePath;
                                break;
                            case "changetaskserverport":
                                Console.WriteLine("Changing Task TServer Port");
                                packetType = PacketType.ChangeTaskServerPort;
                                break;
                            case "changenetworkresolve":
                                Console.WriteLine("Changing Network Resolve");
                                packetType = PacketType.ChangeNetworkResolve;
                                break;
                            case "getcurrentsettings":
                                Console.WriteLine("Getting Current Settings");
                                packetType = PacketType.GetCurrentSettings;
                                break;
                            case "geteventlogs":
                                Console.WriteLine("Getting Event Logs");
                                packetType = PacketType.GetEventLogs;
                                break;
                            case "checkforupdate":
                                Console.WriteLine("Checking for update");
                                packetType = PacketType.CheckUpdate;
                                break;

                            case "restartserver":
                                Console.WriteLine("Restarting TServer");
                                packetType = PacketType.RestartServer;
                                break;
                            case "getwindowsdata":
                                Console.WriteLine("Getting Windows Account Data");
                                packetType = PacketType.RequestWindowsInformation;
                                break;
                            case "getactivewindowssnapshots":
                                Console.WriteLine("Getting Active Windows Snapshots");
                                packetType = PacketType.GetActiveWindowsSnapshots;
                                break;
                            default:
                                packetType = PacketType.InvalidOrEmptyPacket;
                                break;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid API Key " + apiKey);
                        packetType = PacketType.InvalidApiKey;
                    }
                }
                else
                {
                    Console.WriteLine("No API Key Detected - Generated");
                    packetType = PacketType.GenerateNewKey;
                }
            }
        }
    }

    public enum PacketType
    {
        RequestProcess,
        RequestCpuInformation,
        RequestOsInformation,
        RequestNetworkInformation,
        RequestSystemInformation,
        StartProcess,
        KillProcess,
        GenerateNewKey,
        EmptyApiKey,
        InvalidApiKey,
        InvalidOrEmptyPacket,
        UseWebServer,
        ChangeWebServerPort,
        ChangeWebFilePath,
        ChangeTaskServerPort,
        ChangeNetworkResolve,
        GetCurrentSettings,
        GetEventLogs,
        CheckUpdate,
        RequestWindowsInformation,
        RestartServer,
        GetActiveWindowsSnapshots,
        Authenticate
    }
}
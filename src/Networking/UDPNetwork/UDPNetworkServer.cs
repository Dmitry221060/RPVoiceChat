﻿using System;
using System.Collections.Generic;
using System.Net;

namespace RPVoiceChat.Networking
{
    public class UDPNetworkServer : UDPNetworkBase, IExtendedNetworkServer
    {
        public event Action<AudioPacket> AudioPacketReceived;

        private Dictionary<string, ConnectionInfo> connectionsByPlayer = new Dictionary<string, ConnectionInfo>();
        private IPAddress ip;

        public UDPNetworkServer(int port, string ip = null)
        {
            this.port = port;
            this.ip = IPAddress.Parse(ip ?? GetPublicIP());
            logger = Utils.Logger.server;

            OnMessageReceived += MessageReceived;
        }

        public void Launch()
        {
            if (!IsInternalNetwork(ip))
                SetupUpnp(port);
            OpenUDPClient(port);
            StartListening(port);
        }

        public override ConnectionInfo GetConnection()
        {
            if (connectionInfo != null) return connectionInfo;

            connectionInfo = new ConnectionInfo()
            {
                Address = ip.MapToIPv4().ToString(),
                Port = port
            };

            return connectionInfo;
        }

        public void SendPacket(NetworkPacket packet, string playerId)
        {
            ConnectionInfo connectionInfo;
            if (!connectionsByPlayer.TryGetValue(playerId, out connectionInfo))
                throw new Exception($"Player {playerId} is not connected to the server");

            var data = packet.ToBytes();
            var destination = GetEndPoint(connectionInfo);

            UdpClient.Send(data, data.Length, destination);
        }

        public void PlayerConnected(string playerId, ConnectionInfo connectionInfo)
        {
            connectionsByPlayer.Add(playerId, connectionInfo);
            logger.VerboseDebug($"{playerId} connected over UDP");
        }

        public void PlayerDisconnected(string playerId)
        {
            connectionsByPlayer.Remove(playerId);
            logger.VerboseDebug($"{playerId} disconnected from UDP server");
        }

        private void MessageReceived(byte[] msg)
        {
            PacketType code = (PacketType)BitConverter.ToInt32(msg, 0);
            switch (code)
            {
                case PacketType.Audio:
                    var packet = NetworkPacket.FromBytes<AudioPacket>(msg);
                    AudioPacketReceived?.Invoke(packet);
                    break;
                default:
                    throw new Exception($"Unsupported packet type: {code}");
            }
        }
    }
}

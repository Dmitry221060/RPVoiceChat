﻿using RPVoiceChat.Utils;
using System;
using System.Net;
using System.Threading.Tasks;

namespace RPVoiceChat.Networking
{
    public class TCPNetworkClient : TCPNetworkBase, IExtendedNetworkClient
    {
        public event Action<AudioPacket> OnAudioReceived;

        private IPEndPoint serverEndpoint;
        private TCPConnection connection;

        public TCPNetworkClient() : base(Logger.client) { }

        public ConnectionInfo Connect(ConnectionInfo serverConnection)
        {
            serverEndpoint = NetworkUtils.GetEndPoint(serverConnection);
            connection = OpenConnection(serverEndpoint);
            VerifyClientReadiness();

            return new ConnectionInfo(port);
        }

        public void SendAudioToServer(AudioPacket packet)
        {
            if (isReady == false)
            {
                logger.Warning($"Attempting to send audio over {_transportID} while client isn't ready. Skipping sending.");
                return;
            }
            if (connection == null) throw new Exception($"{_transportID} connection has not been initialized.");

            var data = packet.ToBytes();
            connection.Send(data);
        }

        private TCPConnection OpenConnection(IPEndPoint endPoint)
        {
            var connection = new TCPConnection(logger);
            connection.OnMessageReceived += MessageReceived;
            connection.OnDisconnected += ConnectionClosed;
            connection.Connect(endPoint);
            connection.StartListening();
            port = connection.port;

            return connection;
        }

        private void ConnectionClosed(bool isGraceful, TCPConnection _)
        {
            isReady = false;
            logger.Notification($"Connection with {_transportID} server was closed");
            if (isGraceful) return;
            try
            {
                logger.Notification($"Attempting reconnect to {_transportID} server");
                connection.Reconnect(serverEndpoint);
                connection.StartListening();
                VerifyClientReadiness();
                logger.Notification($"Successfully reconnected to {_transportID} server");
            }
            catch(Exception e)
            {
                logger.Warning($"Unable to reconnect to {_transportID}:\n{e}");
            }
        }

        private void MessageReceived(byte[] msg, TCPConnection _)
        {
            PacketType code = (PacketType)BitConverter.ToInt32(msg, 0);
            switch (code)
            {
                case PacketType.Pong:
                    isReady = true;
                    _readinessProbeCTS.Cancel();
                    break;
                case PacketType.Audio:
                    AudioPacket packet = NetworkPacket.FromBytes<AudioPacket>(msg);
                    OnAudioReceived?.Invoke(packet);
                    break;
                default:
                    logger.Error($"Received unsupported packet type: {code}, proceeding to ignore it");
                    return;
            }
        }

        private void VerifyClientReadiness()
        {
            var pingPacket = BitConverter.GetBytes((int)PacketType.Ping);

            try
            {
                connection.SendAsync(pingPacket, _readinessProbeCTS.Token);
                Task.Delay(5000, _readinessProbeCTS.Token).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException) { }

            if (isReady) return;
            throw new Exception("Client failed readiness probe. Aborting to prevent silent malfunction");
        }

        public override void Dispose()
        {
            base.Dispose();
            connection?.Dispose();
        }
    }
}

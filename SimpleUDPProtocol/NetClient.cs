using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SimpleUDPProtocol
{
    public class NetClient
    {

        public class ReliableMessage
        {
            public byte[] Buffer;
            public IPEndPoint Target;
            public int ID;
            public int RetransmissionCount;
            public TimeSpan RetransmissionElapsed;
        }

        public TimeSpan RetransmissionTime = TimeSpan.FromSeconds(0.3f);
        public int RetransmissionTries = 5;
        public int MagicNumber = 0x024810FF;

        public Action<byte[], IPEndPoint> OnMessageReceived;
        public Action<ReliableMessage> OnTimeout;

        Socket socket;
        Timer timer;
        
        List<ReliableMessage> reliableMessages = new List<ReliableMessage>();

        byte[] receiveBuffer = new byte[1024 * 64];

        /// <summary>
        /// For client
        /// </summary>
        public NetClient()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            DisableICMPPortUnreachableException();
        }

        /// <summary>
        /// For server
        /// </summary>
        /// <param name="localEndPoint"></param>
        public NetClient(IPEndPoint localEndPoint)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(localEndPoint);
            DisableICMPPortUnreachableException();
        }

        public void Start()
        {
            StartTimer();
            StartReceive();
        }

        void DisableICMPPortUnreachableException()
        {
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            socket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
        }

        /// <summary>
        /// Receiving loop. Needs to be run in separate Task if your programs need to do soemething more than parsign packets
        /// </summary>
        void StartReceive()
        {
            EndPoint sourceTemp = new IPEndPoint(IPAddress.Any, 0);

            bool succes = false;
            while (!succes)
            {
                try
                {
                    socket.BeginReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref sourceTemp, FinishReceive, null);
                    succes = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        TimeSpan timerDeltaTime = TimeSpan.FromSeconds(0.1f);

        void StartTimer()
        {
            timer = new Timer(TimerCallback, null, TimeSpan.Zero, timerDeltaTime);
        }

        /// <summary>
        /// Checks for bad packets and does retransmission or throws error
        /// </summary>
        /// <param name="arg"></param>
        void TimerCallback(Object arg)
        {
            lock (reliableMessages)
            {
                for (int n = reliableMessages.Count - 1; n >= 0; --n)
                {
                    ReliableMessage message = reliableMessages[n];

                    message.RetransmissionElapsed += timerDeltaTime;
                    if (message.RetransmissionElapsed > RetransmissionTime)
                    {
                        message.RetransmissionElapsed = TimeSpan.Zero;

                        ++message.RetransmissionCount;
                        if (message.RetransmissionCount > RetransmissionTries)
                        {
                            reliableMessages.RemoveAt(n);
                            OnTimeout?.Invoke(message);
                        }
                        else
                        {
                            SendRetransmitted(message.Buffer, message.Target, message.ID);
                        }
                    }
                }
            }
        }

        public void Close()
        {
            timer.Dispose();
            socket.Close();
        }

        void FinishReceive(IAsyncResult result)
        {
            try
            {
                EndPoint sourceTemp = new IPEndPoint(IPAddress.Any, 0);
                int length = socket.EndReceiveFrom(result, ref sourceTemp);
                IPEndPoint source = (IPEndPoint)sourceTemp;

                using (MemoryStream memoryStream = new MemoryStream(receiveBuffer, 0, length))
                {
                    ProcessMessage(memoryStream, source);
                }
            }
            catch
            {
            }

            StartReceive();
        }

        /// <summary>
        /// Processes message and checks if it's valid message. Otherwise it drops it
        /// </summary>
        /// <param name="memoryStream"></param>
        /// <param name="source"></param>
        void ProcessMessage(MemoryStream memoryStream, IPEndPoint source)
        {
            using (BinaryReader reader = new BinaryReader(memoryStream))
            {
                int magic = reader.ReadInt32();
                if (magic != MagicNumber)
                    return;

                NetClientMessageType type = (NetClientMessageType)reader.ReadByte();

                switch (type)
                {
                    case NetClientMessageType.Unreliable:
                        {
                            byte[] data = reader.ReadBytes((int)(memoryStream.Length - memoryStream.Position));
                            OnMessageReceived(data, source);

                            break;
                        }
                    case NetClientMessageType.Reliable:
                        {
                            int id = reader.ReadInt32();
                            SendAcknowledgment(source, id);

                            byte[] data = reader.ReadBytes((int)(memoryStream.Length - memoryStream.Position));
                            OnMessageReceived(data, source);

                            break;
                        }
                    case NetClientMessageType.Ack:
                        {
                            int id = reader.ReadInt32();

                            lock (reliableMessages)
                            {
                                for (int n = reliableMessages.Count - 1; n >= 0; --n)
                                {
                                    if (reliableMessages[n].ID == id)
                                    {
                                        reliableMessages.RemoveAt(n);
                                        break;
                                    }
                                }
                            }

                            break;
                        }
                }
            }
        }

        int nextReliableId;

        /// <summary>
        /// Send buffer to specified target
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="target"></param>
        /// <param name="type">Type reliable or unreliable</param>
        public void Send(byte[] buffer, IPEndPoint target, NetClientMessageType type)
        {
            using (MemoryStream memoryStream = new MemoryStream(buffer.Length + 20))
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                switch (type)
                {
                    case NetClientMessageType.Unreliable:
                        {
                            writer.Write(MagicNumber);
                            writer.Write((byte)NetClientMessageType.Unreliable);
                            writer.Write(buffer, 0, buffer.Length);

                            break;
                        }
                    case NetClientMessageType.Reliable:
                        {
                            int id = ++nextReliableId;

                            writer.Write(MagicNumber);
                            writer.Write((byte)NetClientMessageType.Reliable);
                            writer.Write(id);
                            writer.Write(buffer, 0, buffer.Length);

                            lock (reliableMessages)
                            {
                                reliableMessages.Add(new ReliableMessage
                                {
                                    Buffer = buffer,
                                    Target = target,
                                    ID = id,
                                });
                            }

                            break;
                        }
                }

                byte[] finalBuffer = memoryStream.ToArray();

                try
                {
                    socket.BeginSendTo(finalBuffer, 0, finalBuffer.Length, SocketFlags.None, target, FinishSendTo, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            };
        }

        void SendRetransmitted(byte[] buffer, IPEndPoint target, int id)
        {
            using (MemoryStream memoryStream = new MemoryStream(buffer.Length + 20))
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(MagicNumber);
                writer.Write((byte)NetClientMessageType.Reliable);
                writer.Write(id);
                writer.Write(buffer, 0, buffer.Length);

                byte[] finalBuffer = memoryStream.ToArray();
                try
                {
                    socket.BeginSendTo(finalBuffer, 0, finalBuffer.Length, SocketFlags.None, target, FinishSendTo, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        /// <summary>
        /// Send confirmation that we received UDP packed
        /// </summary>
        /// <param name="target"></param>
        /// <param name="id">id of our packet</param>
        void SendAcknowledgment(IPEndPoint target, int id)
        {
            using (MemoryStream memoryStream = new MemoryStream(5))
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write(MagicNumber);
                writer.Write((byte)NetClientMessageType.Ack);
                writer.Write(id);

                byte[] finalBuffer = memoryStream.ToArray();
                try
                {
                    socket.BeginSendTo(finalBuffer, 0, finalBuffer.Length, SocketFlags.None, target, FinishSendTo, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        void FinishSendTo(IAsyncResult result)
        {
            try
            {
                socket.EndSendTo(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        void OnSendCompleted(object sender, SocketAsyncEventArgs e)
        {
            e.Dispose();
        }
    }
}
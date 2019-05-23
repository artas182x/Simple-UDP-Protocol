using SimpleUDPProtocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    class Client
    {
        class Message
        {
            public BinaryReader Reader;
            public MemoryStream Stream;
            public IPEndPoint Source;
            public MessageType Type;
        }

        class PingRequest
        {
            public DateTime time;
            public int id;
        }

        public static Form1 form1;


        private NetClient netClientConnection;
        private IPEndPoint serverIPConnection;

        private NetClient netClient;
        private int id;

        private IPEndPoint serverIP;

        private Task connectionTask;
        private Task mainTask;

        private Dictionary<byte, PaintData> udpClients;

        private List<PingRequest> pingRequests = new List<PingRequest>();

        public Client(IPEndPoint serverIP)
        {
            if (connectionTask!=null)
                connectionTask.Dispose();

            netClientConnection = new NetClient();
            netClientConnection.OnMessageReceived += ReceiveMessage;
            netClientConnection.OnTimeout += OnConnectionError;

            netClient = new NetClient();
            netClient.OnMessageReceived += ReceiveMessage;
            netClient.OnTimeout += OnConnectionError;

            this.serverIPConnection = serverIP;

            udpClients = new Dictionary<byte, PaintData>();
            
        }

        public void SendClientHello()
        {

            if (connectionTask != null && !connectionTask.IsCanceled && !connectionTask.IsFaulted && !connectionTask.IsCompleted)
                connectionTask.Dispose();

            connectionTask = Task.Run(new Action(netClientConnection.Start));

            using (MemoryStream memoryStream = new MemoryStream(256))
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)MessageType.ClientHello);

                Console.WriteLine("Sending SendClientHello to: {0}", serverIPConnection);

                byte[] finalBuffer = memoryStream.ToArray();
                netClientConnection.Send(finalBuffer, serverIPConnection, NetClientMessageType.Reliable);
            }
        }

        public void SendColor(int color)
        {
            using (MemoryStream memoryStream = new MemoryStream(10))
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)MessageType.ClientUpdateStarted);
                writer.Write((byte)id);
                writer.Write(color);

                Console.WriteLine("Sending Color to: {0}", serverIP);

                byte[] finalBuffer = memoryStream.ToArray();
                netClient.Send(finalBuffer, serverIP, NetClientMessageType.Unreliable);
            }
        }

        public void StopDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream(10))
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)MessageType.ClientUpdateInEnd);
                writer.Write((byte)id);

                Console.WriteLine("Sending stop drawing to: {0}", serverIP);

                byte[] finalBuffer = memoryStream.ToArray();
                netClient.Send(finalBuffer, serverIP, NetClientMessageType.Unreliable);
            }
        }

        public void SendDrawing(short x, short y)
        {
            UpdateSendingPing();
            using (MemoryStream memoryStream = new MemoryStream(10))
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)MessageType.ClientUpdateInProgress);
                writer.Write((byte)id);
                writer.Write(x);
                writer.Write(y);

                byte[] finalBuffer = memoryStream.ToArray();
                netClient.Send(finalBuffer, serverIP, NetClientMessageType.Unreliable);
            }
        }

        public void SendDisconnected()
        {
            using (MemoryStream memoryStream = new MemoryStream(10))
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)MessageType.Disconnected);
                writer.Write(id);

                Console.WriteLine("Sending Disconnected to: {0}", serverIPConnection);         

                byte[] finalBuffer = memoryStream.ToArray();
                netClient.Send(finalBuffer, serverIPConnection, NetClientMessageType.Unreliable);
            }

            if (form1.txtStatus.InvokeRequired)
            {
                form1.txtStatus.Invoke(new Action(() =>
                {
                    form1.txtStatus.Text = "disconnected";
                }));
            }
            else
            {
                form1.txtStatus.Text = "disconnected";
            }

            try
            {
                if (connectionTask != null && !connectionTask.IsCanceled && !connectionTask.IsFaulted && !connectionTask.IsCompleted)
                connectionTask.Dispose();
            }
            catch (Exception)
            {
            }

            try
            {      
                if (mainTask != null && !mainTask.IsCanceled && !mainTask.IsFaulted && !mainTask.IsCompleted)
                    mainTask.Dispose();
            }
            catch (Exception )
            {
            }
        }

        private int nextPingID = 0;

        void UpdateSendingPing()
        {
            SendPing(++nextPingID);

            pingRequests.Add(new PingRequest
            {
                id = nextPingID,
                time = DateTime.Now,
            });       
        }

        void SendPing(int id)
        {
            using (MemoryStream memoryStream = new MemoryStream(5))
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)MessageType.Ping);
                writer.Write(id);

                byte[] finalBuffer = memoryStream.ToArray();
                netClient.Send(finalBuffer, serverIP, NetClientMessageType.Unreliable);
            }
        }


        void ReceivePong(int id)
        {
            for (int n = pingRequests.Count - 1; n >= 0; --n)
            {
                PingRequest ping = pingRequests[n];

                if (ping.id == id)
                {
                    float dt = (float)(System.DateTime.Now - ping.time).TotalMilliseconds;
                    pingRequests.RemoveAt(n);

                    if (form1.InvokeRequired)
                    {
                        form1.Invoke(new Action(() =>
                        {
                            form1.pingLabel.Text = dt + " ms";
                        }));
                    }

                   
                }
            }
        }


        void OnConnectionError(NetClient.ReliableMessage message)
        {
            MessageBox.Show(String.Format("Failed connecting to: {0}", message.Target));
        }

        void ReceiveMessage(byte[] data, IPEndPoint source)
        {
            try
            {
                MemoryStream memoryStream = new MemoryStream(data);
                BinaryReader reader = new SafeBinaryReader(memoryStream);

                MessageType type = (MessageType)reader.ReadByte();

                Message msg = new Message
                {
                    Source = source,
                    Reader = reader,
                    Stream = memoryStream,
                    Type = type,
                };

                
                ProcessMessage(msg);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        void ProcessMessage(Message message)
        {
            BinaryReader reader = message.Reader;

            switch (message.Type)
            {

                case MessageType.Pong:
                    {
                        ReceivePong(reader.ReadInt32());
                        break;
                    }

                case MessageType.ServerHello:
                    {
                        int port = reader.ReadInt32();
                        serverIP = new IPEndPoint(serverIPConnection.Address, port);
                        id = reader.ReadInt32();

                        if (form1.InvokeRequired)
                        {
                            form1.Invoke(new Action(() =>
                            {
                                form1.txtStatus.Text = "connected";           
                            }));
                        }

                        if (connectionTask != null && !connectionTask.IsCanceled && !connectionTask.IsFaulted && !connectionTask.IsCompleted)
                            connectionTask.Dispose();

                        if (mainTask != null && !mainTask.IsCanceled && !mainTask.IsFaulted && !mainTask.IsCompleted)
                            mainTask.Dispose();

                        mainTask = Task.Run(new Action(netClient.Start));

                        break;
                    }

                case MessageType.ServerUpdate:
                    byte penType = reader.ReadByte();
                    byte clientId;
                    switch ((MessageType)penType)
                    {
                        case MessageType.ClientUpdateStarted:
                            clientId = reader.ReadByte();
                            int color = reader.ReadInt32();

                            udpClients[clientId] = new PaintData(Color.FromArgb(color), new Point(0));
                            break;


                        case MessageType.ClientUpdateInProgress:
                            {
                                try
                                {
                                    clientId = reader.ReadByte();
                                    var client1 = udpClients[clientId];

                                    short pointX = reader.ReadInt16();
                                    short pointY = reader.ReadInt16();

                                    Point newPos = new Point(pointX, pointY);

                                    if (client1.StartPos.IsEmpty == true)
                                        client1.StartPos = newPos;

                                    using (Pen p = new Pen(client1.Color, 5.0F))
                                    {
                                        // Działamy na osobnym wątku, dlatego rysowanie na kontrolce
                                        // musimy albo zlecić, albo wykonać od razu.
                                        // Jeśli wykonamy od razu, to jest szansa na "wpadkę" i wyjątek, dlatego
                                        // zlecamy jeśli jest to potrzebne
                                        if (form1.pictureBox1.InvokeRequired)
                                        {
                                            form1.pictureBox1.Invoke(new Action(() =>
                                            {
                                                var graphics = Graphics.FromImage(form1.pictureBox1.Image);
                                                graphics.DrawLine(p, client1.StartPos, newPos);
                                                form1.pictureBox1.Invalidate();
                                            }));
                                        }
                                        else
                                        {
                                            var graphics = Graphics.FromImage(form1.pictureBox1.Image);
                                            graphics.DrawLine(p, client1.StartPos, newPos);
                                            form1.pictureBox1.Invalidate();
                                        }
                                    }
                                    client1.StartPos = newPos;
                                }
                                catch (KeyNotFoundException) { }
                                break;
                            }
                        case MessageType.ClientUpdateInEnd:
                            clientId = reader.ReadByte();
                            var client = udpClients[clientId];
                            client.StartPos = new Point(0);
                            break;
                    }

                    break;
            }
       
       

            message.Reader.Close();
            message.Stream.Close();
        }
    
}
}
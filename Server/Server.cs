using SimpleUDPProtocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class Server
    {
        public NetClient connectionClient;
        public NetClient drawingClient;
        public static Server instance;

        Dictionary<int, Client> clientDictionary = new Dictionary<int, Client>();
        public List<Client> clientList = new List<Client>();
        BlockingCollection<KeyValuePair<byte, byte[]>> paintData;

        public static byte currID = 0;

        class Message
        {
            public BinaryReader Reader;
            public MemoryStream Stream;
            public IPEndPoint Source;
            public MessageType Type;
        }

        public Server()
        {
            instance = this;

            paintData = new BlockingCollection<KeyValuePair<byte, byte[]>>(new ConcurrentQueue<KeyValuePair<byte, byte[]>>(), 20);

            this.connectionClient = new NetClient(new IPEndPoint(IPAddress.Any, 1523));
            connectionClient.OnMessageReceived = ReceiveMessageConnection;

            drawingClient = new NetClient(new IPEndPoint(IPAddress.Any, 3567));
            drawingClient.OnMessageReceived = ReceiveMessage;

            Task sendTask = Task.Run(new Action(PaintDataSender));

            Console.WriteLine("Started server at ports: {0} and {1}", 1523, 3567);
        }

        public void Start()
        {
            Task waitingTask = Task.Run(new Action(connectionClient.Start));
            Task processingTask = Task.Run(new Action(drawingClient.Start));
        }

        public void SendServerHello(IPEndPoint target, int clientRandom, int port)
        {
            using (MemoryStream memoryStream = new MemoryStream(256))
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)MessageType.ServerHello);
                writer.Write(port);
                writer.Write(clientRandom);

                Console.WriteLine("Sending Server Hello! IP: " + target.ToString());

                byte[] finalBuffer = memoryStream.ToArray();
                connectionClient.Send(finalBuffer, target, NetClientMessageType.Reliable);
            }
        }

        Client GetOrCreateClient(IPEndPoint ip)
        {
            Client client = null;

            for (int n = 0; n < clientList.Count; ++n)
            {
                if (clientList[n].IP.Equals(ip))
                {
                    client = clientList[n];
                    break;
                }
            }

            if (client == null)
            {
                client = new Client();
                clientDictionary.Add(client.ID, client);
                clientList.Add(client);
            }

            return client;
        }

        void ProcessClientHello(BinaryReader reader, IPEndPoint ip)
        {
           
            int sameIps = clientList.Where(el => el.IP.Address.Equals(ip.Address)).Count();
            if (sameIps < 4)
            {
                Client client = GetOrCreateClient(ip);
                if (client != null)
                    client.ProcessClientHello(ip);
               
            }
            else
            {
                Console.WriteLine("Detected DDOS. Clients from one IP: {0}", sameIps);
            }
            
        }



        Client GetClient(int id)
        {
            Client client;
            bool found = clientDictionary.TryGetValue(id, out client);
            return client;
        }

        void PaintDataSender()
        {
            try
            {
                while (true)
                {
               
                    var data = paintData.Take();
               
                    foreach (var client in clientDictionary)
                    {
                        if (!client.Value.Dead)
                        {
                            using (MemoryStream memoryStream = new MemoryStream(5))
                            using (BinaryWriter writer = new BinaryWriter(memoryStream))
                            {
                                writer.Write((byte)MessageType.ServerUpdate);
                                writer.Write(data.Value);

                                byte[] finalBuffer = memoryStream.ToArray();
                                drawingClient.Send(finalBuffer, client.Value.IP, NetClientMessageType.Reliable);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
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

                if (type != MessageType.ClientHello && type != MessageType.Disconnected)
                    ProcessMessage(msg);
        
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        void ReceiveMessageConnection(byte[] data, IPEndPoint source)
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
            
                if (type == MessageType.ClientHello || type == MessageType.Disconnected )
                    ProcessMessage(msg);
   
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        void SendPong(IPEndPoint target, int id)
        {
            using (MemoryStream memoryStream = new MemoryStream(5))
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                writer.Write((byte)MessageType.Pong);
                writer.Write(id);

                byte[] finalBuffer = memoryStream.ToArray();
                drawingClient.Send(finalBuffer, target, NetClientMessageType.Unreliable);
            }
        }


        void ProcessMessage(Message message)
        {
            BinaryReader reader = message.Reader;

            switch (message.Type)
            {
                case MessageType.Ping:
                    {
                        int id = reader.ReadInt32();
                        SendPong(message.Source, id);
                        break;
                    }
                case MessageType.Pong:
                    {
                        int id = reader.ReadInt32();
                        break;
                    }
           
                case MessageType.ClientHello:
                    {
                        ProcessClientHello(message.Reader, message.Source);
                        break;
                    }
              
                case MessageType.Disconnected:
                    {
                        int clientId = reader.ReadInt32();
                        Client client = GetClient(clientId);
                        if (client != null)
                            client.ProcessDisconnect();
                        break;
                    }
                case MessageType.ClientUpdateStarted:
                    {
                        byte[] data = new byte[6];
                        data[0] = (byte)MessageType.ClientUpdateStarted;
                        data[1] = reader.ReadByte(); //clientid
                        data[2] = reader.ReadByte(); //color
                        data[3] = reader.ReadByte(); //color
                        data[4] = reader.ReadByte(); //color
                        data[5] = reader.ReadByte(); //color
   
                        paintData.Add(new KeyValuePair<byte, byte[]>(data[1], data));

                        Console.WriteLine("Started drawing from: " + data[1]);
                        break;
                    }
                 case MessageType.ClientUpdateInProgress:
                    {
                        byte[] data = new byte[6];
                        data[0] = (byte)MessageType.ClientUpdateInProgress;
                        data[1] = reader.ReadByte(); //clientid
                        data[2] = reader.ReadByte(); //posx LittleEndian short
                        data[3] = reader.ReadByte(); //posx
                        data[4] = reader.ReadByte(); //posy LittleEndian short
                        data[5] = reader.ReadByte(); //posy

                        paintData.Add(new KeyValuePair<byte, byte[]>(data[1], data));
                        break;
                    }
                case MessageType.ClientUpdateInEnd:
                    {
                        byte[] data = new byte[2];
                        data[0] = (byte)MessageType.ClientUpdateInEnd;
                        data[1] = reader.ReadByte(); //clientid
                        paintData.Add(new KeyValuePair<byte, byte[]>(data[1], data));
                        Console.WriteLine("Stopped drawing from: " + data[1]);
                        break;
                    }
            }

            message.Reader.Close();
            message.Stream.Close();
        }
    }
}

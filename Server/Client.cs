using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class Client
    {
        public byte ID;
        public IPEndPoint IP;

        public ConnectionStep ClientConnectionStep;

        public bool Dead;

        public enum ConnectionStep
        {
            None = 0,
            Hello = 1,
            Playing = 4,
            Dead = 5,
        }

        public Client()
        {
            this.ID = Server.currID++;
            if (Server.currID == 0xFF)
                Server.currID = 0x0;
        }

        public void ProcessClientHello(IPEndPoint ip)
        {
            if (ClientConnectionStep == ConnectionStep.None)
                ClientConnectionStep = ConnectionStep.Hello;

            IP = ip;
       
            //using (var cc = new ConsoleCopy(Settings.LogFile))
            //{
            Console.WriteLine("Client Hello! IP: {0}", ip);
            //}

            //TODO
            Server.instance.SendServerHello(IP, ID, 3567);

            ClientConnectionStep = ConnectionStep.Playing;
        }

        public void ProcessDisconnect()
        {
            Dead = true;
            Console.WriteLine("Client Disconnected. IP: {0}", IP);
        }

    }
}

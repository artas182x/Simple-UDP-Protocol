using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace Client
{
    public partial class Form1 : Form
    {
        class PaintData
        {
            public Color Color { get; set; }
            public Point StartPos { get; set; }

            public PaintData(Color color, Point point)
            {
                Color = color;
                StartPos = point;
            }
        };

        UdpClient udpClient;
        Color myColor;
        Dictionary<byte, PaintData> udpClients;
        Client client;


        public Form1()
        {
            InitializeComponent();
            pictureBox1.Image = new Bitmap(pictureBox1.Width, pictureBox1.Height);

            udpClient = new UdpClient();
            myColor = Color.Black;
            udpClients = new Dictionary<byte, PaintData>();

            Client.form1 = this;

        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            IPEndPoint IP = new IPEndPoint(IPAddress.Parse(txtIP.Text), (int)numPort.Value);
            client = new Client(IP);
            client.SendClientHello();
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            if (client != null)
                client.SendDisconnected();
        }


        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (client != null)
                client.SendColor(myColor.ToArgb());
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            if (client != null)
                client.SendDrawing((short)e.Location.X, (short)e.Location.Y);
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (client != null)
                client.StopDrawing();
        }

        private void btnColor_Click(object sender, EventArgs e)
        {
            if (dlgColorpicker.ShowDialog() == DialogResult.OK)
            {
                myColor = dlgColorpicker.Color;
            }
        }
    }
}

using System.Threading;

namespace Server
{
    class Program
	{

		static void Main(string[] args)
		{
			Program p = new Program();

            Server server = new Server();
            server.Start();

			while(true)
			{
				Thread.Sleep(1000);
			}
		}

      
	
	}
}

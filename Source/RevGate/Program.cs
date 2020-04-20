using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RevGate
{
    // 15779  => 28362
    // 15884  => 23781
    internal class Program
    {
        public static Program GetProgram;

        public static void Main(string[] args)
        {
            var ci = new CultureInfo("tr-TR");
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.WriteLine("Hello World!");

            new Program().MainTask().GetAwaiter().GetResult();
        }

        private Task MainTask()
        {
            try
            {
                GetProgram = this;

                var server = new GateServer(IPAddress.Any, 15779);

                Console.Write("Server starting...");
                server.Start();
                Console.WriteLine("Done!");

                while (true)
                {
                    foreach (var session in server.Clients.ToList())
                    {
                        //Console.Clear();
                        Console.WriteLine("-");
                        Console.WriteLine($"BytesReceived: {session.BytesReceived}");
                        Console.WriteLine($"BytesPending: {session.BytesPending}");
                        Console.WriteLine($"BytesSent: {session.BytesSent}");
                        Console.WriteLine($"Session Count: {server.Clients.Count}");
                    }

                    //var input = Console.ReadLine();
                    Thread.Sleep(1000);
                }
                var input = Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }
    }
}
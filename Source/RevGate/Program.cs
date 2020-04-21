using RevGate.ServerHandlers;
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

                var gateway = new Gateway(IPAddress.Parse("10.0.0.0"), 15779);
                var agent = new Agent(IPAddress.Parse("10.0.0.0"), 15884);
                gateway.Start();
                agent.Start();

                while (true)
                {
                    foreach (var session in gateway.Clients.ToList())
                    {
                        //Console.Clear();
                        Console.WriteLine("-");
                        Console.WriteLine($"BytesReceived: {session.BytesReceived}");
                        Console.WriteLine($"BytesPending: {session.BytesPending}");
                        Console.WriteLine($"BytesSent: {session.BytesSent}");
                        Console.WriteLine($"Session Count: {gateway.Clients.Count}");
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
using RevGate.ServerHandlers;
using System;
using System.Globalization;
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

                var gateway = new Gateway("10.0.0.0", 15779);
                gateway.Start();
                //var agent = new Agent(IPAddress.Parse("10.0.0.0"), 15884);
                //agent.Start();

                while (true)
                {
                    //Console.WriteLine($"Active Sessions: {gateway.Sessions.Count}");

                    ////var input = Console.ReadLine();
                    //Thread.Sleep(1000);
                    var input = Console.ReadLine();
                    Thread.Sleep(2000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }
    }
}
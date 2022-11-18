using SimpleTunnelVisitor;
using System.Net;
using System.Net.Sockets;

namespace SimpleTunnelVisitor
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine("Hello, World!");
            //new SendTest().Start();
            new V1.Visitor().Start();


            Thread.Sleep(Timeout.Infinite);
        }

       




       



    }
}
using System;
using System.Text;

namespace SimpleTunnelServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine("Hello, World!");
            //Console.WriteLine(int.MaxValue);
            //TestBSearch();
            //new V1.Main().Start();
            new V1.Server().Start();


            Thread.Sleep(Timeout.Infinite);
        }


        static void TestBSearch()
        {
            var array = Encoding.UTF8.GetBytes("KMS\r\nOPOQ\rNTK\r\nAKB");
            var idx = Array.BinarySearch<byte>(array, 0, array.Length, 13);
            var buff = Encoding.UTF8.GetString(array, idx, 5);
            Console.WriteLine(buff);


        }



    }
}
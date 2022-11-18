namespace SimpleTunnelServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine("Hello, World!");
            //Console.WriteLine(int.MaxValue);
            //new V1.Main().Start();
            new V1.Server().Start();


            Thread.Sleep(Timeout.Infinite);
        }
    }
}
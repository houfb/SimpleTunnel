namespace SimpleTunnelClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine("Hello, World!");
            new V1.Client().Start();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
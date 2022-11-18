using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
namespace SimpleTunnel
{
    internal class Program
    {
        internal static async Task Main(string[] args)
        {
            //Console.WriteLine("Hello, World!");

            IHostBuilder builder = Host.CreateDefaultBuilder(args) //CreateDefaultBuilder的参数是可选的，下国的ConfigureDefaults或许就是为了补充这个的吧，因为暂时还没有找到更多的相关文档。
                  .ConfigureDefaults(args)                     //或许就只是为了补充上边的args参数及其他默认值，但从MSDN看，似乎这个函数能覆掉IHostBuilder身上的所有现有配置，大有把它重新初始化恢复成一个默认配置时的样子。
                  .ConfigureHostOptions(host => { })           //
                  .ConfigureHostConfiguration(config => {   })   //
                  .ConfigureAppConfiguration(config => { })
                  .ConfigureServices(services => { })
                  .ConfigureLogging(logging => { })
                  //.ConfigureContainer<T>(containBuilder=> { }) 
                  ;
            IHost host = builder.Build();
            await host.RunAsync();

        }

        


    }
}
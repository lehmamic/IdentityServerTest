using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace Diskordia.TestApi
{
  public class Program
  {
    public static void Main(string[] args)
    {
      Console.Title = "API";

      var host = new WebHostBuilder()
        .UseKestrel()
        .UseUrls("http://localhost:5001")
        .UseContentRoot(Directory.GetCurrentDirectory())
        .UseIISIntegration()
        .UseStartup<Startup>()
        .Build();

      host.Run();
    }
  }
}

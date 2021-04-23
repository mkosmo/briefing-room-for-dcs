using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BriefingRoom4DCSWorld.DB;
using BriefingRoom4DCSWorld.Debug;

namespace GUI_API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            DebugLog.Instance.CreateLogFileWriter();
            Database.Instance.Initialize(); 
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}

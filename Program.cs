using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.MSSqlServer;
using Serilog.Sinks.SystemConsole.Themes;

namespace SiteRedirectTemplate
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.SetBasePath(Directory.GetCurrentDirectory());
                    configApp.AddJsonFile("appsettings.json", false, true);
                    configApp.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", true, true);
                    configApp.AddEnvironmentVariables();
                    configApp.AddCommandLine(args);

                    if (hostContext.HostingEnvironment.IsDevelopment())
                    {
                        configApp.AddUserSecrets<Program>();
                    }
                })
                .UseSerilog((hostingContext, loggerConfiguration) =>
                {
                    var columnOptions = new ColumnOptions
                    {
                        ClusteredColumnstoreIndex = false,
                        DisableTriggers = true,
                        AdditionalColumns = new Collection<SqlColumn>
                        {
                            new SqlColumn("Application", SqlDbType.VarChar, true, 50) {NonClusteredIndex = true},
                            new SqlColumn("Environment", SqlDbType.VarChar, true, 50),
                            new SqlColumn("BuildNumber", SqlDbType.VarChar, true, 50),
                            new SqlColumn("RequestPath", SqlDbType.VarChar, true, 255)
                        }
                    };
                    columnOptions.Store.Add(StandardColumn.LogEvent);
                    columnOptions.Store.Remove(StandardColumn.Properties);
                    columnOptions.PrimaryKey = columnOptions.Id;
                    columnOptions.Id.NonClusteredIndex = true;

                    columnOptions.Level.ColumnName = "Severity";
                    columnOptions.Level.DataLength = 15;

                    loggerConfiguration
                        .ReadFrom.Configuration(hostingContext.Configuration)
                        .Enrich.FromLogContext()
                        .Enrich.WithExceptionDetails()
                        .Enrich.WithProperty("Application", hostingContext.Configuration["LogTitle"])
                        .Enrich.WithProperty("Environment", hostingContext.HostingEnvironment.EnvironmentName)
                        .Enrich.WithProperty("BuildNumber", hostingContext.Configuration["BuildNumber"])
                        .WriteTo.MSSqlServer(hostingContext.Configuration.GetConnectionString("LogsConnection"), tableName: "Logs", columnOptions: columnOptions, autoCreateSqlTable: true, batchPostingLimit: 50, period: new TimeSpan(0, 0, 5))
                        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {ThreadId} {EventType:x8} {Level:u3}] {Message:lj}{NewLine}{Exception}", theme: AnsiConsoleTheme.Code);
                });
    }
}

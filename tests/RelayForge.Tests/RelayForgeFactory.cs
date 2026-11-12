using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RelayForge.Data;

namespace RelayForge.Tests;

public class RelayForgeFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"relayforge-tests-{Guid.NewGuid()}.db");

    private readonly bool _runBackgroundServices;

    public RelayForgeFactory(bool runBackgroundServices = false)
    {
        // we don't need polling services for the majority of tests, so default false
        _runBackgroundServices = runBackgroundServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // remove service workers for concurrency tests
            if (!_runBackgroundServices) services.RemoveAll<IHostedService>();
            
            services.RemoveAll<DbContextOptions<RelayForgeDbContext>>();
            services.RemoveAll<RelayForgeDbContext>();

            services.AddDbContext<RelayForgeDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath}"));
            
            var serviceProvider = services.BuildServiceProvider();
            
            using var scope = serviceProvider.CreateScope();
            var db =  scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
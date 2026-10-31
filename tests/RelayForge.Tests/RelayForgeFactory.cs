using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RelayForge.Data;

namespace RelayForge.Tests;

public class RelayForgeFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"relayforge-tests-{Guid.NewGuid()}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
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
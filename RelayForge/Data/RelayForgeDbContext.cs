using Microsoft.EntityFrameworkCore;
using RelayForge.Models;

namespace RelayForge.Data;

public class RelayForgeDbContext : DbContext
{
    public RelayForgeDbContext(
        DbContextOptions<RelayForgeDbContext> options) 
        :  base(options)
    {
        
    }
    
    public DbSet<Event> Events { get; set; }
}
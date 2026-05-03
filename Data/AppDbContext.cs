using Microsoft.EntityFrameworkCore;
using ServerApi.Models;

namespace ServerApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ShortUrl> ShortUrls { get; set; }
    public DbSet<User> Users { get; set; }
}
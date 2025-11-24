using Microsoft.EntityFrameworkCore;

namespace PhotoFrame.DataAccess;

public class MediaDbContext : DbContext
{
    public DbSet<MediaFile> Media { get; set; }

    public MediaDbContext(DbContextOptions<MediaDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Path).IsRequired();
            entity.Property(e => e.TimesShown).HasDefaultValue(0);
        });
    }
}

using FileServer_POC.Models;
using Microsoft.EntityFrameworkCore;

namespace FileServer_POC.Entities
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions options) : base(options) { }
        public DbSet<FileMetadata> FileMetadata { get; set; }

    }
}

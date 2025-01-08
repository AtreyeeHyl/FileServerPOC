using FileServer_POC.Entities;
using FileServer_POC.Models;
using Microsoft.EntityFrameworkCore;

namespace FileServer_POC.Repositories
{
    public class FileRepository : IFileRepository
    {
        private readonly ApplicationDbContext _context;

        public FileRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<FileMetadata> GetMetadataByIdAsync(int id)
        {
            return await _context.FileMetadata.FindAsync(id);
        }

        public async Task AddMetadataAsync(FileMetadata metadata)
        {
            _context.FileMetadata.Add(metadata);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> FileNameExistsAsync(string fileName)
        {
            return await _context.FileMetadata.AnyAsync(m => m.FileName == fileName);
        }

    }
}

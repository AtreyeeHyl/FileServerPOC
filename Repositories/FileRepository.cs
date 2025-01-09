using FileServer_POC.Entities;
using FileServer_POC.Models;
using Microsoft.AspNetCore.Mvc;
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

        public async Task<List<FileMetadata>> GetAllMetadataAsync()
        {
            return await _context.FileMetadata.ToListAsync();
        }

        //returns single value
        public async Task<FileMetadata> GetMetadataByIdAsync(int id)
        {
            return await _context.FileMetadata.FindAsync(id);
        }

        //returns multiple values
        public async Task<List<FileMetadata>> GetMetadataByIdsAsync(int[] ids)
        {
            return await _context.FileMetadata
                .Where(m => ids.Contains(m.Id))
                .ToListAsync();
        }

        public async Task SaveMetadataAsync(FileMetadata metadata)
        {
            _context.FileMetadata.Add(metadata);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> FileNameExistsAsync(string fileName)
        {
            return await _context.FileMetadata.AnyAsync(m => m.FileName == fileName);
        }

        public async Task DeleteMetadataAsync(int id)
        {
            var metadata = await _context.FileMetadata.FindAsync(id);
            if (metadata != null)
            {
                _context.FileMetadata.Remove(metadata);
                await _context.SaveChangesAsync();
            }
        }



    }
}

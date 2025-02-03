using FileServer_POC.DTOs;
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

        public async Task<List<FileMetadata>> GetAllMetadataAsync(string? filterOn = null, string? filterQuery = null)
        {
            // Start with a queryable object
            var query = _context.FileMetadata.AsQueryable();

            // Apply filtering based on the filterOn and filterQuery parameters
            if (!string.IsNullOrEmpty(filterOn) && !string.IsNullOrEmpty(filterQuery))
            {
                query = filterOn.ToLower() switch
                {
                    "filename" => query.Where(file => file.FileName != null &&
                                                      EF.Functions.Like(file.FileName, $"%{filterQuery}%")),
                    "filepath" => query.Where(file => file.FilePath != null &&
                                          EF.Functions.Like(file.FilePath, $"%{filterQuery}%")),
                    "filesize" => int.TryParse(filterQuery, out var maxSize)
                        ? query.Where(file => file.FileSize <= maxSize)
                        : query, // If parsing fails, no filtering for filesize
                    "filetype" => query.Where(file => file.FileType != null &&
                                                      EF.Functions.Like(file.FileType, $"%{filterQuery}%")),
                    _ => query // If filterOn is not recognized, return unfiltered query
                };
            }

            // Execute the query and return the results
            return await query.ToListAsync();
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

        public async Task UpdateMetadataAsync(FileMetadata metadata)
        {
            _context.FileMetadata.Update(metadata);
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

            //Check if the table is empty and reset the identity seed
            if (!await _context.FileMetadata.AnyAsync())
            {
                // Reset the identity seed for the FileMetadata table
                //await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('FileMetadata', RESEED, 0);");
                await _context.Database.ExecuteSqlRawAsync("ALTER TABLE FileMetadata AUTO_INCREMENT = 1;");
            }

        }

        public async Task<List<FileMetadata>> GetAllMetadataByDateAsync(DateTime? startDate = null, DateTime? endDate = null)
        {

            // Start with all files
            var query = _context.FileMetadata.AsQueryable();

            // Apply the date filter if both startDate and endDate are provided
            if (startDate.HasValue && endDate.HasValue)
            {
                query = query.Where(file => file.UploadDate >= startDate.Value && file.UploadDate <= endDate.Value);
            }
            else if (startDate.HasValue)
            {
                query = query.Where(file => file.UploadDate >= startDate.Value);
            }
            else if (endDate.HasValue)
            {
                query = query.Where(file => file.UploadDate <= endDate.Value);
            }

            // Execute the query and return the result as a list of FileMetadata
            var files = await query.ToListAsync();

            return files;
        }
    }
}

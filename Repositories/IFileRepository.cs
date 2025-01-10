using FileServer_POC.Models;
using Microsoft.AspNetCore.Mvc;


namespace FileServer_POC.Repositories
{
    public interface IFileRepository
    {
        Task<List<FileMetadata>> GetAllMetadataAsync(string? filterOn = null, string? filterQuery = null);
        Task<List<FileMetadata>> GetAllMetadataByDateAsync(DateTime? startDate = null, DateTime? endDate = null);

        Task<FileMetadata> GetMetadataByIdAsync(int id);

        Task<List<FileMetadata>> GetMetadataByIdsAsync(int[] ids);

        Task SaveMetadataAsync(FileMetadata metadata);

        Task DeleteMetadataAsync(int id);
        Task UpdateMetadataAsync(FileMetadata metadata);
    }
}

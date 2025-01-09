using FileServer_POC.Models;


namespace FileServer_POC.Repositories
{
    public interface IFileRepository
    {
        Task<FileMetadata> GetMetadataByIdAsync(int id);

        Task<List<FileMetadata>> GetMetadataByIdsAsync(int[] ids);

        Task SaveMetadataAsync(FileMetadata metadata);

        Task DeleteMetadataAsync(int id);
    }
}

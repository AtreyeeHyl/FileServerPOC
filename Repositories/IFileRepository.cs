using FileServer_POC.Models;
using System.Threading.Tasks;

namespace FileServer_POC.Repositories
{
    public interface IFileRepository
    {
        Task<FileMetadata> GetMetadataByIdAsync(int id);
        Task AddMetadataAsync(FileMetadata metadata);
        Task<bool> FileNameExistsAsync(string fileName);
    }
}

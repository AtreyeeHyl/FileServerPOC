using FileServer_POC.DTOs;
using FileServer_POC.Models;


namespace FileServer_POC.Services
{
    public interface IFileService
    {
        Task<FileOperationResponse> UploadFilesAsync(List<IFormFile> files);
        Task<List<GetAllFilesResponse>> GetAllFilesAsync();

        Task<GetFileByIdResponse> GetFileByIdAsync(int id);

        Task<FileOperationResponse> DeleteFilesAndMetadataAsync(int[] ids);
    }
}

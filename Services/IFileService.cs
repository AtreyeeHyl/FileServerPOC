using FileServer_POC.DTOs;
using FileServer_POC.Models;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace FileServer_POC.Services
{
    public interface IFileService
    {
        Task<object> UploadFileAsync(IFormFile file);
        Task<GetFileByIdResponse> GetFileByIdAsync(int id);
        Task<string> GenerateUniqueFileNameAsync(string originalFileName);
        Task<DeleteFileResult> DeleteFilesAndMetadataAsync(int[] ids);
    }
}

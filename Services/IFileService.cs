using FileServer_POC.DTOs;
using FileServer_POC.Models;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace FileServer_POC.Services
{
    public interface IFileService
    {
        Task<FileOperationResponse> UploadFilesAsync(List<IFormFile> files);
        Task<GetFileByIdResponse> GetFileByIdAsync(int id);
        Task<FileOperationResponse> DeleteFilesAndMetadataAsync(int[] ids);
    }
}

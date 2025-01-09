using FileServer_POC.DTOs;
using FileServer_POC.Models;
using Microsoft.AspNetCore.Mvc;


namespace FileServer_POC.Services
{
    public interface IFileService
    {
        Task<FileOperationDTO> UploadFilesAsync(List<IFormFile> files);
        Task<List<GetFileDTO>> GetAllFilesAsync([FromQuery] string? filterOn = null, [FromQuery] string? filterQuery = null);

        Task<GetFileDTO> GetFileByIdAsync(int id);

        Task<FileOperationDTO> DeleteFilesAndMetadataAsync(int[] ids);
    }
}

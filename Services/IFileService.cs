using FileServer_POC.DTOs;
using FileServer_POC.Models;
using Microsoft.AspNetCore.Mvc;


namespace FileServer_POC.Services
{
    public interface IFileService
    {
        Task<FileOperationDTO> UploadFilesAsync(List<IFormFile> files);

        Task<List<GetFileDTO>> GetAllFilesAsync(string? filterOn = null, string? filterQuery = null);
        Task<List<GetFileDTO>> GetAllFilesStreamAsync(string? filterOn = null, string? filterQuery = null);
        Task<List<GetFileDTO>> GetAllFilesByDateRangeAsync(DateTime? startDate=null, DateTime? endDate=null);
        Task<GetFileDTO> GetFileByIdAsync(int id);
        Task<FileOperationDTO> UpdateFileByIdAsync(int id, IFormFile file);
        Task<FileOperationDTO> UpdateFileNameAndMetadataAsync(int id, string newFileName);
        Task<FileOperationDTO> DeleteFilesAndMetadataAsync(int[] ids);
        Task<FileOperationDTO> DeleteFilesAsync(int[] ids);
    }
}

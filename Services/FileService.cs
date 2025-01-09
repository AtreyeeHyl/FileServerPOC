using FileServer_POC.DTOs;
using FileServer_POC.Repositories;
using FileServer_POC.Services.Utilities;
using FileServer_POC.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace FileServer_POC.Services
{
    public class FileService : IFileService
    {
        private readonly IFileRepository _fileRepository;
        private readonly FileStorageHelper _fileStorageHelper;
        private readonly FileMetadataHelper _fileMetadataHelper;
        private readonly FileValidationHelper _fileValidationHelper;
        private readonly ZipProcessingHelper _zipProcessingHelper;

        public FileService(IFileRepository fileRepository)
        {
            _fileRepository = fileRepository;
            _fileStorageHelper = new FileStorageHelper();
            _fileMetadataHelper = new FileMetadataHelper(_fileRepository);
            _fileValidationHelper = new FileValidationHelper();
            _zipProcessingHelper = new ZipProcessingHelper(_fileStorageHelper, _fileMetadataHelper);
        }

        public async Task<FileOperationDTO> UploadFilesAsync(List<IFormFile> files)
        {
            var errors = new List<FileErrorDTO>();
            var uploadDirPath = _fileStorageHelper.EnsureUploadDirectoryExists();

            foreach (var file in files)
            {
                if (!_fileValidationHelper.IsValidFile(file, errors)) continue;

                try
                {
                    if (_fileValidationHelper.IsZipFile(file))
                    {
                        await _zipProcessingHelper.ProcessZipFileAsync(file, uploadDirPath, errors);
                    }
                    else
                    {
                        await _fileStorageHelper.SaveRegularFileAsync(file, uploadDirPath, errors, _fileMetadataHelper);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new FileErrorDTO
                    {
                        FileName = file.FileName,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return new FileOperationDTO
            {
                Success = errors.Count == 0,
                Message = errors.Count == 0 ? "All files uploaded successfully." : "Partial success in file upload.",
                Errors = errors
            };
        }

        public async Task<List<GetFileDTO>> GetAllFilesAsync([FromQuery] string? filterOn = null, [FromQuery] string? filterQuery = null)
        {
            var files = await _fileRepository.GetAllMetadataAsync();

            // Apply filtering if parameters are provided
            if (!string.IsNullOrEmpty(filterOn) && !string.IsNullOrEmpty(filterQuery))
            {
                files = filterOn.ToLower() switch
                {
                    "filename" => files.Where(file => file.FileName != null && file.FileName.Contains(filterQuery, StringComparison.OrdinalIgnoreCase)).ToList(),
                    "filesize" => int.TryParse(filterQuery, out var maxSize)
                    ? files.Where(file => file.FileSize <= maxSize).ToList()
                    : files, // If parsing fails, return original list
                    "filetype" => files.Where(file => file.FileType != null && file.FileType.Contains(filterQuery, StringComparison.OrdinalIgnoreCase)).ToList(),
                    _ => files // If the filterOn value is not recognized, return the original list
                };
            }

            //Convert to DTO
            return files.Select(file => new GetFileDTO
            {
                FileId = file.Id,
                FileName = file.FileName,
                FileType = file.FileType,
                FilePath = file.FilePath,
                FileSize = file.FileSize,
                UploadDate = file.UploadDate
            }).ToList();
        }

        public async Task<GetFileDTO> GetFileByIdAsync(int id)
        {
            var metadata = await _fileRepository.GetMetadataByIdAsync(id);

            if (metadata == null || !_fileStorageHelper.FileExists(metadata.FilePath))
                return null;

            var fileStream = new FileStream(metadata.FilePath, FileMode.Open, FileAccess.Read);
            return new GetFileDTO
            {
                FileStream = fileStream,
                FileName = metadata.FileName
            };
        }

        public async Task<FileOperationDTO> DeleteFilesAndMetadataAsync(int[] ids)
        {
            var filesToDelete = await _fileRepository.GetMetadataByIdsAsync(ids);
            var result = new FileOperationDTO
            {
                Success = true,
                Message = "All files deleted successfully."
            };

            foreach (var metadata in filesToDelete)
            {
                var fileDeleted = _fileStorageHelper.DeleteFile(metadata.FilePath, metadata.Id, result);
                if (fileDeleted)
                {
                    await _fileMetadataHelper.DeleteMetadataAsync(metadata.Id, result);
                }
            }

            if (result.Errors.Count > 0)
            {
                result.Success = false;
                result.Message = "Partial success in file deletion.";
            }

            return result;
        }

    }
}

using FileServer_POC.DTOs;
using FileServer_POC.Repositories;
using FileServer_POC.Services.Utilities;
using FileServer_POC.Helpers;
using Microsoft.AspNetCore.Mvc;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;

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

 

        public async Task<List<GetFileDTO>> GetAllFilesAsync(string? filterOn = null, string? filterQuery = null)
        {
            var files = await _fileRepository.GetAllMetadataAsync(filterOn, filterQuery);

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

        public async Task<List<GetFileDTO>> GetAllFilesStreamAsync(string? filterOn = null, string? filterQuery = null)
        {
            var files = await _fileRepository.GetAllMetadataAsync(filterOn, filterQuery);

            //Convert to DTO
            return files.Select(file => new GetFileDTO
            {
                FileStream = new FileStream(file.FilePath, FileMode.Open, FileAccess.Read),
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

        public async Task<FileOperationDTO> UpdateFileByIdAsync(int id, IFormFile file)
        {
            var metadata = await _fileRepository.GetMetadataByIdAsync(id);

            if (metadata == null)
            {
                return new FileOperationDTO
                {
                    Success = false,
                    Message = $"File with ID {id} not found!",

                };
            }

            try
            {
                
                await DeleteFilesAsync([id]);

                var uploadDirPath = _fileStorageHelper.EnsureUploadDirectoryExists();
                var errors = new List<FileErrorDTO>();

                await _fileStorageHelper.UpdateRegularFileAsync(file, uploadDirPath, errors, _fileMetadataHelper, metadata);
              
                return new FileOperationDTO
                {
                    Success = true,
                    Message = $"File with ID {id} is updated successfully!",

                };

            }
            catch (Exception ex)
            {
                return new FileOperationDTO
                {
                    Success = false,
                    Message = $"File with ID {id} not found!",

                };
            }



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

        public async Task<FileOperationDTO> DeleteFilesAsync(int[] ids)
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
            }

            if (result.Errors.Count > 0)
            {
                result.Success = false;
                result.Message = "Partial success in file deletion.";
            }

            return result;
        }

        public async Task<FileOperationDTO> UpdateFileNameAndMetadataAsync(int id, string newFileName)
        {
            var metadata = await _fileRepository.GetMetadataByIdAsync(id);

            if (metadata == null || !_fileStorageHelper.FileExists(metadata.FilePath))
            {
                return new FileOperationDTO
                {
                    Success = false,
                    Message = $"File with ID {id} not found!",
                };
            }

            try
            {
                // Rename the file in the storage
                var directory = Path.GetDirectoryName(metadata.FilePath);
                var newFilePath = Path.Combine(directory, newFileName);

                // Rename the file in the storage system
                File.Move(metadata.FilePath, newFilePath);

                // Update the metadata
                metadata.FileName = newFileName;
                metadata.FilePath = newFilePath;

                await _fileRepository.UpdateMetadataAsync(metadata);

                return new FileOperationDTO
                {
                    Success = true,
                    Message = $"File name and metadata updated successfully for ID {id}.",
                };
            }
            catch (Exception ex)
            {
                return new FileOperationDTO
                {
                    Success = false,
                    Message = $"Error updating file: {ex.Message}",
                };
            }
        }

        public async Task<List<GetFileDTO>> GetAllFilesByDateRangeAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            // Fetch files from the repository by date range
            var filesMetadata = await _fileRepository.GetAllMetadataByDateAsync(startDate, endDate);

            // Convert the list of FileMetadata to GetFileDTO
            var filesDTO = filesMetadata.Select(file => new GetFileDTO
            {
                FileId = file.Id,
                FileName = file.FileName,
                FileSize = file.FileSize,
                FileType = file.FileType,
                UploadDate = file.UploadDate
            }).ToList();

            return filesDTO;
        }
    }
}

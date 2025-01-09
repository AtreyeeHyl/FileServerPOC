using FileServer_POC.DTOs;
using FileServer_POC.Models;
using FileServer_POC.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FileServer_POC.Services
{
    public class FileService : IFileService
    {
        private readonly IFileRepository _fileRepository;

        public FileService(IFileRepository fileRepository)
        {
            _fileRepository = fileRepository;
        }

        public async Task<FileOperationResponse> UploadFilesAsync(List<IFormFile> files)
        {
            var errors = new List<FileError>();
            var uploadDirPath = EnsureUploadDirectoryExists();

            foreach (var file in files)
            {
                if (file.Length == 0)
                {
                    errors.Add(new FileError
                    {
                        FileName = file.FileName,
                        ErrorMessage = "File is empty."
                    });
                    continue;
                }

                try
                {
                    if (IsZipFile(file))
                    {
                        await ProcessZipFileAsync(file, uploadDirPath, errors);
                    }
                    else
                    {
                        await ProcessRegularFileAsync(file, uploadDirPath, errors);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new FileError
                    {
                        FileName = file.FileName,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return new FileOperationResponse
            {
                Success = errors.Count == 0,
                Message = errors.Count == 0 ? "All files uploaded successfully." : "Partial success in file upload.",
                Errors = errors
            };
        }

        private string EnsureUploadDirectoryExists()
        {
            var uploadDirPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadDirPath))
            {
                Directory.CreateDirectory(uploadDirPath);
            }
            return uploadDirPath;
        }

        private bool IsZipFile(IFormFile file)
        {
            return Path.GetExtension(file.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ProcessZipFileAsync(IFormFile zipFile, string uploadDirPath, List<FileError> errors)
        {
            var tempZipPath = Path.Combine(uploadDirPath, zipFile.FileName);
            using (var zipStream = new FileStream(tempZipPath, FileMode.Create))
            {
                await zipFile.CopyToAsync(zipStream);
            }

            var extractDirPath = GenerateUniqueFolderName(uploadDirPath, Path.GetFileNameWithoutExtension(zipFile.FileName));

            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(tempZipPath, extractDirPath);
                foreach (var extractedFilePath in Directory.GetFiles(extractDirPath))
                {

                    var extractedFile = new FileInfo(extractedFilePath);
                    var uniqueFilePath = GenerateUniqueFileName(uploadDirPath, extractedFile.Name);

                    var metadata = await CreateAndSaveFileMetadataAsync(uniqueFilePath, extractedFile.FullName, extractedFile.Length);
                }
            }
            catch (Exception ex)
            {
                errors.Add(new FileError
                {
                    FileName = zipFile.FileName,
                    ErrorMessage = $"Failed to extract ZIP file: {ex.Message}"
                });
            }
            finally
            {
                System.IO.File.Delete(tempZipPath);
            }
        }

        private async Task ProcessRegularFileAsync(IFormFile file, string uploadDirPath, List<FileError> errors)
        {
            try
            {
                var uploadFilePath = GenerateUniqueFileName(uploadDirPath, file.FileName);
                using (var stream = new FileStream(uploadFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                await CreateAndSaveFileMetadataAsync(file.FileName, uploadFilePath, file.Length);
            }
            catch (Exception ex)
            {
                errors.Add(new FileError
                {
                    FileName = file.FileName,
                    ErrorMessage = ex.Message
                });
            }
        }

        private string GenerateUniqueFolderName(string baseDir, string folderName)
        {
            var uniqueFolderName = folderName;
            var counter = 1;

            while (Directory.Exists(Path.Combine(baseDir, uniqueFolderName)))
            {
                uniqueFolderName = $"{folderName}({counter++})";
            }

            return Path.Combine(baseDir, uniqueFolderName);
        }

        private string GenerateUniqueFileName(string baseDir, string fileName)
        {
            var uniqueFileName = fileName;
            var counter = 1;

            while (File.Exists(Path.Combine(baseDir, uniqueFileName)))
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                var fileExtension = Path.GetExtension(fileName);
                uniqueFileName = $"{fileNameWithoutExtension}({counter++}){fileExtension}";
            }

            return Path.Combine(baseDir, uniqueFileName);
        }

        private async Task<FileMetadata> CreateAndSaveFileMetadataAsync(string fileName, string filePath, long fileSize)
        {
            var metadata = new FileMetadata
            {
                FileName = fileName,
                FilePath = filePath,
                FileSize = fileSize,
                UploadDate = DateTime.UtcNow
            };

            await _fileRepository.SaveMetadataAsync(metadata);
            return metadata;
        }

        public async Task<GetFileByIdResponse> GetFileByIdAsync(int id)
        {
            var metadata = await _fileRepository.GetMetadataByIdAsync(id);

            if (metadata == null || !File.Exists(metadata.FilePath))
                return null;

            var fileStream = new FileStream(metadata.FilePath, FileMode.Open, FileAccess.Read);
            return new GetFileByIdResponse
            {
                FileStream = fileStream,
                FileName = metadata.FileName
            };
        }

        public async Task<FileOperationResponse> DeleteFilesAndMetadataAsync(int[] ids)
        {
            var filesToDelete = await _fileRepository.GetMetadataByIdsAsync(ids);
            var result = new FileOperationResponse
            {
                Success = true, 
                Message = "All files deleted successfully." 
            };

            foreach (var metadata in filesToDelete)
            {
                var fileDeleted = DeleteFileFromDisk(metadata.FilePath, metadata.Id, result);
                if (fileDeleted)
                {
                    await DeleteMetadataAsync(metadata.Id, result);
                }
            }

            if (result.Errors.Count > 0)
            {
                result.Success = false;
                result.Message = "Partial success in file deletion.";
            }

            return result;
        }

        private bool DeleteFileFromDisk(string filePath, int metadataId, FileOperationResponse result)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                else
                {
                    result.Errors.Add(new FileError
                    {
                        FileId = metadataId,
                        ErrorMessage = "File not found on disk."
                    });
                    return false;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(new FileError
                {
                    FileId = metadataId,
                    ErrorMessage = $"Error deleting file: {ex.Message}"
                });
                return false;
            }
        }

        private async Task DeleteMetadataAsync(int metadataId, FileOperationResponse result)
        {
            try
            {
                await _fileRepository.DeleteMetadataAsync(metadataId);
            }
            catch (Exception ex)
            {
                result.Errors.Add(new FileError
                {
                    FileId = metadataId,
                    ErrorMessage = $"Error deleting metadata: {ex.Message}"
                });
            }
        }

    }
}

using FileServer_POC.DTOs;
using FileServer_POC.Helpers;
using FileServer_POC.Models;
using Microsoft.VisualBasic.FileIO;

namespace FileServer_POC.Services.Utilities
{
    public class FileStorageHelper
    {
        public string EnsureUploadDirectoryExists()
        {
            var uploadDirPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadDirPath))
            {
                Directory.CreateDirectory(uploadDirPath);
            }
            return uploadDirPath;
        }

        public async Task SaveRegularFileAsync(IFormFile file, string uploadDirPath, List<FileErrorDTO> errors, FileMetadataHelper metadataHelper)
        {
            try
            {
                var uploadFilePath = GenerateUniqueFileName(uploadDirPath, file.FileName);
                using (var stream = new FileStream(uploadFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                await metadataHelper.CreateAndSaveFileMetadataAsync(file.FileName, uploadFilePath, file.Length);
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

        public async Task UpdateRegularFileAsync(IFormFile file, string uploadDirPath, List<FileErrorDTO> errors, FileMetadataHelper metadataHelper, FileMetadata metadata)
        {
            try
            {
                var uploadFilePath = GenerateUniqueFileName(uploadDirPath, file.FileName);
                using (var stream = new FileStream(uploadFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                metadata.FileName = file.FileName;
                metadata.FileType = Path.GetExtension(file.FileName);
                metadata.FilePath = uploadFilePath;
                metadata.FileSize = file.Length;
                metadata.UploadDate = DateTime.UtcNow;

                await metadataHelper.UpdateFileMetadataAsync(metadata);
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

        public bool DeleteFile(string filePath, int metadataId, FileOperationDTO result)
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
                    result.Errors.Add(new FileErrorDTO
                    {
                        FileId = metadataId,
                        ErrorMessage = "File not found on disk."
                    });
                    return false;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(new FileErrorDTO
                {
                    FileId = metadataId,
                    ErrorMessage = $"Error deleting file: {ex.Message}"
                });
                return false;
            }
        }

        public string GenerateUniqueFileName(string baseDir, string fileName)
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

        public bool FileExists(string filePath) => File.Exists(filePath);
    }
}

using FileServer_POC.DTOs;
using FileServer_POC.Helpers;

namespace FileServer_POC.Services.Utilities
{
    public class ZipProcessingHelper
    {
        private readonly FileStorageHelper _fileStorageHelper;
        private readonly FileMetadataHelper _fileMetadataHelper;

        public ZipProcessingHelper(FileStorageHelper fileStorageHelper, FileMetadataHelper fileMetadataHelper)
        {
            _fileStorageHelper = fileStorageHelper;
            _fileMetadataHelper = fileMetadataHelper;
        }

        public async Task ProcessZipFileAsync(IFormFile zipFile, string uploadDirPath, List<FileError> errors)
        {
            var tempZipPath = Path.Combine(uploadDirPath, zipFile.FileName);
            using (var zipStream = new FileStream(tempZipPath, FileMode.Create))
            {
                await zipFile.CopyToAsync(zipStream);
            }

            var extractDirPath = _fileStorageHelper.GenerateUniqueFileName(uploadDirPath, Path.GetFileNameWithoutExtension(zipFile.FileName));

            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(tempZipPath, extractDirPath);
                foreach (var extractedFilePath in Directory.GetFiles(extractDirPath))
                {
                    var extractedFile = new FileInfo(extractedFilePath);
                    await _fileMetadataHelper.CreateAndSaveFileMetadataAsync(extractedFile.Name, extractedFile.FullName, extractedFile.Length);
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
                File.Delete(tempZipPath);
            }
        }
    }

}

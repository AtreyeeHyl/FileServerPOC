using Amazon.S3;
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

        //public async Task ProcessZipFileAsync(IFormFile zipFile, string uploadDirPath, List<FileErrorDTO> errors)
        //{
        //    var tempZipPath = Path.Combine(uploadDirPath, zipFile.FileName);
        //    using (var zipStream = new FileStream(tempZipPath, FileMode.Create))
        //    {
        //        await zipFile.CopyToAsync(zipStream);
        //    }

        //    var extractDirPath = _fileStorageHelper.GenerateUniqueFileName(uploadDirPath, Path.GetFileNameWithoutExtension(zipFile.FileName));

        //    try
        //    {
        //        System.IO.Compression.ZipFile.ExtractToDirectory(tempZipPath, extractDirPath);
        //        foreach (var extractedFilePath in Directory.GetFiles(extractDirPath))
        //        {
        //            var extractedFile = new FileInfo(extractedFilePath);
        //            await _fileMetadataHelper.CreateAndSaveFileMetadataAsync(extractedFile.Name, extractedFile.FullName, extractedFile.Length);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        errors.Add(new FileErrorDTO
        //        {
        //            FileName = zipFile.FileName,
        //            ErrorMessage = $"Failed to extract ZIP file: {ex.Message}"
        //        });
        //    }
        //    finally
        //    {
        //        File.Delete(tempZipPath);
        //    }
        //}


        public async Task ProcessZipFileAsync(IFormFile zipFile, List<FileErrorDTO> errors)
        {
            var tempZipPath = zipFile.FileName;

            // Save the zip file to a local directory first
            using (var zipStream = new FileStream(tempZipPath, FileMode.Create))
            {
                await zipFile.CopyToAsync(zipStream);
            }

            // Generate a unique folder name for the extracted files
            var extractDirPath = tempZipPath + "ExtractedFolder";

            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(tempZipPath, extractDirPath);

                foreach (var extractedFilePath in Directory.GetFiles(extractDirPath))
                {
                    var extractedFile = new FileInfo(extractedFilePath);

                    IFormFile extractedFileForm = ConvertToIFormFile(extractedFile);

                    await _fileStorageHelper.SaveFileToS3Async(extractedFileForm, errors, _fileMetadataHelper);

                }
            }
            catch (Exception ex)
            {
                // Add an error to the errors list in case of failure
                errors.Add(new FileErrorDTO
                {
                    FileName = zipFile.FileName,
                    ErrorMessage = $"Failed to extract or upload ZIP file: {ex.Message}"
                });
            }
            finally
            {
                // Clean up the temporary zip files after processing
                File.Delete(tempZipPath);
                if (Directory.Exists(extractDirPath))
                {
                    Directory.Delete(extractDirPath, true);
                }
            }
        }

        private IFormFile ConvertToIFormFile(FileInfo fileInfo)
        {
            var memoryStream = new MemoryStream();

            // Read the file into the memory stream
            using (var fileStream = fileInfo.OpenRead())
            {
                fileStream.CopyTo(memoryStream);
            }

            // Set the position of the stream back to the beginning
            memoryStream.Position = 0;

            // Create an IFormFile object using the MemoryStream
            var formFile = new FormFile(memoryStream, 0, memoryStream.Length, fileInfo.Name, fileInfo.Name)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };

            return formFile;
        }
    }

}

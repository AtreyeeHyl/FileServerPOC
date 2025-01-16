using FileServer_POC.DTOs;
using FileServer_POC.Helpers;
using FileServer_POC.Models;
using Microsoft.VisualBasic.FileIO;
using Amazon.S3;
using Amazon.S3.Model;

namespace FileServer_POC.Services.Utilities
{
    public class FileStorageHelper
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        
        public FileStorageHelper(IAmazonS3 s3Client, IConfiguration configuration)
        {
            _s3Client = s3Client;
            _bucketName = configuration["AWS:BucketName"]; // Read from appsettings.json
        }


        //public string EnsureUploadDirectoryExists()
        //{
        //    var uploadDirPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        //    if (!Directory.Exists(uploadDirPath))
        //    {
        //        Directory.CreateDirectory(uploadDirPath);
        //    }
        //    return uploadDirPath;
        //}

        //public async Task<bool> IfS3BucketExists(string bucketName)
        //{
        //    var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
        //    return bucketExists;
        //}

        //public async Task SaveRegularFileAsync(IFormFile file, string uploadDirPath, List<FileErrorDTO> errors, FileMetadataHelper metadataHelper)
        //{
        //    try
        //    {
        //        var uploadFilePath = GenerateUniqueFileName(uploadDirPath, file.FileName);
        //        using (var stream = new FileStream(uploadFilePath, FileMode.Create))
        //        {
        //            await file.CopyToAsync(stream);
        //        }
        //        await metadataHelper.CreateAndSaveFileMetadataAsync(file.FileName, uploadFilePath, file.Length);
        //    }
        //    catch (Exception ex)
        //    {
        //        errors.Add(new FileErrorDTO
        //        {
        //            FileName = file.FileName,
        //            ErrorMessage = ex.Message
        //        });
        //    }
        //}

        public async Task SaveFileToS3Async(IFormFile file, List<FileErrorDTO> errors, FileMetadataHelper metadataHelper)
        {
            try
            {
                // Check if the bucket exists
                var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _bucketName);
                if (!bucketExists)
                {
                    throw new Exception($"The bucket '{_bucketName}' does not exist.");
                }

                // Generate a unique key for the file in S3
                var uniqueKey = $"{Guid.NewGuid()}_{file.FileName}";

                // Upload the file to S3
                using (var stream = file.OpenReadStream())
                {
                    var uploadRequest = new Amazon.S3.Model.PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = uniqueKey,
                        InputStream = stream,
                        ContentType = file.ContentType
                    };

                    var response = await _s3Client.PutObjectAsync(uploadRequest);
                }

                // Optionally save metadata for the uploaded file
                await metadataHelper.CreateAndSaveFileMetadataAsync(file.FileName, uniqueKey, file.Length);
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

        public async Task<bool> FileExistsInS3Async(string fileKey)
        {
            try
            {
                var request = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = fileKey
                };

                // Check if the object exists by attempting to retrieve its metadata
                var response = await _s3Client.GetObjectMetadataAsync(request);
                return response != null;
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
            {
                // File does not exist if "NoSuchKey" error occurs
                return false;
            }
        }
    }
}

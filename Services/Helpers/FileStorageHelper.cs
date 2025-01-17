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

        public async Task SaveFileToS3Async(IFormFile file, List<FileErrorDTO> errors, FileMetadataHelper metadataHelper, string? bucket_prefix)
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
                var uniqueKey = $"{bucket_prefix}{Guid.NewGuid()}_{file.FileName}";

                //Upload the file to S3
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





        public async Task UpdateRegularFileAsync(IFormFile file, List<FileErrorDTO> errors, FileMetadataHelper metadataHelper, FileMetadata metadata)
        {
            try
            {
                // Generate a unique file name to avoid conflicts in the S3 bucket
                var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";

                // Upload the file to S3 bucket
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    memoryStream.Position = 0; // Reset stream position before uploading

                    var putRequest = new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = uniqueFileName,
                        InputStream = memoryStream,
                        ContentType = file.ContentType,
                        AutoCloseStream = true
                    };

                    await _s3Client.PutObjectAsync(putRequest);
                }

                // Update the metadata with the S3 file path and other details
                metadata.FileName = file.FileName;
                metadata.FileType = Path.GetExtension(file.FileName);
                metadata.FilePath = uniqueFileName; // Use S3 key as the file path
                metadata.FileSize = file.Length;
                metadata.UploadDate = DateTime.UtcNow;

                // Update the metadata in the database
                await metadataHelper.UpdateFileMetadataAsync(metadata);
            }
            catch (Exception ex)
            {
                // Add error details to the errors list
                errors.Add(new FileErrorDTO
                {
                    FileName = file.FileName,
                    ErrorMessage = ex.Message
                });
            }
        }


        //public bool DeleteFile(string filePath, int metadataId, FileOperationDTO result)
        //{
        //    try
        //    {
        //        if (File.Exists(filePath))
        //        {
        //            File.Delete(filePath);
        //            return true;
        //        }
        //        else
        //        {
        //            result.Errors.Add(new FileErrorDTO
        //            {
        //                FileId = metadataId,
        //                ErrorMessage = "File not found on disk."
        //            });
        //            return false;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        result.Errors.Add(new FileErrorDTO
        //        {
        //            FileId = metadataId,
        //            ErrorMessage = $"Error deleting file: {ex.Message}"
        //        });
        //        return false;
        //    }
        //}


        public async Task<bool> DeleteFileAsync(string filePath, int metadataId, FileOperationDTO result)
        {
            try
            {
                // Check if the file exists in the S3 bucket
                if (await FileExistsInS3Async(filePath))
                {
                    // Delete the file from the S3 bucket
                    var deleteRequest = new DeleteObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = filePath
                    };
                    await _s3Client.DeleteObjectAsync(deleteRequest);
                    return true;
                }
                else
                {
                    result.Errors.Add(new FileErrorDTO
                    {
                        FileId = metadataId,
                        ErrorMessage = "File not found in S3 bucket."
                    });
                    return false;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(new FileErrorDTO
                {
                    FileId = metadataId,
                    ErrorMessage = $"Error deleting file from S3: {ex.Message}"
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

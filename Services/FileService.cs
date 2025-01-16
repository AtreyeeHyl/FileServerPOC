using FileServer_POC.DTOs;
using FileServer_POC.Repositories;
using FileServer_POC.Services.Utilities;
using FileServer_POC.Helpers;
using Microsoft.AspNetCore.Mvc;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Amazon.S3.Model;
using Azure.Core;
using Amazon.Runtime.Internal;

namespace FileServer_POC.Services
{
    public class FileService : IFileService
    {
        private readonly IFileRepository _fileRepository;
        private readonly IConfiguration _configuration;
        private readonly FileStorageHelper _fileStorageHelper;
        private readonly FileMetadataHelper _fileMetadataHelper;
        private readonly FileValidationHelper _fileValidationHelper;
        private readonly ZipProcessingHelper _zipProcessingHelper;
        private readonly IMemoryCache _memoryCache;
        private readonly string _cacheKey = "SampleData";
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public FileService(IFileRepository fileRepository, IMemoryCache memoryCache, IConfiguration configuration, IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
            _configuration = configuration;
            _fileRepository = fileRepository;
            _fileStorageHelper = new FileStorageHelper(_s3Client, _configuration);
            _fileMetadataHelper = new FileMetadataHelper(_fileRepository);
            _fileValidationHelper = new FileValidationHelper();
            _zipProcessingHelper = new ZipProcessingHelper(_fileStorageHelper, _fileMetadataHelper);
            _memoryCache = memoryCache;
            _bucketName = configuration["AWS:BucketName"];
        }

        public async Task<FileOperationDTO> UploadFilesAsync(List<IFormFile> files)
        {
            var errors = new List<FileErrorDTO>();
            //var uploadDirPath = _fileStorageHelper.EnsureUploadDirectoryExists();

            foreach (var file in files)
            {
                if (!_fileValidationHelper.IsValidFile(file, errors)) continue;

                try
                {
                    if (_fileValidationHelper.IsZipFile(file))
                    {
                        await _zipProcessingHelper.ProcessZipFileAsync(file, errors);
                    }
                    else
                    {
                        //await _fileStorageHelper.SaveRegularFileAsync(file, uploadDirPath, errors, _fileMetadataHelper);
                        await _fileStorageHelper.SaveFileToS3Async(file, errors, _fileMetadataHelper);
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



        //public async Task<List<GetFileDTO>> GetAllFilesAsync(string? filterOn = null, string? filterQuery = null)
        //{

        //    // Create a unique cache key based on filters
        //    string cacheKey = $"AllFiles_{filterOn ?? "None"}_{filterQuery ?? "None"}";

        //    // Check if the data is already cached
        //    if (!_memoryCache.TryGetValue(cacheKey, out List<GetFileDTO> cachedFiles))
        //    {
        //        var files = await _fileRepository.GetAllMetadataAsync(filterOn, filterQuery);


        //        // Convert to DTO
        //        cachedFiles = files.Select(file => new GetFileDTO
        //        {




        //            FileId = file.Id,
        //            FileName = file.FileName,
        //            FileType = file.FileType,
        //            FilePath = file.FilePath,
        //            FileSize = file.FileSize,
        //            UploadDate = file.UploadDate
        //        }).ToList();

        //        // Cache the result with appropriate expiration settings
        //        var cacheEntryOptions = new MemoryCacheEntryOptions()
        //            .SetSlidingExpiration(TimeSpan.FromSeconds(20)) // Extend expiration if accessed
        //            .SetAbsoluteExpiration(TimeSpan.FromSeconds(40)); // Max cache lifespan

        //        _memoryCache.Set(cacheKey, cachedFiles, cacheEntryOptions);
        //    }

        //    return cachedFiles;
        //}

        public async Task<List<GetFileDTO>> GetAllFilesAsync(string? filterOn = null, string? filterQuery = null)
        {
            // Create a unique cache key based on filters
            string cacheKey = $"AllFiles_{filterOn ?? "None"}_{filterQuery ?? "None"}";

            // Check if the data is already cached
            if (!_memoryCache.TryGetValue(cacheKey, out List<GetFileDTO> cachedFiles))
            {
                var files = await _fileRepository.GetAllMetadataAsync(filterOn, filterQuery);

                // Convert to DTO and generate pre-signed URLs
                cachedFiles = files.Select(file =>
                {
                    string presignedUrl = null;
                    try
                    {
                        // Generate the pre-signed URL for the file in S3
                        var urlRequest = new GetPreSignedUrlRequest
                        {
                            BucketName = _bucketName,
                            Key = file.FilePath,
                            Expires = DateTime.UtcNow.AddMinutes(10) // Set expiration time for the URL
                        };
                        presignedUrl = _s3Client.GetPreSignedURL(urlRequest);
                    }
                    catch (Exception ex)
                    {
                        // Log or handle exception if URL generation fails
                        Console.WriteLine($"Error generating pre-signed URL for file {file.FileName}: {ex.Message}");
                    }

                    return new GetFileDTO
                    {
                        FileId = file.Id,
                        FileName = file.FileName,
                        FileType = file.FileType,
                        FilePath = file.FilePath,
                        FileSize = file.FileSize,
                        UploadDate = file.UploadDate,
                        PreSignedUrl = presignedUrl
                    };
                }).ToList();

                // Cache the result with appropriate expiration settings
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromSeconds(20)) // Extend expiration if accessed
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(40)); // Max cache lifespan

                _memoryCache.Set(cacheKey, cachedFiles, cacheEntryOptions);
            }

            return cachedFiles;
        }

        public async Task<List<GetFileDTO>> GetAllFilesStreamAsync(string? filterOn = null, string? filterQuery = null)
        {
            var files = await _fileRepository.GetAllMetadataAsync(filterOn, filterQuery);

            if (files == null)
                return null;

            //Convert to DTO
            return files.Select(file => new GetFileDTO
            {
                //FileStream = new FileStream(file.FilePath, FileMode.Open, FileAccess.Read),
                FileId = file.Id,
                FileName = file.FileName,
                FileType = file.FileType,
                FilePath = file.FilePath,
                FileSize = file.FileSize,
                UploadDate = file.UploadDate
            }).ToList();
        }

        //public async Task<GetFileDTO> GetFileByIdAsync(int id)
        //{
        //    var metadata = await _fileRepository.GetMetadataByIdAsync(id);

        //    if (metadata == null || !(await _fileStorageHelper.FileExistsAsync(metadata.FilePath)))
        //        return null;

        //    var fileStream = new FileStream(metadata.FilePath, FileMode.Open, FileAccess.Read);
        //    return new GetFileDTO
        //    {
        //        FileStream = fileStream,
        //        FileName = metadata.FileName
        //    };
        //}

        public async Task<GetFileDTO> GetFileByIdAsync(int id)
        {
            var metadata = await _fileRepository.GetMetadataByIdAsync(id);

            if (metadata == null)
                return null;

            // Check if the file exists in S3
            if (!(await _fileStorageHelper.FileExistsInS3Async(metadata.FilePath)))
                return null;

           ////Generate the pre - signed URL for the file in S3

           //var urlRequest = new GetPreSignedUrlRequest
           //{
           //    BucketName = _bucketName,
           //    Key = metadata.FilePath,
           //    Expires = DateTime.UtcNow.AddMinutes(10) // Set the expiration time for the URL
           //};
           //var presignedUrl = _s3Client.GetPreSignedURL(urlRequest);

            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = metadata.FilePath
            };

            

            // Fetch the file from S3
            using (var response = await _s3Client.GetObjectAsync(request))
            {
                // Stream the file content to the response
                var memoryStream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0; // Reset stream position for reading

                // Return the file as a downloadable response
                return new GetFileDTO
                {
                    FileName = metadata.FileName,
                    FileType = metadata.FileType,
                    MemoryStream = memoryStream
                };
                //return File(memoryStream, response.Headers["Content-Type"], fileName);
            }

            // Return the pre-signed URL
            
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

                //var uploadDirPath = _fileStorageHelper.EnsureUploadDirectoryExists();
                var errors = new List<FileErrorDTO>();

                //await _fileStorageHelper.UpdateRegularFileAsync(file, uploadDirPath, errors, _fileMetadataHelper, metadata);
              
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

            if (metadata == null || !(await _fileStorageHelper.FileExistsInS3Async(metadata.FilePath)))
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
            // Define a unique cache key based on the date range
            var cacheKey = $"FilesByDateRange_{startDate?.ToString("yyyyMMdd") ?? "Start"}_{endDate?.ToString("yyyyMMdd") ?? "End"}";

            if (_memoryCache.TryGetValue(cacheKey, out List<GetFileDTO> cachedFiles))
            {
                return cachedFiles;
            }

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

            // Store the result in the cache with a sliding expiration of 30 seconds
            _memoryCache.Set(cacheKey, filesDTO, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
                SlidingExpiration = TimeSpan.FromSeconds(15)
            });

            return filesDTO;
        }
    }
}

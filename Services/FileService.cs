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
using System.Reflection.Metadata;

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

        public async Task<List<GetFileDTO>> GetAllFilesByDateRangeAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            // Define a unique cache key based on the date range
            var cacheKey = $"FilesByDateRange_{startDate?.ToString("yyyyMMdd") ?? "Start"}_{endDate?.ToString("yyyyMMdd") ?? "End"}";

            if (_memoryCache.TryGetValue(cacheKey, out List<GetFileDTO> cachedFiles))
            {
                return cachedFiles;
            }

            var filesMetadata = await _fileRepository.GetAllMetadataByDateAsync(startDate, endDate);

            // Convert to DTO and generate pre-signed URLs
            var filesDTO = filesMetadata.Select(file =>
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

            // Store the result in the cache with a sliding expiration of 30 seconds
            _memoryCache.Set(cacheKey, filesDTO, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
                SlidingExpiration = TimeSpan.FromSeconds(15)
            });

            return filesDTO;
        }

        public async Task<GetFileDTO> GetFileByIdAsync(int id)
        {
            var metadata = await _fileRepository.GetMetadataByIdAsync(id);

            if ((metadata == null) || (!(await _fileStorageHelper.FileExistsInS3Async(metadata.FilePath))))
                return null;

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
            }
        }


        public async Task<List<GetFileDTO>> GetAllFilesStreamAsync(string? filterOn = null, string? filterQuery = null)
        {
            var files = await _fileRepository.GetAllMetadataAsync(filterOn, filterQuery);

            if (files == null || !files.Any())
                return null;

            var fileDTOs = new List<GetFileDTO>();

            foreach (var file in files)
            {
                try
                {
                    var request = new GetObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = file.FilePath
                    };

                    // Fetch the file from S3
                    using (var response = await _s3Client.GetObjectAsync(request))
                    {
                        var memoryStream = new MemoryStream();
                        await response.ResponseStream.CopyToAsync(memoryStream);
                        memoryStream.Position = 0; // Reset stream position for reading

                        fileDTOs.Add(new GetFileDTO
                        {
                            FileId = file.Id,
                            FileName = file.FileName,
                            FileType = file.FileType,
                            MemoryStream = memoryStream
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Log or handle errors fetching files from S3
                    Console.WriteLine($"Error fetching file from S3: {file.FilePath}, Exception: {ex.Message}");
                }
            }

            return fileDTOs;
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

                var errors = new List<FileErrorDTO>();

                await _fileStorageHelper.UpdateRegularFileAsync(file, errors, _fileMetadataHelper, metadata);
              
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
                newFileName += Path.GetExtension(metadata.FileName);
                var uniqueFileName = $"{Guid.NewGuid()}_{newFileName}";
                // Copy the file in S3 to the new name (renaming)
                var copyRequest = new CopyObjectRequest
                {
                    SourceBucket = _bucketName,
                    SourceKey = metadata.FilePath,
                    DestinationBucket = _bucketName,
                    DestinationKey = uniqueFileName
                };
                await _s3Client.CopyObjectAsync(copyRequest);

                // Delete the old file
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = metadata.FilePath
                };
                await _s3Client.DeleteObjectAsync(deleteRequest);

                // Update the metadata with the new file name
                metadata.FileName = newFileName;
                metadata.FileType = Path.GetExtension(metadata.FileName);
                metadata.FilePath = uniqueFileName; // Use S3 key as the file path

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


        public async Task<FileOperationDTO> DeleteFilesAndMetadataAsync(int[] ids)
        {
            var filesToDelete = await _fileRepository.GetMetadataByIdsAsync(ids);
            var result = new FileOperationDTO
            {
                Success = true,
                Message = "All files deleted successfully."
            };

            if (filesToDelete==null || filesToDelete.Count == 0)
            {
                result.Success = false;
                result.Message = "Could not find file metadata with given ids";
            }

            foreach (var metadata in filesToDelete)
            {
                var fileDeleted = await _fileStorageHelper.DeleteFileAsync(metadata.FilePath, metadata.Id, result);
                if (fileDeleted)
                {
                    await _fileMetadataHelper.DeleteMetadataAsync(metadata.Id, result);
                }
                else
                {
                    break;
                }
            }

            if (result.Errors.Count > 0 || (ids.Length - filesToDelete.Count) > 0)
{
    // Add errors for IDs in the provided array that are not in filesToDelete
    var validIds = filesToDelete.Select(f => f.Id).ToHashSet();
    var invalidIds = ids.Where(id => !validIds.Contains(id));

    foreach (var invalidId in invalidIds)
    {
        result.Errors.Add(new FileErrorDTO
        {
            FileId = invalidId,
            ErrorMessage = "File metadata not found or invalid ID provided."
        });
    }

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
                var fileDeleted = await _fileStorageHelper.DeleteFileAsync(metadata.FilePath, metadata.Id, result);
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

using Amazon.S3;
using FileServer_POC.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;
using System.IO.Compression;
using System.Text.Json;
using FileServer_POC.DTOs;
using Amazon.S3.Model;
using Azure;
using System.IO;


namespace FileServer_POC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly IAmazonS3 _s3Client;

        public FileController(IFileService fileService, IAmazonS3 s3Client)
        {
            _fileService = fileService;
            _s3Client = s3Client;
        }





        [HttpPost]
        public async Task<IActionResult> UploadFileAsync(IFormFile file, string bucketName, string? prefix)
        {
            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");
            var request = new PutObjectRequest()
            {
                BucketName = bucketName,
                Key = string.IsNullOrEmpty(prefix) ? file.FileName : $"{prefix?.TrimEnd('/')}/{file.FileName}",
                InputStream = file.OpenReadStream()
            };
            request.Metadata.Add("Content-Type", file.ContentType);
            await _s3Client.PutObjectAsync(request);
            return Ok($"File {prefix}/{file.FileName} uploaded to S3 successfully!");
        }

        [HttpGet]
        public async Task<IActionResult> GetAllFilesAsync(string bucketName, string? prefix)
        {
            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");
            var request = new ListObjectsV2Request()
            {
                BucketName = bucketName,
                Prefix = prefix
            };
            var result = await _s3Client.ListObjectsV2Async(request);
            var s3Objects = result.S3Objects.Select(s =>
            {
                var urlRequest = new GetPreSignedUrlRequest()
                {
                    BucketName = bucketName,
                    Key = s.Key,
                    Expires = DateTime.UtcNow.AddMinutes(1)
                };
                return new S3ObjectDTO()
                {
                    Name = s.Key.ToString(),
                    PresignedUrl = _s3Client.GetPreSignedURL(urlRequest),
                };
            });
            return Ok(s3Objects);
        }






        //Upload Files Single or Multiple
        [HttpPost]
        [Route("UploadFiles")]
        public async Task<ActionResult> UploadFiles([FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return BadRequest("No files uploaded.");

            var result = await _fileService.UploadFilesAsync(files);

            if (!result.Success)
            {
                return StatusCode(StatusCodes.Status207MultiStatus, new
                {
                    Message = "Partial success in file upload.",
                    Errors = result.Errors
                });
            }

            return Ok(new { Message = "All files uploaded successfully." });
        }


        //Update File
        [HttpPut("UpdateFile/{id}")]
        public async Task<ActionResult> UpdateFiles(int id,[FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return BadRequest("No files uploaded.");

            var result = await _fileService.UpdateFileByIdAsync(id, files[0]);

           if (!result.Success)
            {
                return StatusCode(StatusCodes.Status207MultiStatus, new
                {
                    Message = "Could not update file.",
                    Errors = result.Message
                });
            }

            return Ok(new { Message = "All files uploaded successfully." });
        }


        // Update File Name and Metadata
        [HttpPut("RenameFile/{id}")]
        public async Task<ActionResult> UpdateFileNameAndMetadata(int id, [FromForm] string newFileName)
        {
            if (string.IsNullOrEmpty(newFileName))
            {
                return BadRequest("New file name is required.");
            }

            var result = await _fileService.UpdateFileNameAndMetadataAsync(id, newFileName);

            if (!result.Success)
            {
                return StatusCode(StatusCodes.Status207MultiStatus, new
                {
                    Message = "Could not update file name.",
                    Errors = result.Message
                });
            }

            return Ok(new { Message = "File name updated successfully." });
        }

        //Get Multiple Files Details Using Filters
        [HttpGet]
        [Route("GetFilesDetails")]
        public async Task<IActionResult> GetAllFilesDetails([FromQuery] string? filterOn, [FromQuery] string? filterQuery)
        {
            var files = await _fileService.GetAllFilesAsync(filterOn,filterQuery);

            if (files == null || files.Count == 0)
                return NotFound("No files found.");

            return Ok(files);
        }

        //Get files using Date Range
        [HttpGet]
        [Route("GetFilesByDate")]
        public async Task<IActionResult> GetAllFilesByDateRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            
            var files = await _fileService.GetAllFilesByDateRangeAsync(startDate, endDate);

            if (files == null || files.Count == 0)
            {
                return NotFound(new { Message = "No files found within the specified date range." });
            }

            return Ok(new { Files = files });
        }


        //Download Single file using ID
        [HttpGet("GetFileByID/{id}")]
        public async Task<IActionResult> GetFileById(int id)
        {
            var result = await _fileService.GetFileByIdAsync(id);

            if (result == null)
                return NotFound(new { Message = "File not found." });

            return File(result.MemoryStream, "application/octet-stream", result.FileName);

        }


        //Download Multiple Files Using Filters
        //[HttpGet]
        //[Route("GetFilesDownload")]
        //public async Task<IActionResult> GetAllFiles([FromQuery] string? filterOn, [FromQuery] string? filterQuery)
        //{
        //    var files = await _fileService.GetAllFilesStreamAsync(filterOn, filterQuery);

        //    if (files == null || files.Count == 0)
        //        return NotFound("No files found.");

        //    // Create a memory stream to hold the zip file
        //    var memoryStream = new MemoryStream();

        //    // Create the zip archive
        //    using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        //    {
        //        foreach (var file in files)
        //        {
        //            var zipEntry = archive.CreateEntry(file.FileName);
        //            using (var zipStream = zipEntry.Open())
        //            {
        //                // Copy the file's stream into the zip entry stream
        //                await file.FileStream.CopyToAsync(zipStream);
        //            }
        //        }
        //    }

        //    // Reset memory stream position before sending
        //    memoryStream.Position = 0;

        //    // Prepare file metadata
        //    var fileMetadata = files.Select(file => new
        //    {
        //        file.FileName
        //    }).ToList();

        //    // Serialize the metadata to JSON
        //    var metadataJson = JsonConvert.SerializeObject(fileMetadata);

        //    // Add metadata to custom header
        //    HttpContext.Response.Headers.Append("X-File-Metadata", metadataJson);

        //    // Return the zip file as a downloadable file
        //    return File(memoryStream, "application/zip", "AllFiles.zip");
        //}


        //Delete Single or Multiple files using ID
        [HttpDelete]
        [Route("DeleteFiles")]
        public async Task<IActionResult> DeleteFiles([FromBody] int[] ids)
        {
            var result = await _fileService.DeleteFilesAndMetadataAsync(ids);

            var successfullyDeleted = ids.Except(result.Errors.Select(e => e.FileId))
            .ToList();

            return StatusCode(StatusCodes.Status207MultiStatus, new
            {
                Message = result.Errors.Any() ? "Partial success in file deletion." : "All files deleted successfully.",
                SuccessfullyDeleted = successfullyDeleted,
                FailedToDelete = result.Errors.Select(e => new
                {
                    FileId = e.FileId,
                    FileName = e.FileName,
                    ErrorMessage = e.ErrorMessage
                })
            });

        }


    }
}

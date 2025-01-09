using FileServer_POC.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileServer_POC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;

        public FileController(IFileService fileService)
        {
            _fileService = fileService;
        }

        [HttpPost]
        [Route("upload")]
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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetFileById(int id)
        {
            var result = await _fileService.GetFileByIdAsync(id);

            if (result == null)
                return NotFound(new { Message = "File not found." });

            return File(result.FileStream, "application/octet-stream", result.FileName);
        }

        [HttpDelete]
        [Route("delete")]
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

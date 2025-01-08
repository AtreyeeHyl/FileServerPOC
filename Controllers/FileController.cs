using FileServer_POC.Entities;
using FileServer_POC.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;

namespace FileServer_POC.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FileController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("upload")]
        public async Task<ActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var uploadDirPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadDirPath))
                Directory.CreateDirectory(uploadDirPath);

            var uploadFilePath = Path.Combine(uploadDirPath, file.FileName);

            using FileStream stream = new FileStream(uploadFilePath, FileMode.Create);
            await file.CopyToAsync(stream);
            
            var metadata = new FileMetadata
            {
                FileName = file.FileName,
                FilePath = uploadFilePath,
                FileSize = file.Length,
                UploadDate = DateTime.UtcNow
            };

            _context.FileMetadata.Add(metadata);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "File uploaded successfully", metadata });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetFileById(int id)
        {
            var metadata = await _context.FileMetadata.FindAsync(id);

            if (metadata == null)
                return NotFound(new { Message = "File not found." });

            if (!System.IO.File.Exists(metadata.FilePath))
                return NotFound(new { Message = "File not found on the server." });

            FileStream fileStream = new FileStream(metadata.FilePath, FileMode.Open, FileAccess.Read);
            return new FileStreamResult(fileStream, "application/octet-stream")
            {
                FileDownloadName = metadata.FileName
            };

        }

    }

}

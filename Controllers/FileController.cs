using FileServer_POC.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

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
        public async Task<ActionResult> UploadFile(IFormFile file)
        {
            var result = await _fileService.UploadFileAsync(file);
            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetFileById(int id)
        {
            var result = await _fileService.GetFileByIdAsync(id);

            if (result == null)
                return NotFound(new { Message = "File not found." });

            return File(result.FileStream, "application/octet-stream", result.FileName);
        }

    }
}

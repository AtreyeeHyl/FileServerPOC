using System.Collections.Generic;

namespace FileServer_POC.DTOs
{
    public class DeleteFileResult
    {
        public List<FileError> Errors { get; set; } = new List<FileError>();
    }
}

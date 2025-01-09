namespace FileServer_POC.DTOs
{
    public class FileOperationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<FileError> Errors { get; set; } = new List<FileError>();
    }

}

namespace FileServer_POC.DTOs
{
    public class FileError
    {
        public int? FileId { get; set; }
        public string FileName { get; set; }
        public string ErrorMessage { get; set; }
    }
}

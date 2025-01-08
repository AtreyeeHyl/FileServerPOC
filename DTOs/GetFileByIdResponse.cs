namespace FileServer_POC.DTOs
{
    public class GetFileByIdResponse
    {
        public FileStream FileStream { get; set; }
        public string FileName { get; set; }
    }
}

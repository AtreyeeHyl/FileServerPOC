namespace FileServer_POC.Models
{
    public class BufferedFile
    {
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public MemoryStream Content { get; set; }
    }
}

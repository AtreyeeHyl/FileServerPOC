using System.Text.Json.Serialization;

namespace FileServer_POC.DTOs
{
    public class GetFileDTO
    {
        public int FileId { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime UploadDate { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public FileStream FileStream { get; set; }

    }
}

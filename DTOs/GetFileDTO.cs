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
        public MemoryStream MemoryStream { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string PreSignedUrl { get; set; }

    }
}

using System.Text.Json.Serialization;

namespace FileServer_POC.DTOs
{
    public class FileOperationDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<FileErrorDTO> Errors { get; set; } = new List<FileErrorDTO>();
    }

}

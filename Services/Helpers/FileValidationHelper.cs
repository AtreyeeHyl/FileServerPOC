using FileServer_POC.DTOs;

namespace FileServer_POC.Services.Utilities
{
    public class FileValidationHelper
    {
        public bool IsValidFile(IFormFile file, List<FileErrorDTO> errors)
        {
            if (file.Length == 0)
            {
                errors.Add(new FileErrorDTO
                {
                    FileName = file.FileName,
                    ErrorMessage = "File is empty."
                });
                return false;
            }
            return true;
        }

        public bool IsZipFile(IFormFile file)
        {
            return Path.GetExtension(file.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase);
        }
    }

}

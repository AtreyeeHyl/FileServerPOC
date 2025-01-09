using FileServer_POC.DTOs;
using FileServer_POC.Repositories;
using FileServer_POC.Services.Utilities;
using FileServer_POC.Helpers;

namespace FileServer_POC.Services
{
    public class FileService : IFileService
    {
        private readonly IFileRepository _fileRepository;
        private readonly FileStorageHelper _fileStorageHelper;
        private readonly FileMetadataHelper _fileMetadataHelper;
        private readonly FileValidationHelper _fileValidationHelper;
        private readonly ZipProcessingHelper _zipProcessingHelper;

        public FileService(IFileRepository fileRepository)
        {
            _fileRepository = fileRepository;
            _fileStorageHelper = new FileStorageHelper();
            _fileMetadataHelper = new FileMetadataHelper(_fileRepository);
            _fileValidationHelper = new FileValidationHelper();
            _zipProcessingHelper = new ZipProcessingHelper(_fileStorageHelper, _fileMetadataHelper);
        }

        public async Task<FileOperationResponse> UploadFilesAsync(List<IFormFile> files)
        {
            var errors = new List<FileError>();
            var uploadDirPath = _fileStorageHelper.EnsureUploadDirectoryExists();

            foreach (var file in files)
            {
                if (!_fileValidationHelper.IsValidFile(file, errors)) continue;

                try
                {
                    if (_fileValidationHelper.IsZipFile(file))
                    {
                        await _zipProcessingHelper.ProcessZipFileAsync(file, uploadDirPath, errors);
                    }
                    else
                    {
                        await _fileStorageHelper.SaveRegularFileAsync(file, uploadDirPath, errors, _fileMetadataHelper);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new FileError
                    {
                        FileName = file.FileName,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return new FileOperationResponse
            {
                Success = errors.Count == 0,
                Message = errors.Count == 0 ? "All files uploaded successfully." : "Partial success in file upload.",
                Errors = errors
            };
        }

        public async Task<List<GetAllFilesResponse>> GetAllFilesAsync()
        {
            var files = await _fileRepository.GetAllMetadataAsync();

            return files.Select(file => new GetAllFilesResponse
            {
                FileId = file.Id,
                FileName = file.FileName,
                FilePath = file.FilePath,
                FileSize = file.FileSize,
                UploadDate = file.UploadDate
            }).ToList();
        }

        public async Task<GetFileByIdResponse> GetFileByIdAsync(int id)
        {
            var metadata = await _fileRepository.GetMetadataByIdAsync(id);

            if (metadata == null || !_fileStorageHelper.FileExists(metadata.FilePath))
                return null;

            var fileStream = new FileStream(metadata.FilePath, FileMode.Open, FileAccess.Read);
            return new GetFileByIdResponse
            {
                FileStream = fileStream,
                FileName = metadata.FileName
            };
        }

        public async Task<FileOperationResponse> DeleteFilesAndMetadataAsync(int[] ids)
        {
            var filesToDelete = await _fileRepository.GetMetadataByIdsAsync(ids);
            var result = new FileOperationResponse
            {
                Success = true,
                Message = "All files deleted successfully."
            };

            foreach (var metadata in filesToDelete)
            {
                var fileDeleted = _fileStorageHelper.DeleteFile(metadata.FilePath, metadata.Id, result);
                if (fileDeleted)
                {
                    await _fileMetadataHelper.DeleteMetadataAsync(metadata.Id, result);
                }
            }

            if (result.Errors.Count > 0)
            {
                result.Success = false;
                result.Message = "Partial success in file deletion.";
            }

            return result;
        }

    }
}

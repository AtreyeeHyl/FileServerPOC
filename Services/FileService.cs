using FileServer_POC.DTOs;
using FileServer_POC.Models;
using FileServer_POC.Repositories;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FileServer_POC.Services
{
    public class FileService : IFileService
    {
        private readonly IFileRepository _fileRepository;

        public FileService(IFileRepository fileRepository)
        {
            _fileRepository = fileRepository;
        }

        public async Task<object> UploadFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file uploaded.");

            var uploadDirPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadDirPath))
                Directory.CreateDirectory(uploadDirPath);

            string uniqueFileName = await GenerateUniqueFileNameAsync(file.FileName);
            var uploadFilePath = Path.Combine(uploadDirPath, uniqueFileName);

            using FileStream stream = new FileStream(uploadFilePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var metadata = new FileMetadata
            {
                FileName = uniqueFileName,
                FilePath = uploadFilePath,
                FileSize = file.Length,
                UploadDate = DateTime.UtcNow
            };

            await _fileRepository.AddMetadataAsync(metadata);

            return new { Message = "File uploaded successfully", metadata };
        }

        public async Task<string> GenerateUniqueFileNameAsync(string originalFileName)
        {
            string originalName = Path.GetFileNameWithoutExtension(originalFileName);
            string extension = Path.GetExtension(originalFileName);
            string uniqueFileName = originalFileName;
            int counter = 1;

            while (await _fileRepository.FileNameExistsAsync(uniqueFileName))
            {
                uniqueFileName = $"{originalName}({counter}){extension}";
                counter++;
            }

            return uniqueFileName;
        }

        public async Task<GetFileByIdResponse> GetFileByIdAsync(int id)
        {
            var metadata = await _fileRepository.GetMetadataByIdAsync(id);

            if (metadata == null || !File.Exists(metadata.FilePath))
                return null;

            var fileStream = new FileStream(metadata.FilePath, FileMode.Open, FileAccess.Read);
            return new GetFileByIdResponse
            {
                FileStream = fileStream,
                FileName = metadata.FileName
            };
        }

    }
}

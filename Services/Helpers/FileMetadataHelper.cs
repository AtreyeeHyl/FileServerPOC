﻿using FileServer_POC.DTOs;
using FileServer_POC.Models;
using FileServer_POC.Repositories;

namespace FileServer_POC.Helpers
{
    public class FileMetadataHelper
    {
        private readonly IFileRepository _fileRepository;

        public FileMetadataHelper(IFileRepository fileRepository)
        {
            _fileRepository = fileRepository;
        }

        public async Task CreateAndSaveFileMetadataAsync(string fileName, string filePath, long fileSize)
        {
            var metadata = new FileMetadata
            {
                FileName = fileName,
                FilePath = filePath,
                FileSize = fileSize,
                UploadDate = DateTime.UtcNow
            };

            await _fileRepository.SaveMetadataAsync(metadata);
        }

        public async Task DeleteMetadataAsync(int metadataId, FileOperationResponse result)
        {
            try
            {
                await _fileRepository.DeleteMetadataAsync(metadataId);
            }
            catch (Exception ex)
            {
                result.Errors.Add(new FileError
                {
                    FileId = metadataId,
                    ErrorMessage = $"Error deleting metadata: {ex.Message}"
                });
            }
        }
    }

}
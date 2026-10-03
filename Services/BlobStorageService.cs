using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RESK.WIL.Services
{
    public interface IBlobStorageService
    {
        Task<string> UploadAsync(string fileName, Stream content, string contentType,
            CancellationToken cancellationToken = default);
    }

    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobContainerClient _container;

        public BlobStorageService(string connectionString, string containerName)
        {
            _container = new BlobContainerClient(connectionString, containerName);
            _container.CreateIfNotExists(PublicAccessType.Blob);
        }

        public async Task<string> UploadAsync(string fileName, Stream content, string contentType,
            CancellationToken cancellationToken = default)
        {
            var blobName = $"{Guid.NewGuid():N}-{Path.GetFileName(fileName)}";
            var blobClient = _container.GetBlobClient(blobName);
            await blobClient.UploadAsync(content,
                new BlobHttpHeaders { ContentType = contentType },
                cancellationToken: cancellationToken);
            return blobClient.Uri.ToString();
        }
    }
}

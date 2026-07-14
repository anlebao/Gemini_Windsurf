using Microsoft.AspNetCore.Http;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Image storage abstraction — upload/delete product images.
    /// G8: Cloudinary implementation. Decoupled from Product API (separate upload endpoint).
    /// </summary>
    public interface IImageStorageService
    {
        /// <summary>Upload an image file (IFormFile — from HTTP multipart endpoint). Returns the public URL, or null on failure.</summary>
        Task<string?> UploadAsync(IFormFile file, string folder, CancellationToken cancellationToken = default);

        /// <summary>Upload an image stream (from Blazor Server IBrowserFile). Returns the public URL, or null on failure.</summary>
        Task<string?> UploadAsync(Stream stream, string fileName, string folder, CancellationToken cancellationToken = default);

        /// <summary>Delete an image by public ID. Returns true on success.</summary>
        Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default);
    }
}

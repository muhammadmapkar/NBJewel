// Role: The swappable data boundary for try-on jewelry. FileJewelryRepository (JSON metadata
// plus images on disk) implements it today. A SQL or blob storage version can replace it later
// without changing the controller, the same way IItemRepository works for the ERP catalog.
// Callers pass bytes that UploadValidator already checked; the repository picks the file name.

namespace NbJewel.Api.Jewelry;

public interface IJewelryRepository
{
    Task<IReadOnlyList<JewelryRecord>> ListAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<JewelryRecord?> GetAsync(string id, CancellationToken cancellationToken);
    // extension is ".png" or ".jpg", decided from the file content by UploadValidator.
    Task<JewelryRecord> AddAsync(string name, string type, byte[] content, string extension, CancellationToken cancellationToken);
    // Returns null when the id is unknown.
    Task<JewelryRecord?> SetActiveAsync(string id, bool active, CancellationToken cancellationToken);
    // Returns false when the id is unknown. Removes the metadata and the image file.
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}

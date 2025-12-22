using DocControl.Infrastructure.Data;
using DocControl.Core.Models;

namespace DocControl.Infrastructure.Services;

public sealed record DocumentCreationResult(long DocumentId, int Number, string FileName);

public sealed class DocumentService
{
    private readonly NumberAllocator allocator;
    private readonly DocumentRepository documentRepo;
    private readonly AuditRepository auditRepo;
    private readonly CodeGenerator codeGenerator;

    public DocumentService(NumberAllocator allocator, DocumentRepository documentRepo, AuditRepository auditRepo, CodeGenerator codeGenerator)
    {
        this.allocator = allocator;
        this.documentRepo = documentRepo;
        this.auditRepo = auditRepo;
        this.codeGenerator = codeGenerator;
    }

    public async Task<DocumentCreationResult> CreateAsync(CodeSeriesKey key, string freeText, long createdByUserId, string? originalQuery, string? extension, CancellationToken cancellationToken = default)
    {
        var allocated = await allocator.AllocateAsync(key, cancellationToken).ConfigureAwait(false);
        var fileName = codeGenerator.BuildFileName(key, allocated.Number, freeText, extension);
        var createdAt = DateTime.UtcNow;

        var docId = await documentRepo.InsertAsync(allocated, key, freeText, fileName, createdByUserId, createdAt, originalQuery, cancellationToken).ConfigureAwait(false);

        await auditRepo.InsertAsync(key.ProjectId, "DocumentCreated", fileName, createdByUserId, createdAt, docId, cancellationToken).ConfigureAwait(false);

        return new DocumentCreationResult(docId, allocated.Number, fileName);
    }
}

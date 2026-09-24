using Microsoft.AspNetCore.Identity;
using Pgvector;

namespace BuildYourOwnAI.Api.Infrastructure.Data;

public sealed class AppUser : IdentityUser;

/// <summary>The user's own AI: a name, instructions and the documents it answers from.</summary>
public sealed class Assistant
{
    public const int NameMaxLength = 100;
    public const int InstructionsMaxLength = 4000;

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string OwnerId { get; init; }
    public required string Name { get; set; }
    public string? Instructions { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<Document> Documents { get; init; } = [];
}

public sealed class Document
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AssistantId { get; init; }
    public Assistant Assistant { get; init; } = null!;
    public required string FileName { get; init; }
    public long SizeBytes { get; init; }
    public required string ContentSha256 { get; init; }
    public int ChunkCount { get; init; }
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<Chunk> Chunks { get; init; } = [];
}

public sealed class Chunk
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid DocumentId { get; init; }
    public Document Document { get; init; } = null!;
    public int Index { get; init; }
    public required string Content { get; init; }
    public required Vector Embedding { get; init; }
}

using Microsoft.AspNetCore.Identity;
using Pgvector;

namespace BuildYourOwnAI.Api.Infrastructure.Data;

public sealed class AppUser : IdentityUser
{
    public const int FullNameMaxLength = 100;

    /// <summary>Asked at registration; null for accounts created before it was.</summary>
    public string? FullName { get; set; }
}

/// <summary>The user's container: the documents its assistants share, and the assistants themselves.</summary>
public sealed class Organization
{
    public const int NameMaxLength = 100;

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string OwnerId { get; init; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<Assistant> Assistants { get; init; } = [];
    public List<Document> Documents { get; init; } = [];
}

/// <summary>The user's own AI: a persona (name, instructions, when to use it) over its organization's documents.</summary>
public sealed class Assistant
{
    public const int NameMaxLength = 100;
    public const int InstructionsMaxLength = 4000;
    public const int RoutingDescriptionMaxLength = 500;

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid OrganizationId { get; init; }
    public Organization Organization { get; init; } = null!;
    public required string Name { get; set; }
    public string? Instructions { get; set; }
    public string? RoutingDescription { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class Document
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid OrganizationId { get; init; }
    public Organization Organization { get; init; } = null!;
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

public enum GapStatus
{
    Open,
    Answered,
    Dismissed,
}

/// <summary>A question no assistant could answer. Only <see cref="GapStatus.Open"/> transitions.</summary>
public sealed class Gap
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string OwnerId { get; init; }
    public Guid? OrganizationId { get; init; }
    public Organization? Organization { get; init; }
    public Guid? AssistantId { get; init; }
    public Assistant? Assistant { get; init; }
    public required string Question { get; init; }
    public required string NormalizedQuestion { get; init; }
    public int AskCount { get; init; }
    public DateTimeOffset FirstAskedAt { get; init; }
    public DateTimeOffset LastAskedAt { get; init; }
    public GapStatus Status { get; set; }
    public Guid? DocumentId { get; set; }
}

/// <summary>A study round in an organization: questions generated from chunks of the chosen documents.</summary>
public sealed class StudySession
{
    public static readonly int[] AllowedQuestionCounts = [5, 10, 20];

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid OrganizationId { get; init; }
    public Organization Organization { get; init; } = null!;
    public int QuestionCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<StudyQuestion> Questions { get; init; } = [];
}

/// <summary>
/// A multiple-choice question generated from one chunk. The right option never leaves the server before the
/// question is answered (study-mode door 1); it is answered once.
/// </summary>
public sealed class StudyQuestion
{
    public const int OptionCount = 4;

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid SessionId { get; init; }
    public StudySession Session { get; init; } = null!;
    public int Position { get; init; }
    public Guid DocumentId { get; init; }
    public Document Document { get; init; } = null!;
    public int ChunkIndex { get; init; }
    public required string Prompt { get; init; }
    public required List<string> Options { get; init; }
    public short CorrectOption { get; init; }
    public required string Explanation { get; init; }
    public short? ChosenOption { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
}

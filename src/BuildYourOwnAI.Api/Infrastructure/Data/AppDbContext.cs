using BuildYourOwnAI.Api.Infrastructure.Ai;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<Assistant> Assistants => Set<Assistant>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Chunk> Chunks => Set<Chunk>();

    // Read per query by the global filters below; EF parameterizes it per DbContext instance.
    private string? CurrentUserId => currentUser.Id;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("vector");

        builder.Entity<Assistant>(e =>
        {
            e.Property(a => a.Name).HasMaxLength(Assistant.NameMaxLength);
            e.Property(a => a.Instructions).HasMaxLength(Assistant.InstructionsMaxLength);
            e.HasOne<AppUser>().WithMany().HasForeignKey(a => a.OwnerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.OwnerId, a.CreatedAt });
            // Door 5: an assistant is only ever visible to its owner.
            e.HasQueryFilter(a => a.OwnerId == CurrentUserId);
        });

        builder.Entity<Document>(e =>
        {
            e.Property(d => d.FileName).HasMaxLength(255);
            e.Property(d => d.ContentSha256).HasMaxLength(64);
            e.HasOne(d => d.Assistant).WithMany(a => a.Documents).HasForeignKey(d => d.AssistantId).OnDelete(DeleteBehavior.Cascade);
            // Door 3: the same content cannot be attached twice to the same assistant.
            e.HasIndex(d => new { d.AssistantId, d.ContentSha256 }).IsUnique();
            e.HasQueryFilter(d => d.Assistant.OwnerId == CurrentUserId);
        });

        builder.Entity<Chunk>(e =>
        {
            e.HasOne(c => c.Document).WithMany(d => d.Chunks).HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.Property(c => c.Embedding).HasColumnType($"vector({AiOptions.EmbeddingDimensions})");
            // Door 2: approximate nearest neighbour by cosine distance.
            e.HasIndex(c => c.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
            e.HasQueryFilter(c => c.Document.Assistant.OwnerId == CurrentUserId);
        });
    }
}

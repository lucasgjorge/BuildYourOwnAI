using BuildYourOwnAI.Api.Infrastructure.Ai;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BuildYourOwnAI.Api.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Assistant> Assistants => Set<Assistant>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Chunk> Chunks => Set<Chunk>();
    public DbSet<Gap> Gaps => Set<Gap>();

    // Read per query by the global filters below; EF parameterizes it per DbContext instance.
    private string? CurrentUserId => currentUser.Id;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("vector");

        builder.Entity<Organization>(e =>
        {
            e.Property(o => o.Name).HasMaxLength(Organization.NameMaxLength);
            e.HasOne<AppUser>().WithMany().HasForeignKey(o => o.OwnerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(o => new { o.OwnerId, o.CreatedAt });
            // AD-010: the organization is the ownership root; everything else is reached through it.
            e.HasQueryFilter(o => o.OwnerId == CurrentUserId);
        });

        builder.Entity<Assistant>(e =>
        {
            e.Property(a => a.Name).HasMaxLength(Assistant.NameMaxLength);
            e.Property(a => a.Instructions).HasMaxLength(Assistant.InstructionsMaxLength);
            e.Property(a => a.RoutingDescription).HasMaxLength(Assistant.RoutingDescriptionMaxLength);
            e.HasOne(a => a.Organization).WithMany(o => o.Assistants).HasForeignKey(a => a.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(a => a.Organization.OwnerId == CurrentUserId);
        });

        builder.Entity<Document>(e =>
        {
            e.Property(d => d.FileName).HasMaxLength(255);
            e.Property(d => d.ContentSha256).HasMaxLength(64);
            e.HasOne(d => d.Organization).WithMany(o => o.Documents).HasForeignKey(d => d.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            // Door 1: the same content cannot be attached twice to the same organization.
            e.HasIndex(d => new { d.OrganizationId, d.ContentSha256 }).IsUnique();
            e.HasQueryFilter(d => d.Organization.OwnerId == CurrentUserId);
        });

        builder.Entity<Chunk>(e =>
        {
            e.HasOne(c => c.Document).WithMany(d => d.Chunks).HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.Property(c => c.Embedding).HasColumnType($"vector({AiOptions.EmbeddingDimensions})");
            // Approximate nearest neighbour by cosine distance.
            e.HasIndex(c => c.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
            e.HasQueryFilter(c => c.Document.Organization.OwnerId == CurrentUserId);
        });

        builder.Entity<Gap>(e =>
        {
            e.Property(g => g.Status).HasConversion(s => s.ToString().ToLowerInvariant(), s => Enum.Parse<GapStatus>(s, true)).HasMaxLength(16);
            e.HasOne<AppUser>().WithMany().HasForeignKey(g => g.OwnerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(g => g.Organization).WithMany().HasForeignKey(g => g.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(g => g.Assistant).WithMany().HasForeignKey(g => g.AssistantId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Document>().WithMany().HasForeignKey(g => g.DocumentId).OnDelete(DeleteBehavior.SetNull);
            // Door 6: at most one open gap per (owner, organization, normalized question); a null organization counts as a value.
            e.HasIndex(g => new { g.OwnerId, g.OrganizationId, g.NormalizedQuestion })
                .IsUnique()
                .AreNullsDistinct(false)
                .HasFilter("status = 'open'");
            e.HasQueryFilter(g => g.OwnerId == CurrentUserId);
        });
    }
}


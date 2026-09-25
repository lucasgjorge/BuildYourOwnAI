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
    public DbSet<StudySession> StudySessions => Set<StudySession>();
    public DbSet<StudyQuestion> StudyQuestions => Set<StudyQuestion>();
    public DbSet<AiUsage> AiUsages => Set<AiUsage>();

    // Read per query by the global filters below; EF parameterizes it per DbContext instance.
    private string? CurrentUserId => currentUser.Id;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("vector");

        builder.Entity<AppUser>(e => e.Property(u => u.FullName).HasMaxLength(AppUser.FullNameMaxLength));

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

        builder.Entity<StudySession>(e =>
        {
            e.HasOne(s => s.Organization).WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(s => s.Organization.OwnerId == CurrentUserId);
        });

        builder.Entity<StudyQuestion>(e =>
        {
            // study-mode door 1: the answer key stays on the server; a question is answered once.
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_study_questions_options", "cardinality(options) = 4");
                t.HasCheckConstraint("ck_study_questions_correct_option", "correct_option between 0 and 3");
                t.HasCheckConstraint("ck_study_questions_chosen_option", "chosen_option is null or chosen_option between 0 and 3");
            });
            e.HasOne(q => q.Session).WithMany(s => s.Questions).HasForeignKey(q => q.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(q => q.Document).WithMany().HasForeignKey(q => q.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(q => new { q.SessionId, q.Position }).IsUnique();
            e.HasQueryFilter(q => q.Session.Organization.OwnerId == CurrentUserId);
        });

        // admin-usage door 1: no owner filter - only the admin report reads it, across every user.
        builder.Entity<AiUsage>(e =>
        {
            e.ToTable("ai_usage", t =>
            {
                t.HasCheckConstraint("ck_ai_usage_mode", "mode in ('ask', 'routing', 'study', 'upload', 'gap')");
                t.HasCheckConstraint("ck_ai_usage_operation", "operation in ('chat', 'embedding', 'choice')");
            });
            e.Property(u => u.CorrelationId).HasMaxLength(AiUsage.CorrelationIdMaxLength);
            e.Property(u => u.Mode).HasConversion(m => m.ToString().ToLowerInvariant(), m => Enum.Parse<UsageMode>(m, true)).HasMaxLength(16);
            e.Property(u => u.Operation).HasConversion(o => o.ToString().ToLowerInvariant(), o => Enum.Parse<UsageOperation>(o, true)).HasMaxLength(16);
            e.Property(u => u.Model).HasMaxLength(AiUsage.ModelMaxLength);
            e.Property(u => u.CostUsd).HasPrecision(12, 6);
            e.HasOne<AppUser>().WithMany().HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(u => u.OccurredAt);
            e.HasIndex(u => new { u.UserId, u.OccurredAt });
        });
    }
}


using Microsoft.EntityFrameworkCore;
using api.Models;

namespace api.Data;

public class BillingDbContext : DbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options)
    {
    }

    public DbSet<Patient> Patients { get; set; } = null!;
    public DbSet<Encounter> Encounters { get; set; } = null!;
    public DbSet<Claim> Claims { get; set; } = null!;
    public DbSet<ChargeLine> ChargeLines { get; set; } = null!;
    public DbSet<WorkQueueItem> WorkQueueItems { get; set; } = null!;
    public DbSet<TriageNote> TriageNotes { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Patient
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("Patient", "dbo");
            entity.HasKey(e => e.PatientId);
            entity.Property(e => e.EpicPatientId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.MRNHash).HasMaxLength(100);
            entity.Property(e => e.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(e => e.EpicPatientId).IsUnique();
        });

        // Encounter
        modelBuilder.Entity<Encounter>(entity =>
        {
            entity.ToTable("Encounter", "dbo");
            entity.HasKey(e => e.EncounterId);
            entity.Property(e => e.EpicEncounterId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(e => e.EpicEncounterId).IsUnique();
            entity.HasOne(e => e.Patient)
                .WithMany(p => p.Encounters)
                .HasForeignKey(e => e.PatientId);
        });

        // Claim
        modelBuilder.Entity<Claim>(entity =>
        {
            entity.ToTable("Claim", "dbo");
            entity.HasKey(e => e.ClaimId);
            entity.Property(e => e.EpicClaimId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Payer).HasMaxLength(100);
            entity.Property(e => e.BillType).HasMaxLength(20);
            entity.Property(e => e.TotalCharge).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalAllowed).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalPaid).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ClaimStatus).HasMaxLength(50);
            entity.Property(e => e.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(e => e.EpicClaimId).IsUnique();
            entity.HasOne(e => e.Encounter)
                .WithMany(en => en.Claims)
                .HasForeignKey(e => e.EncounterId);
        });

        // ChargeLine
        modelBuilder.Entity<ChargeLine>(entity =>
        {
            entity.ToTable("ChargeLine", "dbo");
            entity.HasKey(e => e.ChargeLineId);
            entity.Property(e => e.CPT).HasMaxLength(10);
            entity.Property(e => e.Modifier).HasMaxLength(10);
            entity.Property(e => e.ChargeAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AllowedAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PaidAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DenialCode).HasMaxLength(20);
            entity.Property(e => e.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne(e => e.Claim)
                .WithMany(c => c.ChargeLines)
                .HasForeignKey(e => e.ClaimId);
        });

        // WorkQueueItem
        modelBuilder.Entity<WorkQueueItem>(entity =>
        {
            entity.ToTable("WorkQueueItem", "dbo");
            entity.HasKey(e => e.WorkQueueItemId);
            entity.Property(e => e.QueueName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AssignedToUpn).HasMaxLength(255);
            entity.Property(e => e.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne(e => e.Claim)
                .WithOne(c => c.WorkQueueItem)
                .HasForeignKey<WorkQueueItem>(e => e.ClaimId);
        });

        // TriageNote
        modelBuilder.Entity<TriageNote>(entity =>
        {
            entity.ToTable("TriageNote", "dbo");
            entity.HasKey(e => e.TriageNoteId);
            entity.Property(e => e.AuthorUpn).HasMaxLength(255).IsRequired();
            entity.Property(e => e.NoteText).IsRequired();
            entity.Property(e => e.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne(e => e.WorkQueueItem)
                .WithMany(w => w.TriageNotes)
                .HasForeignKey(e => e.WorkQueueItemId);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog", "dbo");
            entity.HasKey(e => e.AuditLogId);
            entity.Property(e => e.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ActorUpn).HasMaxLength(255).IsRequired();
            entity.Property(e => e.CreatedUtc).HasDefaultValueSql("GETUTCDATE()");
        });
    }
}

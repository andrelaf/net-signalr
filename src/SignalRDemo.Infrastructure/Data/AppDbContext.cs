using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SignalRDemo.Domain.Entities;

namespace SignalRDemo.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PresenceSession> PresenceSessions => Set<PresenceSession>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite não ordena/compara DateTimeOffset (armazenado como TEXT com offset).
        // O DateTimeOffsetToBinaryConverter o codifica como long ordenável, preservando o offset,
        // permitindo comparações (<, >, ordenação) traduzíveis no banco.
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserName).IsUnique();
            e.Property(x => x.UserName).HasMaxLength(64).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
        });

        b.Entity<Ticket>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Subject).HasMaxLength(256).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);

            // Concorrência otimista: RowVersion é o token. SQLite não tem rowversion nativo,
            // então usamos um Guid marcado como ConcurrencyToken (atualizado a cada save).
            e.Property(x => x.RowVersion).IsConcurrencyToken();

            e.HasOne(x => x.Customer)
                .WithMany(u => u.TicketsOpened)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.AssignedAgent)
                .WithMany(u => u.TicketsAssigned)
                .HasForeignKey(x => x.AssignedAgentId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.Status);
        });

        b.Entity<Message>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);

            e.HasOne(x => x.Ticket)
                .WithMany(t => t.Messages)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.TicketId, x.SentAt });
        });

        b.Entity<Attachment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            e.HasOne(x => x.Message)
                .WithOne(m => m.Attachment)
                .HasForeignKey<Attachment>(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(64).IsRequired();
            e.HasOne(x => x.Recipient)
                .WithMany()
                .HasForeignKey(x => x.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RecipientUserId, x.IsRead });
        });

        b.Entity<PresenceSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ConnectionId).HasMaxLength(128).IsRequired();
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ConnectionId);
        });
    }

    /// <summary>
    /// Renova o token de concorrência das entidades Ticket modificadas a cada SaveChanges,
    /// já que SQLite não atualiza rowversion automaticamente.
    /// </summary>
    public override int SaveChanges()
    {
        BumpTicketRowVersions();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        BumpTicketRowVersions();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void BumpTicketRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries<Ticket>())
        {
            if (entry.State is EntityState.Modified or EntityState.Added)
                entry.Entity.RowVersion = Guid.NewGuid();
        }
    }
}

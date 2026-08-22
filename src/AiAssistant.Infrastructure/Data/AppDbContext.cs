using AiAssistant.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace AiAssistant.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<KnowledgeEntry> Knowledge => Set<KnowledgeEntry>();
    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    private readonly string _dbPath;

    public AppDbContext(string dbPath = "AiAssistant.db")
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KnowledgeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Question).IsRequired();
            entity.Property(e => e.Answer).IsRequired();
            entity.HasIndex(e => e.Topic);
        });

        modelBuilder.Entity<ConversationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
        });

        modelBuilder.Entity<MessageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConversationId).IsRequired();
            entity.HasIndex(e => e.ConversationId);
        });
    }
}

using Microsoft.EntityFrameworkCore;
using PaymentProcessor.Models;

namespace PaymentProcessor.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionEvent> TransactionEvents => Set<TransactionEvent>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");

            // Índice para acelerar las búsquedas por comercio y estado
            entity.HasIndex(t => new { t.MerchantId, t.Status })
                  .HasDatabaseName("IX_Transactions_MerchantId_Status");
        });

        modelBuilder.Entity<TransactionEvent>(entity =>
        {
            entity.ToTable("TransactionEvents");

            // Relación de la transacción con sus eventos (borrado en cascada)
            entity.HasOne(e => e.Transaction)
                  .WithMany(t => t.Events)
                  .HasForeignKey(e => e.TransactionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IdempotencyRecords");

            // Usamos la llave de idempotencia como PK para que la BD bloquee los duplicados
            entity.HasKey(i => i.Key);
        });
    }
}
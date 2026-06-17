using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PaymentProcessor.DTOs;
using PaymentProcessor.Models;

namespace PaymentProcessor.Data;

// SOLID (SRP): Responsabilidad única → operaciones de persistencia de pagos.
// SOLID (DIP): Implementa IPaymentRepository; puede ser reemplazada sin afectar servicios.
public class PaymentRepository : IPaymentRepository
{
    private readonly ApplicationDbContext _db;

    public PaymentRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IdempotencyRecord?> GetIdempotencyRecordAsync(string key)
    {
        return await _db.IdempotencyRecords
            .Where(i => i.Key == key && i.CreatedAt >= DateTime.UtcNow.AddHours(-24))
            .FirstOrDefaultAsync();
    }

    public async Task<CreatePaymentResponse> CreateTransactionWithIdempotencyAsync(
        Transaction transaction, IdempotencyRecord idempotencyRecord)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using IDbContextTransaction dbTransaction = await _db.Database.BeginTransactionAsync();

            _db.Transactions.Add(transaction);
            await _db.SaveChangesAsync();

            _db.IdempotencyRecords.Add(idempotencyRecord);
            await _db.SaveChangesAsync();

            await dbTransaction.CommitAsync();

            return JsonSerializer.Deserialize<CreatePaymentResponse>(idempotencyRecord.ResponseBody)!;
        });
    }
}

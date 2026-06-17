using Microsoft.EntityFrameworkCore;
using PaymentProcessor.Data;
using PaymentProcessor.DTOs;
using PaymentProcessor.Models;
using PaymentProcessor.Models.Enum;

namespace PaymentProcessor.Services;

public class PaymentQueryService : IPaymentQueryService
{
    private readonly ApplicationDbContext _db;

    public PaymentQueryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PaymentDetailResponse?> GetById(string transactionId)
    {
        if (!Guid.TryParse(transactionId, out var parsedId))
            return null;

        Transaction? transaction = await _db.Transactions
            .Include(t => t.Events.OrderBy(e => e.CreatedAt))
            .FirstOrDefaultAsync(t => t.Id == parsedId);

        if (transaction == null) return null;

        return new PaymentDetailResponse
        {
            TransactionId = transaction.Id,
            MerchantId = transaction.MerchantId,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            CardLastFour = transaction.CardLastFour,
            Status = transaction.Status.ToString(),
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
            Events = transaction.Events.Select(e => new TransactionEventDto
            {
                Id = e.Id,
                EventType = e.EventType,
                PreviousStatus = e.PreviousStatus?.ToString(),
                NewStatus = e.NewStatus.ToString(),
                CreatedAt = e.CreatedAt
            }).ToList()
        };
    }

    public async Task<PaymentListResponse> GetFiltered(string? merchantId, string? status)
    {
        IQueryable<Transaction> query = _db.Transactions
            .Where(t => t.MerchantId == merchantId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TransactionStatusEnum>(status, true, out TransactionStatusEnum statusEnum))
        {
            query = query.Where(t => t.Status == statusEnum);
        }

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PaymentSummaryDto
            {
                TransactionId = t.Id,
                MerchantId = t.MerchantId,
                Amount = t.Amount,
                Currency = t.Currency,
                Status = t.Status.ToString(),
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return new PaymentListResponse
        {
            Items = items,
            TotalCount = items.Count
        };
    }
}

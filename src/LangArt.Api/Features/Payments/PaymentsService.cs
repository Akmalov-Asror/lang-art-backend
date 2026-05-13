using LangArt.Api.Common.Exceptions;
using LangArt.Api.Data;
using LangArt.Api.Data.Entities;
using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Payments.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Payments;

public class PaymentsService
{
    private readonly AppDbContext _db;

    public PaymentsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<PaymentResponse>> ListAsync(Guid? userId)
    {
        var query = _db.Payments
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.Course)
            .AsQueryable();
        if (userId.HasValue) query = query.Where(p => p.UserId == userId.Value);

        var rows = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return rows.Select(ToResponse).ToList();
    }

    public async Task<PaymentResponse> CreateAsync(CreatePaymentRequest dto)
    {
        var userExists = await _db.Profiles.AnyAsync(p => p.Id == dto.UserId);
        if (!userExists) throw new BadRequestException("User not found");
        var courseExists = await _db.Courses.AnyAsync(c => c.Id == dto.CourseId);
        if (!courseExists) throw new BadRequestException("Course not found");

        var payment = new Payment
        {
            UserId = dto.UserId,
            CourseId = dto.CourseId,
            Amount = dto.Amount,
            Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "USD" : dto.Currency,
            Status = dto.Status ?? PaymentStatus.Completed,
            PeriodStart = DateTime.SpecifyKind(dto.PeriodStart, DateTimeKind.Utc),
            PeriodEnd = DateTime.SpecifyKind(dto.PeriodEnd, DateTimeKind.Utc),
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // Reload with includes so the response has user/course nested.
        var saved = await _db.Payments.AsNoTracking()
            .Include(p => p.User).Include(p => p.Course)
            .FirstAsync(p => p.Id == payment.Id);
        return ToResponse(saved);
    }

    public async Task<PaymentResponse> UpdateStatusAsync(Guid id, PaymentStatus status)
    {
        var payment = await _db.Payments
            .Include(p => p.User).Include(p => p.Course)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("Payment not found");

        payment.Status = status;
        await _db.SaveChangesAsync();
        return ToResponse(payment);
    }

    public async Task<SubscriptionStatusResponse> CheckSubscriptionAsync(Guid userId, Guid courseId)
    {
        var now = DateTime.UtcNow;
        var active = await _db.Payments.AnyAsync(p =>
            p.UserId == userId &&
            p.CourseId == courseId &&
            p.Status == PaymentStatus.Completed &&
            p.PeriodStart <= now &&
            p.PeriodEnd >= now);
        return new SubscriptionStatusResponse { HasSubscription = active };
    }

    private static PaymentResponse ToResponse(Payment p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        CourseId = p.CourseId,
        Amount = p.Amount,
        Currency = p.Currency,
        Status = p.Status.ToString().ToLowerInvariant(),
        PeriodStart = p.PeriodStart,
        PeriodEnd = p.PeriodEnd,
        CreatedAt = p.CreatedAt,
        User = p.User is null ? null : new PaymentUserSummary
        {
            Id = p.User.Id,
            Email = p.User.Email,
            FullName = p.User.FullName,
        },
        Course = p.Course is null ? null : new PaymentCourseSummary
        {
            Id = p.Course.Id,
            Title = p.Course.Title,
        },
    };
}

using System.ComponentModel.DataAnnotations;
using LangArt.Api.Data.Enums;

namespace LangArt.Api.Features.Payments.Dtos;

public class PaymentUserSummary
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class PaymentCourseSummary
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class PaymentResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "pending";
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public PaymentUserSummary? User { get; set; }
    public PaymentCourseSummary? Course { get; set; }
}

public class CreatePaymentRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid CourseId { get; set; }

    [Required, Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    public string? Currency { get; set; }

    public PaymentStatus? Status { get; set; }

    [Required]
    public DateTime PeriodStart { get; set; }

    [Required]
    public DateTime PeriodEnd { get; set; }
}

public class UpdatePaymentStatusRequest
{
    [Required]
    public PaymentStatus Status { get; set; }
}

public class SubscriptionStatusResponse
{
    public bool HasSubscription { get; set; }
}

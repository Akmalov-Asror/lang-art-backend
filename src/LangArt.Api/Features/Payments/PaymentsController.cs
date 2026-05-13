using LangArt.Api.Common.Auth;
using LangArt.Api.Features.Payments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Payments;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentsService _payments;
    private readonly ICurrentUser _currentUser;

    public PaymentsController(PaymentsService payments, ICurrentUser currentUser)
    {
        _payments = payments;
        _currentUser = currentUser;
    }

    [Authorize(Roles = "admin,teacher")]
    [HttpGet("")]
    public Task<List<PaymentResponse>> List([FromQuery] Guid? userId) =>
        _payments.ListAsync(userId);

    [Authorize]
    [HttpGet("my-payments")]
    public Task<List<PaymentResponse>> MyPayments() =>
        _payments.ListAsync(_currentUser.Id);

    [Authorize(Roles = "admin")]
    [HttpPost("")]
    public Task<PaymentResponse> Create([FromBody] CreatePaymentRequest dto) =>
        _payments.CreateAsync(dto);

    [Authorize]
    [HttpGet("subscription/{userId:guid}/{courseId:guid}")]
    public Task<SubscriptionStatusResponse> CheckSubscription(Guid userId, Guid courseId) =>
        _payments.CheckSubscriptionAsync(userId, courseId);

    [Authorize(Roles = "admin")]
    [HttpPut("{id:guid}/status")]
    public Task<PaymentResponse> UpdateStatus(Guid id, [FromBody] UpdatePaymentStatusRequest dto) =>
        _payments.UpdateStatusAsync(id, dto.Status);
}

using LangArt.Api.Data.Enums;
using LangArt.Api.Features.Users.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Users;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "admin")]
public class UsersController : ControllerBase
{
    private readonly UsersService _users;

    public UsersController(UsersService users)
    {
        _users = users;
    }

    [HttpGet("")]
    public Task<List<UserResponse>> List([FromQuery] Role? role) =>
        _users.ListAsync(role);

    [HttpGet("stats")]
    public Task<UserStatsResponse> Stats() =>
        _users.GetStatsAsync();

    [HttpGet("{id:guid}")]
    public Task<UserResponse> GetById(Guid id) =>
        _users.GetByIdAsync(id);

    [HttpPost("")]
    public Task<UserResponse> Create([FromBody] CreateUserRequest dto) =>
        _users.CreateAsync(dto);

    [HttpPut("{id:guid}")]
    public Task<UserResponse> Update(Guid id, [FromBody] UpdateUserRequest dto) =>
        _users.UpdateAsync(id, dto);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _users.DeleteAsync(id);
        return Ok(new { });
    }
}

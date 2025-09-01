using System.Security.Claims;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VladiCore.Api.Models.Staff;
using VladiCore.App.Services;
using VladiCore.Domain.Enums;

namespace VladiCore.Api.Controllers;

[ApiController]
[Route("staff")]
[Authorize]
public class StaffAdminController : ControllerBase
{
    private readonly IStaffService _service;

    public StaffAdminController(IStaffService service)
    {
        _service = service;
    }

    private bool IsAdmin() => User.FindFirstValue(ClaimTypes.Role) == UserRole.Admin.ToString();

    [HttpGet("branches")]
    public async Task<ActionResult<IEnumerable<BranchDto>>> GetBranches()
    {
        var branches = await _service.GetBranchesAsync();
        var result = branches.Select(b => new BranchDto(b.Id, b.Name, b.Address, b.Phone));
        return Ok(result);
    }

    [HttpPost("branches")]
    public async Task<ActionResult<BranchDto>> CreateBranch(CreateBranchRequest request)
    {
        if (!IsAdmin()) return Forbid();
        var branch = await _service.CreateBranchAsync(request.Name, request.Address, request.Phone);
        return Created(string.Empty, new BranchDto(branch.Id, branch.Name, branch.Address, branch.Phone));
    }

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee(CreateEmployeeRequest request)
    {
        if (!IsAdmin()) return Forbid();
        await _service.CreateEmployeeAsync(request.UserId, request.Position, request.BranchId);
        return Created(string.Empty, null);
    }
}

using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/approvals")]
public class ApprovalRequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ApprovalRequestsController(AppDbContext db) { _db = db; }

    public record SubmitRequest(long? ApplicantId, long? FirstApproverId);
    public record ActionRequest(long? ApproverId, string? Comment);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ApprovalRequest>>> List([FromQuery] string? status, [FromQuery] string? targetType, [FromQuery] string? approvalType)
    {
        var query = _db.ApprovalRequests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(targetType)) query = query.Where(x => x.TargetType == targetType);
        if (!string.IsNullOrWhiteSpace(approvalType)) query = query.Where(x => x.ApprovalType == approvalType);
        return await query.OrderByDescending(x => x.Id).Take(300).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<object>> Get(long id)
    {
        var entity = await _db.ApprovalRequests.FindAsync(id);
        if (entity == null) return NotFound();
        var steps = await _db.ApprovalSteps.Where(x => x.ApprovalRequestId == id).OrderBy(x => x.StepNo).ToListAsync();
        return new { request = entity, steps };
    }

    [HttpPost]
    public async Task<ActionResult<ApprovalRequest>> Create(ApprovalRequest input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("APR") : input.No;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "draft" : input.Status;
        input.CreatedAt = DateTime.Now;
        _db.ApprovalRequests.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPost("{id:long}/submit")]
    public async Task<ActionResult<object>> Submit(long id, SubmitRequest request)
    {
        var entity = await _db.ApprovalRequests.FindAsync(id);
        if (entity == null) return NotFound();
        entity.Status = "submitted";
        entity.ApplicantId = request.ApplicantId ?? entity.ApplicantId;
        entity.CurrentApproverId = request.FirstApproverId;
        entity.SubmittedAt = DateTime.Now;
        entity.UpdatedAt = DateTime.Now;
        if (!await _db.ApprovalSteps.AnyAsync(x => x.ApprovalRequestId == id))
        {
            _db.ApprovalSteps.Add(new ApprovalStep { ApprovalRequestId = id, StepNo = 1, StepName = "主管审批", ApproverId = request.FirstApproverId, Status = "pending", CreatedAt = DateTime.Now });
            _db.ApprovalSteps.Add(new ApprovalStep { ApprovalRequestId = id, StepNo = 2, StepName = "经理审批", Status = "waiting", CreatedAt = DateTime.Now });
        }
        await _db.SaveChangesAsync();
        return await Get(id);
    }

    [HttpPost("{id:long}/approve")]
    public async Task<ActionResult<object>> Approve(long id, ActionRequest request)
    {
        var entity = await _db.ApprovalRequests.FindAsync(id);
        if (entity == null) return NotFound();
        var step = await _db.ApprovalSteps.Where(x => x.ApprovalRequestId == id && x.Status == "pending").OrderBy(x => x.StepNo).FirstOrDefaultAsync();
        if (step == null) return BadRequest("No pending approval step");
        step.Status = "approved";
        step.Action = "approve";
        step.ApproverId = request.ApproverId ?? step.ApproverId;
        step.Comment = request.Comment;
        step.ActionAt = DateTime.Now;
        step.UpdatedAt = DateTime.Now;
        var next = await _db.ApprovalSteps.Where(x => x.ApprovalRequestId == id && x.StepNo > step.StepNo).OrderBy(x => x.StepNo).FirstOrDefaultAsync();
        if (next != null)
        {
            next.Status = "pending";
            next.UpdatedAt = DateTime.Now;
            entity.Status = "approving";
            entity.CurrentApproverId = next.ApproverId;
        }
        else
        {
            entity.Status = "approved";
            entity.CurrentApproverId = null;
            entity.CompletedAt = DateTime.Now;
        }
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return await Get(id);
    }

    [HttpPost("{id:long}/reject")]
    public async Task<ActionResult<object>> Reject(long id, ActionRequest request)
    {
        var entity = await _db.ApprovalRequests.FindAsync(id);
        if (entity == null) return NotFound();
        var step = await _db.ApprovalSteps.Where(x => x.ApprovalRequestId == id && x.Status == "pending").OrderBy(x => x.StepNo).FirstOrDefaultAsync();
        if (step != null)
        {
            step.Status = "rejected";
            step.Action = "reject";
            step.ApproverId = request.ApproverId ?? step.ApproverId;
            step.Comment = request.Comment;
            step.ActionAt = DateTime.Now;
            step.UpdatedAt = DateTime.Now;
        }
        entity.Status = "rejected";
        entity.CurrentApproverId = null;
        entity.CompletedAt = DateTime.Now;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return await Get(id);
    }

    [HttpPost("{id:long}/return")]
    public async Task<ActionResult<object>> ReturnBack(long id, ActionRequest request)
    {
        var entity = await _db.ApprovalRequests.FindAsync(id);
        if (entity == null) return NotFound();
        var step = await _db.ApprovalSteps.Where(x => x.ApprovalRequestId == id && x.Status == "pending").OrderBy(x => x.StepNo).FirstOrDefaultAsync();
        if (step != null)
        {
            step.Status = "returned";
            step.Action = "return";
            step.ApproverId = request.ApproverId ?? step.ApproverId;
            step.Comment = request.Comment;
            step.ActionAt = DateTime.Now;
            step.UpdatedAt = DateTime.Now;
        }
        entity.Status = "returned";
        entity.CurrentApproverId = null;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return await Get(id);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.ApprovalRequests.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}

using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/backup-center")]
public class BackupCenterController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly BackupService _backup;
    public BackupCenterController(AppDbContext db, IWebHostEnvironment env, ILogger<BackupService> logger)
    {
        _db = db;
        _backup = new BackupService(db, env, logger);
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<IEnumerable<BackupJob>>> Jobs() => await _db.BackupJobs.OrderByDescending(x => x.Id).ToListAsync();

    [HttpPost("jobs")]
    public async Task<ActionResult<BackupJob>> CreateJob(BackupJob input)
    {
        input.Id = 0;
        input.CreatedAt = DateTime.Now;
        _db.BackupJobs.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("jobs/{id:long}")]
    public async Task<ActionResult<BackupJob>> UpdateJob(long id, BackupJob input)
    {
        var entity = await _db.BackupJobs.FindAsync(id);
        if (entity == null) return NotFound();
        entity.Name = input.Name;
        entity.ScheduleType = input.ScheduleType;
        entity.BackupScope = input.BackupScope;
        entity.StoragePath = input.StoragePath;
        entity.IsEnabled = input.IsEnabled;
        entity.NextRunAt = input.NextRunAt;
        entity.Status = input.Status;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("jobs/{id:long}")]
    public async Task<IActionResult> DeleteJob(long id)
    {
        var entity = await _db.BackupJobs.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("run")]
    public async Task<ActionResult<BackupHistory>> Run([FromQuery] long? jobId = null)
    {
        return await _backup.CreateBackupAsync(jobId, jobId.HasValue ? "scheduled" : "manual");
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<BackupHistory>>> History() => await _db.BackupHistories.OrderByDescending(x => x.Id).Take(300).ToListAsync();

    [HttpGet("restore-history")]
    public async Task<ActionResult<IEnumerable<RestoreHistory>>> RestoreHistory() => await _db.RestoreHistories.OrderByDescending(x => x.Id).Take(300).ToListAsync();

    [HttpPost("verify/{id:long}")]
    public async Task<ActionResult<object>> Verify(long id)
    {
        var backup = await _db.BackupHistories.FindAsync(id);
        if (backup == null) return NotFound();
        return new { id, verified = _backup.VerifyBackupFile(backup.FilePath), fileName = backup.FileName };
    }

    [HttpPost("restore/{id:long}")]
    public async Task<ActionResult<RestoreHistory>> Restore(long id)
    {
        return await _backup.RecordRestoreAsync(id, true);
    }

    [HttpGet("download/{id:long}")]
    public async Task<IActionResult> Download(long id)
    {
        var backup = await _db.BackupHistories.FindAsync(id);
        if (backup == null || !System.IO.File.Exists(backup.FilePath)) return NotFound();
        return PhysicalFile(backup.FilePath, "application/sql", backup.FileName);
    }

    [HttpDelete("history/{id:long}")]
    public async Task<IActionResult> DeleteHistory(long id)
    {
        var entity = await _db.BackupHistories.FindAsync(id);
        if (entity == null) return NotFound();
        if (System.IO.File.Exists(entity.FilePath)) System.IO.File.Delete(entity.FilePath);
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("seed")]
    public async Task<ActionResult<object>> Seed()
    {
        if (!await _db.BackupJobs.AnyAsync(x => x.Name == "每日自动备份"))
        {
            _db.BackupJobs.Add(new BackupJob { Name = "每日自动备份", ScheduleType = "daily", BackupScope = "database", StoragePath = "backups", IsEnabled = false, Status = "ready", CreatedAt = DateTime.Now });
        }
        if (!await _db.BackupJobs.AnyAsync(x => x.Name == "每周自动备份"))
        {
            _db.BackupJobs.Add(new BackupJob { Name = "每周自动备份", ScheduleType = "weekly", BackupScope = "database", StoragePath = "backups", IsEnabled = false, Status = "ready", CreatedAt = DateTime.Now });
        }
        await _db.SaveChangesAsync();
        return new { jobs = await _db.BackupJobs.CountAsync() };
    }
}

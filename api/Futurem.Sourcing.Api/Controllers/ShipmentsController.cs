using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/shipments")]
public class ShipmentsController : ControllerBase
{
    private static readonly string[] AllowedStatuses = ["draft", "confirmed", "shipped", "completed", "cancelled"];

    private readonly AppDbContext _db;
    private readonly ShipmentExpenseService _expenseService;
    private readonly ShipmentMeasurementService _measurementService;
    private readonly ShipmentFinanceSyncService _financeSyncService;

    public ShipmentsController(
        AppDbContext db,
        ShipmentExpenseService expenseService,
        ShipmentMeasurementService measurementService,
        ShipmentFinanceSyncService financeSyncService)
    {
        _db = db;
        _expenseService = expenseService;
        _measurementService = measurementService;
        _financeSyncService = financeSyncService;
    }

    public record GenerateFromContainerRequest(long ContainerLoadId, string? ShipmentMode, string? Carrier, string? DeparturePort, string? DestinationPort, DateTime? Etd, DateTime? Eta, string? Currency);
    public record RecalculateMeasurementsRequest(bool OverwriteFinalValues = false);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Shipment>>> List([FromQuery] long? containerLoadId, [FromQuery] long? summaryOrderId)
    {
        var query = _db.Shipments.AsQueryable();
        if (containerLoadId.HasValue) query = query.Where(x => x.ContainerLoadId == containerLoadId.Value);
        if (summaryOrderId.HasValue) query = query.Where(x => x.SummaryOrderId == summaryOrderId.Value);
        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<Shipment>> Get(long id)
    {
        var entity = await _db.Shipments.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<Shipment>> Create(Shipment input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("SHP") : input.No.Trim();
        input.Status = "draft";
        input.ShipmentMode = string.IsNullOrWhiteSpace(input.ShipmentMode) ? "SEA" : input.ShipmentMode;
        input.Currency = string.IsNullOrWhiteSpace(input.Currency) ? "RMB" : input.Currency.Trim().ToUpperInvariant();
        input.CreatedAt = DateTime.Now;
        _db.Shipments.Add(input);
        await _db.SaveChangesAsync();
        await _expenseService.EnsureDefaultsAsync(input.Id);
        await _measurementService.RecalculateAsync(input.Id, true);
        return input;
    }

    [HttpPost("generate-from-container")]
    public async Task<ActionResult<Shipment>> GenerateFromContainer(GenerateFromContainerRequest request)
    {
        if (request.ContainerLoadId <= 0) return BadRequest("ContainerLoadId required");
        var cl = await _db.ContainerLoads.FindAsync(request.ContainerLoadId);
        if (cl == null) return NotFound();

        var shipment = new Shipment
        {
            No = NumberService.NewNo("SHP"),
            ContainerLoadId = cl.Id,
            SummaryOrderId = cl.SummaryOrderId,
            ShipmentMode = string.IsNullOrWhiteSpace(request.ShipmentMode) ? "SEA" : request.ShipmentMode!,
            Carrier = request.Carrier,
            DeparturePort = request.DeparturePort,
            DestinationPort = request.DestinationPort,
            Etd = request.Etd ?? DateTime.Today,
            Eta = request.Eta,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "RMB" : request.Currency.Trim().ToUpperInvariant(),
            Status = "draft",
            Remark = $"由装柜单 {cl.No} 生成",
            CreatedAt = DateTime.Now
        };
        _db.Shipments.Add(shipment);
        cl.Status = "shipment_created";
        cl.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "CL", cl.Id, "SHP", shipment.Id);
        await _db.SaveChangesAsync();
        await _expenseService.EnsureDefaultsAsync(shipment.Id);
        await _measurementService.RecalculateAsync(shipment.Id, true);
        return shipment;
    }

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<Shipment>> Copy(long id)
    {
        var source = await _db.Shipments.FindAsync(id);
        if (source == null) return NotFound();
        var copy = new Shipment
        {
            No = NumberService.NewNo("SHP"),
            ContainerLoadId = source.ContainerLoadId,
            SummaryOrderId = source.SummaryOrderId,
            ShipmentMode = source.ShipmentMode,
            Carrier = source.Carrier,
            DeparturePort = source.DeparturePort,
            DestinationPort = source.DestinationPort,
            Etd = DateTime.Today,
            Eta = source.Eta,
            Currency = source.Currency,
            Status = "draft",
            Remark = $"复制自 {source.No}",
            CreatedAt = DateTime.Now
        };
        _db.Shipments.Add(copy);
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "SHP", source.Id, "SHP", copy.Id);
        await _db.SaveChangesAsync();
        await _expenseService.EnsureDefaultsAsync(copy.Id);

        var sourceExpenses = await _db.ShipmentExpenses.Where(x => x.ShipmentId == source.Id).ToListAsync();
        var copyExpenses = await _db.ShipmentExpenses.Where(x => x.ShipmentId == copy.Id).ToListAsync();
        foreach (var sourceExpense in sourceExpenses)
        {
            if (sourceExpense.IsCustom)
            {
                _db.ShipmentExpenses.Add(new ShipmentExpense
                {
                    ShipmentId = copy.Id,
                    ExpenseCode = sourceExpense.ExpenseCode,
                    ExpenseName = sourceExpense.ExpenseName,
                    NormalizedExpenseName = sourceExpense.NormalizedExpenseName,
                    IsCustom = true,
                    SupplierId = sourceExpense.SupplierId,
                    Amount = sourceExpense.Amount,
                    Currency = copy.Currency,
                    SortNo = sourceExpense.SortNo,
                    Remark = sourceExpense.Remark,
                    CreatedAt = DateTime.Now
                });
            }
            else
            {
                var target = copyExpenses.First(x => x.ExpenseCode == sourceExpense.ExpenseCode);
                target.SupplierId = sourceExpense.SupplierId;
                target.Amount = sourceExpense.Amount;
                target.Remark = sourceExpense.Remark;
                target.UpdatedAt = DateTime.Now;
            }
        }
        await _db.SaveChangesAsync();
        await _expenseService.RecalculateExpenseTotalAsync(copy.Id);
        await _measurementService.RecalculateAsync(copy.Id, true);
        return copy;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<Shipment>> Update(long id, Shipment input)
    {
        var entity = await _db.Shipments.FindAsync(id);
        if (entity == null) return NotFound();

        var newCurrency = string.IsNullOrWhiteSpace(input.Currency) ? entity.Currency : input.Currency.Trim().ToUpperInvariant();
        if (!string.Equals(entity.Currency, newCurrency, StringComparison.OrdinalIgnoreCase))
        {
            var hasFinance = await _db.ShipmentExpenses
                .Where(x => x.ShipmentId == id && x.FinanceRecordId.HasValue)
                .AnyAsync();
            if (hasFinance) return BadRequest(new { message = "已生成应付，不能直接修改出运单币种" });
            entity.Currency = newCurrency;
            var expenses = await _db.ShipmentExpenses.Where(x => x.ShipmentId == id).ToListAsync();
            foreach (var expense in expenses) expense.Currency = newCurrency;
        }

        entity.ContainerLoadId = input.ContainerLoadId;
        entity.SummaryOrderId = input.SummaryOrderId;
        entity.ShipmentMode = input.ShipmentMode;
        entity.Carrier = input.Carrier;
        entity.VesselVoyage = input.VesselVoyage;
        entity.BillOfLadingNo = input.BillOfLadingNo;
        entity.DeparturePort = input.DeparturePort;
        entity.DestinationPort = input.DestinationPort;
        entity.Etd = input.Etd;
        entity.Eta = input.Eta;
        entity.FinalTotalCbm = FinanceBalanceService.Round2(input.FinalTotalCbm);
        entity.FinalGrossWeightKg = FinanceBalanceService.Round2(input.FinalGrossWeightKg);
        entity.FinalNetWeightKg = FinanceBalanceService.Round2(input.FinalNetWeightKg);
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpPost("{id:long}/recalculate-measurements")]
    public async Task<ActionResult<Shipment>> RecalculateMeasurements(long id, RecalculateMeasurementsRequest request)
    {
        try
        {
            return await _measurementService.RecalculateAsync(id, request.OverwriteFinalValues);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:long}/confirm")]
    public Task<ActionResult<Shipment>> Confirm(long id) => ChangeStatusAndSync(id, "confirmed");

    [HttpPost("{id:long}/mark-shipped")]
    public Task<ActionResult<Shipment>> MarkShipped(long id) => ChangeStatusAndSync(id, "shipped");

    [HttpPost("{id:long}/sync-finance")]
    public async Task<ActionResult<Shipment>> SyncFinance(long id)
    {
        var shipment = await _db.Shipments.FindAsync(id);
        if (shipment == null) return NotFound();
        if (!new[] { "confirmed", "shipped", "completed" }.Contains(shipment.Status))
            return BadRequest(new { message = "草稿出运单不能同步财务" });
        return await ChangeStatusAndSync(id, shipment.Status);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.Shipments.FindAsync(id);
        if (entity == null) return NotFound();
        var hasSettledFinance = await _db.ShipmentExpenses
            .Where(x => x.ShipmentId == id && x.FinanceRecordId.HasValue)
            .Join(_db.FinanceRecords, x => x.FinanceRecordId, x => x.Id, (expense, finance) => finance)
            .AnyAsync(x => x.PaidAmount > 0m || x.PrepaymentAppliedAmount > 0m);
        if (hasSettledFinance) return BadRequest(new { message = "出运费用已有付款或预付款抵扣，不能删除出运单" });

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private async Task<ActionResult<Shipment>> ChangeStatusAndSync(long id, string targetStatus)
    {
        if (!AllowedStatuses.Contains(targetStatus)) return BadRequest(new { message = "出运状态无效" });
        await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync() : null;
        try
        {
            var shipment = await _db.Shipments.FindAsync(id);
            if (shipment == null) return NotFound();
            await _expenseService.ValidateAllAsync(id);
            await _measurementService.RecalculateAsync(id, false);
            shipment.Status = targetStatus;
            shipment.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _financeSyncService.SyncAsync(id);
            if (transaction != null) await transaction.CommitAsync();
            return shipment;
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync();
            _db.ChangeTracker.Clear();
            var shipment = await _db.Shipments.FindAsync(id);
            if (shipment != null)
            {
                shipment.FinanceSyncStatus = "error";
                shipment.FinanceSyncMessage = ex.Message;
                shipment.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
            return BadRequest(new { message = ex.Message });
        }
    }
}

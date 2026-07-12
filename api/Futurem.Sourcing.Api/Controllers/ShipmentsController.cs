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
        if (input.ContainerLoadId.HasValue)
        {
            var existing = await _db.Shipments.FirstOrDefaultAsync(x => x.ContainerLoadId == input.ContainerLoadId.Value);
            if (existing is not null) return existing;
            throw new BusinessRuleException(
                "SHIPMENT_MUST_BE_GENERATED_FROM_CONFIRMED_CONTAINER",
                "关联装柜单的出运单必须由装柜确认自动生成");
        }

        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("SHP") : input.No.Trim();
        input.Status = "draft";
        input.ShipmentMode = string.IsNullOrWhiteSpace(input.ShipmentMode) ? "SEA" : input.ShipmentMode;
        input.Currency = RmbMoneyService.Currency;
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

        var existing = await _db.Shipments.FirstOrDefaultAsync(x => x.ContainerLoadId == cl.Id);
        if (existing is not null)
        {
            if (existing.Status == "draft")
            {
                existing.ShipmentMode = string.IsNullOrWhiteSpace(request.ShipmentMode) ? existing.ShipmentMode : request.ShipmentMode!;
                existing.Carrier = request.Carrier ?? existing.Carrier;
                existing.DeparturePort = request.DeparturePort ?? existing.DeparturePort;
                existing.DestinationPort = request.DestinationPort ?? existing.DestinationPort;
                existing.Etd = request.Etd ?? existing.Etd;
                existing.Eta = request.Eta ?? existing.Eta;
                existing.Currency = RmbMoneyService.Currency;
                existing.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
            await _expenseService.EnsureDefaultsAsync(existing.Id);
            return existing;
        }

        if (cl.Status is not ("confirmed" or "shipment_created" or "completed"))
        {
            throw new BusinessRuleException(
                "CONTAINER_NOT_CONFIRMED",
                "装柜确认后系统才会生成出运单草稿");
        }

        var shipment = new Shipment
        {
            No = NumberService.NewNo("SHP"),
            ContainerLoadId = cl.Id,
            SummaryOrderId = cl.SummaryOrderId,
            CustomerId = cl.CustomerId,
            WarehouseId = cl.WarehouseId,
            ContainerType = cl.ContainerType,
            ContainerNo = cl.ContainerNo,
            SealNo = cl.SealNo,
            ShipmentMode = string.IsNullOrWhiteSpace(request.ShipmentMode) ? "SEA" : request.ShipmentMode!,
            Carrier = request.Carrier,
            DeparturePort = request.DeparturePort,
            DestinationPort = request.DestinationPort,
            Etd = request.Etd,
            Eta = request.Eta,
            Currency = RmbMoneyService.Currency,
            Status = "draft",
            CalculatedTotalCbm = RmbMoneyService.Round(cl.TotalCbm),
            FinalTotalCbm = RmbMoneyService.Round(cl.TotalCbm),
            CalculatedGrossWeightKg = RmbMoneyService.Round(cl.TotalGwKg),
            FinalGrossWeightKg = RmbMoneyService.Round(cl.TotalGwKg),
            Remark = $"由装柜单 {cl.No} 生成",
            CreatedAt = DateTime.Now
        };
        _db.Shipments.Add(shipment);
        cl.Status = "shipment_created";
        cl.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await _expenseService.EnsureDefaultsAsync(shipment.Id);
        return shipment;
    }

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<Shipment>> Copy(long id)
    {
        var source = await _db.Shipments.FindAsync(id);
        if (source == null) return NotFound();
        if (source.ContainerLoadId.HasValue)
        {
            throw new BusinessRuleException(
                "CONTAINER_SHIPMENT_CANNOT_BE_COPIED",
                "一张装柜单只能对应一张出运单，不能复制关联装柜单的出运单");
        }

        var copy = new Shipment
        {
            No = NumberService.NewNo("SHP"),
            ContainerLoadId = null,
            SummaryOrderId = source.SummaryOrderId,
            CustomerId = source.CustomerId,
            WarehouseId = source.WarehouseId,
            ShipmentMode = source.ShipmentMode,
            Carrier = source.Carrier,
            DeparturePort = source.DeparturePort,
            DestinationPort = source.DestinationPort,
            Etd = DateTime.Today,
            Eta = source.Eta,
            Currency = RmbMoneyService.Currency,
            Status = "draft",
            Remark = $"复制自 {source.No}",
            CreatedAt = DateTime.Now
        };
        _db.Shipments.Add(copy);
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
                    Currency = RmbMoneyService.Currency,
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
                target.Currency = RmbMoneyService.Currency;
                target.Remark = sourceExpense.Remark;
                target.UpdatedAt = DateTime.Now;
            }
        }
        await _db.SaveChangesAsync();
        await _expenseService.RecalculateExpenseTotalAsync(copy.Id);
        return copy;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<Shipment>> Update(long id, Shipment input)
    {
        var entity = await _db.Shipments.FindAsync(id);
        if (entity == null) return NotFound();
        if (entity.Status is "shipped" or "completed")
            throw new BusinessRuleException("SHIPMENT_LOCKED", "已发运出运单不能直接修改");
        if (input.ContainerLoadId != entity.ContainerLoadId)
            throw new BusinessRuleException("SHIPMENT_CONTAINER_LOCKED", "出运单的装柜单来源不能修改");

        entity.ShipmentMode = input.ShipmentMode;
        entity.Carrier = input.Carrier;
        entity.VesselVoyage = input.VesselVoyage;
        entity.BillOfLadingNo = input.BillOfLadingNo;
        entity.DeparturePort = input.DeparturePort;
        entity.DestinationPort = input.DestinationPort;
        entity.Etd = input.Etd;
        entity.Eta = input.Eta;
        entity.Currency = RmbMoneyService.Currency;
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
        if (entity.ContainerLoadId.HasValue)
            throw new BusinessRuleException("CONTAINER_SHIPMENT_CANNOT_BE_DELETED", "装柜确认自动生成的出运单不能直接删除");

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
            shipment.Currency = RmbMoneyService.Currency;
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

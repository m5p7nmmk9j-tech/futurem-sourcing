using System.Security.Claims;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/container-loads")]
public class ContainerLoadsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ContainerReservationService _reservations;
    private readonly ContainerConfirmationService _confirmation;

    public ContainerLoadsController(
        AppDbContext db,
        ContainerReservationService reservations,
        ContainerConfirmationService confirmation)
    {
        _db = db;
        _reservations = reservations;
        _confirmation = confirmation;
    }

    public record GenerateFromSoRequest(long SummaryOrderId, string? ContainerType, DateTime? LoadDate);
    public record ReservationItemRequest(long InventoryLotId, decimal Quantity, decimal Cartons);
    public record LockInventoryRequest(List<ReservationItemRequest> Items);
    public record ReleaseInventoryRequest(string Reason);
    public record ActualLoadLineRequest(long InventoryReservationId, decimal ActualQuantity, decimal ActualCartons);
    public record ConfirmContainerRequest(List<ActualLoadLineRequest> Lines);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContainerLoad>>> List(
        [FromQuery] long? summaryOrderId,
        [FromQuery] long? customerId,
        [FromQuery] long? warehouseId)
    {
        var query = _db.ContainerLoads.AsQueryable();
        if (summaryOrderId.HasValue) query = query.Where(x => x.SummaryOrderId == summaryOrderId.Value);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (warehouseId.HasValue) query = query.Where(x => x.WarehouseId == warehouseId.Value);
        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ContainerLoad>> Get(long id)
    {
        var entity = await _db.ContainerLoads.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpGet("{id:long}/reservations")]
    public async Task<IActionResult> Reservations(long id)
    {
        var container = await _db.ContainerLoads.FindAsync(id);
        if (container is null) return NotFound();
        var reservations = await _db.InventoryReservations
            .Where(x => x.ContainerLoadId == id)
            .OrderByDescending(x => x.Id)
            .ToListAsync();
        var lotIds = reservations.Select(x => x.InventoryLotId).Distinct().ToList();
        var lots = await _db.InventoryLots.Where(x => lotIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var productIds = lots.Values.Select(x => x.OrderProductId).Distinct().ToList();
        var products = await _db.OrderProducts.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return Ok(new
        {
            container,
            items = reservations.Select(reservation =>
            {
                lots.TryGetValue(reservation.InventoryLotId, out var lot);
                OrderProduct? product = null;
                if (lot is not null) products.TryGetValue(lot.OrderProductId, out product);
                return new { reservation, lot, product };
            })
        });
    }

    [HttpGet("{id:long}/sources")]
    public async Task<IActionResult> Sources(long id)
    {
        var container = await _db.ContainerLoads.FindAsync(id);
        if (container is null) return NotFound();
        var sources = await _db.ContainerLoadSources
            .Where(x => x.ContainerLoadId == id)
            .OrderBy(x => x.Id)
            .ToListAsync();
        var productIds = sources.Select(x => x.OrderProductId).Distinct().ToList();
        var products = await _db.OrderProducts.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return Ok(new
        {
            container,
            items = sources.Select(source =>
            {
                products.TryGetValue(source.OrderProductId, out var product);
                return new { source, product };
            })
        });
    }

    [HttpPost("{id:long}/lock-inventory")]
    public async Task<IActionResult> LockInventory(long id, LockInventoryRequest request)
    {
        var result = await _reservations.LockAsync(
            id,
            request.Items.Select(x => new InventoryReservationInput(x.InventoryLotId, x.Quantity, x.Cartons)).ToList(),
            CurrentUserId());
        return Ok(result);
    }

    [HttpPost("{id:long}/relock-inventory")]
    public async Task<IActionResult> RelockInventory(long id, LockInventoryRequest request)
    {
        var result = await _reservations.RelockAsync(
            id,
            request.Items.Select(x => new InventoryReservationInput(x.InventoryLotId, x.Quantity, x.Cartons)).ToList(),
            CurrentUserId());
        return Ok(result);
    }

    [HttpPost("{id:long}/release-inventory")]
    public async Task<IActionResult> ReleaseInventory(long id, ReleaseInventoryRequest request)
        => Ok(new { released = await _reservations.ReleaseAsync(id, request.Reason, CurrentUserId()) });

    [HttpPost("{id:long}/confirm")]
    public async Task<IActionResult> Confirm(long id, ConfirmContainerRequest request)
    {
        var result = await _confirmation.ConfirmAsync(
            id,
            request.Lines.Select(x => new ActualLoadInput(
                x.InventoryReservationId,
                x.ActualQuantity,
                x.ActualCartons)).ToList(),
            CurrentUserId());
        return Ok(new
        {
            containerLoad = result.ContainerLoad,
            receivable = result.Receivable,
            shipment = result.Shipment
        });
    }

    [HttpGet("recommend")]
    public async Task<ActionResult<object>> Recommend([FromQuery] long? summaryOrderId = null, [FromQuery] long? containerLoadId = null)
    {
        decimal cbm = 0, gw = 0, cartons = 0;
        if (containerLoadId.HasValue)
        {
            var cl = await _db.ContainerLoads.FindAsync(containerLoadId.Value);
            if (cl == null) return NotFound();
            cbm = cl.TotalCbm;
            gw = cl.TotalGwKg;
            cartons = cl.TotalCartons;
        }
        else if (summaryOrderId.HasValue)
        {
            var so = await _db.SummaryOrders.FindAsync(summaryOrderId.Value);
            if (so == null) return NotFound();
            cbm = so.TotalCbm;
            gw = so.TotalGrossWeightKg;
            cartons = so.TotalCartons;
        }
        else return BadRequest("summaryOrderId or containerLoadId required");

        var options = new[] { "20GP", "40GP", "40HQ", "45HQ" }.Select(t =>
        {
            var cap = GetCapacity(t);
            var cbmRate = cap.Cbm <= 0 ? 0 : Math.Round(cbm / cap.Cbm * 100, 2);
            var weightRate = cap.Kg <= 0 ? 0 : Math.Round(gw / cap.Kg * 100, 2);
            var ok = cbm <= cap.Cbm && gw <= cap.Kg;
            return new { containerType = t, capacityCbm = cap.Cbm, capacityKg = cap.Kg, cbmRate, weightRate, remainingCbm = cap.Cbm - cbm, remainingKg = cap.Kg - gw, ok };
        }).ToList();
        var recommended = options.FirstOrDefault(x => x.ok) ?? options.Last();
        return new
        {
            cartons,
            cbm,
            gw,
            recommended = recommended.containerType,
            needSplit = !options.Any(x => x.ok),
            message = options.Any(x => x.ok) ? $"建议使用 {recommended.containerType}" : "单柜无法装完，建议拆分多柜或重新分配装柜",
            options
        };
    }

    [HttpGet("{id:long}/utilization")]
    public async Task<ActionResult<object>> Utilization(long id)
    {
        var cl = await _db.ContainerLoads.FindAsync(id);
        if (cl == null) return NotFound();
        var cartons = cl.TotalCartons;
        var cbm = cl.TotalCbm;
        var gw = cl.TotalGwKg;
        var cap = GetCapacity(cl.ContainerType);
        var cbmRate = cap.Cbm <= 0 ? 0 : Math.Round(cbm / cap.Cbm * 100, 2);
        var weightRate = cap.Kg <= 0 ? 0 : Math.Round(gw / cap.Kg * 100, 2);
        var overCbm = cbm > cap.Cbm;
        var overWeight = gw > cap.Kg;
        var level = overCbm || overWeight ? "danger" : cbmRate >= 95 || weightRate >= 95 ? "warning" : "ok";
        return new
        {
            containerType = cl.ContainerType,
            capacityCbm = cap.Cbm,
            capacityKg = cap.Kg,
            cartons,
            cbm,
            gw,
            remainingCbm = cap.Cbm - cbm,
            remainingKg = cap.Kg - gw,
            cbmRate,
            weightRate,
            overCbm,
            overWeight,
            level,
            message = level == "danger" ? "已超柜，请拆分或换更大柜型" : level == "warning" ? "接近满柜，请谨慎继续添加货物" : "容量正常"
        };
    }

    [HttpPost]
    public async Task<ActionResult<ContainerLoad>> Create(ContainerLoad input)
    {
        if (!input.CustomerId.HasValue || input.CustomerId <= 0)
            throw new BusinessRuleException("CONTAINER_CUSTOMER_REQUIRED", "请选择客户");
        if (!input.WarehouseId.HasValue || input.WarehouseId <= 0)
            throw new BusinessRuleException("CONTAINER_WAREHOUSE_REQUIRED", "请选择仓库");
        if (!await _db.Customers.AnyAsync(x => x.Id == input.CustomerId.Value))
            throw new BusinessRuleException("CUSTOMER_NOT_FOUND", "客户不存在");
        if (!await _db.Warehouses.AnyAsync(x => x.Id == input.WarehouseId.Value && x.Status == "active"))
            throw new BusinessRuleException("WAREHOUSE_NOT_FOUND", "仓库不存在或已停用");

        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("CL") : input.No;
        input.Status = "draft";
        input.InventoryLockedAt = null;
        input.InventoryLockExpiresAt = null;
        input.TotalCartons = 0m;
        input.TotalCbm = 0m;
        input.TotalGwKg = 0m;
        input.CreatedAt = DateTime.Now;
        _db.ContainerLoads.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPost("generate-from-so")]
    public async Task<ActionResult<ContainerLoad>> GenerateFromSo(GenerateFromSoRequest request)
    {
        if (request.SummaryOrderId <= 0) return BadRequest("SummaryOrderId required");
        var so = await _db.SummaryOrders.FindAsync(request.SummaryOrderId);
        if (so == null) return NotFound();
        var cl = new ContainerLoad
        {
            No = NumberService.NewNo("CL"),
            SummaryOrderId = so.Id,
            CustomerId = so.CustomerId,
            ContainerType = string.IsNullOrWhiteSpace(request.ContainerType) ? "40HQ" : request.ContainerType!,
            LoadDate = request.LoadDate ?? DateTime.Today,
            Status = "draft",
            TotalCartons = 0m,
            TotalCbm = 0m,
            TotalGwKg = 0m,
            Remark = $"兼容创建：来源客户汇总单 {so.No}，请先选择仓库并从库存锁定商品",
            CreatedAt = DateTime.Now
        };
        _db.ContainerLoads.Add(cl);
        await _db.SaveChangesAsync();
        return cl;
    }

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<ContainerLoad>> Copy(long id)
    {
        var source = await _db.ContainerLoads.FindAsync(id);
        if (source == null) return NotFound();
        var copy = new ContainerLoad
        {
            No = NumberService.NewNo("CL"),
            SummaryOrderId = source.SummaryOrderId,
            CustomerId = source.CustomerId,
            WarehouseId = source.WarehouseId,
            ContainerType = source.ContainerType,
            LoadDate = DateTime.Today,
            Status = "draft",
            TotalCbm = 0m,
            TotalGwKg = 0m,
            TotalCartons = 0m,
            Remark = $"复制自 {source.No}，库存未复制，请重新选择并锁定",
            CreatedAt = DateTime.Now
        };
        _db.ContainerLoads.Add(copy);
        await _db.SaveChangesAsync();
        return copy;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ContainerLoad>> Update(long id, ContainerLoad input)
    {
        var entity = await _db.ContainerLoads.FindAsync(id);
        if (entity == null) return NotFound();
        if (entity.Status is "confirmed" or "completed" or "shipment_created")
            throw new BusinessRuleException("CONTAINER_LOCKED", "已确认装柜单不能直接修改");

        var hasActiveReservations = await _db.InventoryReservations
            .AnyAsync(x => x.ContainerLoadId == id && x.Status == "active");
        if (hasActiveReservations &&
            (input.CustomerId != entity.CustomerId || input.WarehouseId != entity.WarehouseId))
        {
            throw new BusinessRuleException("CONTAINER_SOURCE_LOCKED", "释放库存锁定后才能修改客户或仓库");
        }

        entity.CustomerId = input.CustomerId;
        entity.WarehouseId = input.WarehouseId;
        entity.ContainerType = input.ContainerType;
        entity.ContainerNo = input.ContainerNo;
        entity.SealNo = input.SealNo;
        entity.LoadDate = input.LoadDate;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        // 普通保存不得修改状态、锁定时间或到期时间。
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.ContainerLoads.FindAsync(id);
        if (entity == null) return NotFound();
        if (await _db.InventoryReservations.AnyAsync(x => x.ContainerLoadId == id && x.Status == "active"))
            await _reservations.ReleaseAsync(id, "删除装柜草稿，释放库存", CurrentUserId());
        entity.IsDeleted = true;
        entity.Status = "cancelled";
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private long? CurrentUserId()
        => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static (decimal Cbm, decimal Kg) GetCapacity(string? containerType)
    {
        return (containerType ?? "40HQ").ToUpperInvariant() switch
        {
            "20GP" => (28m, 21600m),
            "40GP" => (58m, 26500m),
            "40HQ" => (68m, 26500m),
            "45HQ" => (78m, 28000m),
            _ => (68m, 26500m)
        };
    }
}

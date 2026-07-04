using Futurem.Sourcing.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/business-dashboard")]
public class BusinessDashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    public BusinessDashboardController(AppDbContext db) { _db = db; }

    [HttpGet("summary")]
    public async Task<ActionResult<object>> Summary()
    {
        var receivables = await _db.FinanceRecords.Where(x => x.RecordType == "receivable").ToListAsync();
        var payables = await _db.FinanceRecords.Where(x => x.RecordType == "payable").ToListAsync();
        var containers = await _db.ContainerLoads.ToListAsync();
        var shipments = await _db.Shipments.ToListAsync();
        var po = await _db.PurchaseOrders.ToListAsync();
        var so = await _db.SummaryOrders.ToListAsync();
        var qc = await _db.QcOrders.ToListAsync();

        return new
        {
            masterData = new
            {
                customers = await _db.Customers.CountAsync(),
                suppliers = await _db.Suppliers.CountAsync(),
                products = await _db.Products.CountAsync()
            },
            workflow = new
            {
                rfqs = await _db.Rfqs.CountAsync(),
                customerOrders = await _db.CustomerOrders.CountAsync(),
                purchaseOrders = po.Count,
                summaryOrders = so.Count,
                receivingOrders = await _db.ReceivingOrders.CountAsync(),
                qcOrders = qc.Count,
                containerLoads = containers.Count,
                shipments = shipments.Count
            },
            finance = new
            {
                receivableAmount = receivables.Sum(x => x.Amount),
                receivedAmount = receivables.Sum(x => x.PaidAmount),
                receivableBalance = receivables.Sum(x => x.Amount - x.PaidAmount),
                payableAmount = payables.Sum(x => x.Amount),
                paidAmount = payables.Sum(x => x.PaidAmount),
                payableBalance = payables.Sum(x => x.Amount - x.PaidAmount)
            },
            alerts = new
            {
                unpaidPo = po.Count(x => x.PayStatus != "paid"),
                unpaidSo = so.Count(x => x.ReceivedAmount < x.ReceivableAmount),
                qcFailed = qc.Count(x => x.Result == "failed"),
                containersNotShipped = containers.Count(x => x.Status != "shipment_created"),
                shipmentsNotArrived = shipments.Count(x => x.Status != "arrived" && x.Status != "done")
            }
        };
    }

    [HttpGet("todo")]
    public async Task<ActionResult<object>> Todo()
    {
        var pendingReceivables = await _db.FinanceRecords
            .Where(x => x.RecordType == "receivable" && x.Status != "done")
            .OrderBy(x => x.RecordDate ?? x.CreatedAt)
            .Take(20)
            .ToListAsync();
        var pendingPayables = await _db.FinanceRecords
            .Where(x => x.RecordType == "payable" && x.Status != "done")
            .OrderBy(x => x.RecordDate ?? x.CreatedAt)
            .Take(20)
            .ToListAsync();
        var pendingContainers = await _db.ContainerLoads
            .Where(x => x.Status != "shipment_created")
            .OrderByDescending(x => x.Id)
            .Take(20)
            .ToListAsync();
        var pendingShipments = await _db.Shipments
            .Where(x => x.Status != "arrived" && x.Status != "done")
            .OrderBy(x => x.Eta ?? x.Etd ?? x.CreatedAt)
            .Take(20)
            .ToListAsync();

        return new
        {
            pendingReceivables = pendingReceivables.Select(x => new { x.No, x.CustomerId, x.Currency, x.Amount, x.PaidAmount, balance = x.Amount - x.PaidAmount, x.RecordDate, x.Status }),
            pendingPayables = pendingPayables.Select(x => new { x.No, x.SupplierId, x.Currency, x.Amount, x.PaidAmount, balance = x.Amount - x.PaidAmount, x.RecordDate, x.Status }),
            pendingContainers = pendingContainers.Select(x => new { x.No, x.SummaryOrderId, x.ContainerType, x.LoadDate, x.TotalCbm, x.TotalGwKg, x.Status }),
            pendingShipments = pendingShipments.Select(x => new { x.No, x.ContainerLoadId, x.SummaryOrderId, x.ShipmentMode, x.Carrier, x.Etd, x.Eta, x.Status })
        };
    }
}

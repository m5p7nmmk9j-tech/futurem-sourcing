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
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var receivables = await _db.FinanceRecords.Where(x => x.RecordType == "receivable").ToListAsync();
        var payables = await _db.FinanceRecords.Where(x => x.RecordType == "payable").ToListAsync();
        var expenses = await _db.FinanceRecords.Where(x => x.RecordType == "expense").ToListAsync();
        var incomes = await _db.FinanceRecords.Where(x => x.RecordType == "income").ToListAsync();
        var payments = await _db.Payments.ToListAsync();
        var containers = await _db.ContainerLoads.ToListAsync();
        var shipments = await _db.Shipments.ToListAsync();
        var po = await _db.PurchaseOrders.ToListAsync();
        var so = await _db.SummaryOrders.ToListAsync();
        var qc = await _db.QcOrders.ToListAsync();

        decimal IncomeAmount(DateTime from, DateTime to) => receivables.Where(x => (x.RecordDate ?? x.CreatedAt).Date >= from && (x.RecordDate ?? x.CreatedAt).Date <= to).Sum(x => x.Amount) + incomes.Where(x => (x.RecordDate ?? x.CreatedAt).Date >= from && (x.RecordDate ?? x.CreatedAt).Date <= to).Sum(x => x.Amount);
        decimal CostAmount(DateTime from, DateTime to) => payables.Where(x => (x.RecordDate ?? x.CreatedAt).Date >= from && (x.RecordDate ?? x.CreatedAt).Date <= to).Sum(x => x.Amount) + expenses.Where(x => (x.RecordDate ?? x.CreatedAt).Date >= from && (x.RecordDate ?? x.CreatedAt).Date <= to).Sum(x => x.Amount);

        return new
        {
            masterData = new { customers = await _db.Customers.CountAsync(), suppliers = await _db.Suppliers.CountAsync(), products = await _db.Products.CountAsync() },
            workflow = new { rfqs = await _db.Rfqs.CountAsync(), customerOrders = await _db.CustomerOrders.CountAsync(), purchaseOrders = po.Count, summaryOrders = so.Count, receivingOrders = await _db.ReceivingOrders.CountAsync(), qcOrders = qc.Count, containerLoads = containers.Count, shipments = shipments.Count },
            today = new { rfqs = await _db.Rfqs.CountAsync(x => x.CreatedAt.Date == today), customerOrders = await _db.CustomerOrders.CountAsync(x => x.CreatedAt.Date == today), purchaseOrders = po.Count(x => x.CreatedAt.Date == today), summaryOrders = so.Count(x => x.CreatedAt.Date == today), receivingOrders = await _db.ReceivingOrders.CountAsync(x => x.CreatedAt.Date == today), qcOrders = qc.Count(x => x.CreatedAt.Date == today), containerLoads = containers.Count(x => x.CreatedAt.Date == today), shipments = shipments.Count(x => x.CreatedAt.Date == today), received = payments.Where(x => x.Direction == "receive" && x.PaymentDate.Date == today).Sum(x => x.Amount), paid = payments.Where(x => x.Direction == "pay" && x.PaymentDate.Date == today).Sum(x => x.Amount), profit = IncomeAmount(today, today) - CostAmount(today, today) },
            month = new { sales = so.Where(x => x.OrderDate.HasValue && x.OrderDate.Value.Date >= monthStart).Sum(x => x.ReceivableAmount), purchase = payables.Where(x => (x.RecordDate ?? x.CreatedAt).Date >= monthStart && x.TargetType == "PO").Sum(x => x.Amount), received = payments.Where(x => x.Direction == "receive" && x.PaymentDate.Date >= monthStart).Sum(x => x.Amount), paid = payments.Where(x => x.Direction == "pay" && x.PaymentDate.Date >= monthStart).Sum(x => x.Amount), profit = IncomeAmount(monthStart, today) - CostAmount(monthStart, today) },
            finance = new { receivableAmount = receivables.Sum(x => x.Amount), receivedAmount = receivables.Sum(x => x.PaidAmount), receivableBalance = receivables.Sum(x => x.Amount - x.PaidAmount), payableAmount = payables.Sum(x => x.Amount), paidAmount = payables.Sum(x => x.PaidAmount), payableBalance = payables.Sum(x => x.Amount - x.PaidAmount) },
            alerts = new { unpaidPo = po.Count(x => x.PayStatus != "paid"), unpaidSo = so.Count(x => x.ReceivedAmount < x.ReceivableAmount), qcFailed = qc.Count(x => x.Result == "failed"), containersNotShipped = containers.Count(x => x.Status != "shipment_created"), shipmentsNotArrived = shipments.Count(x => x.Status != "arrived" && x.Status != "done") }
        };
    }

    [HttpGet("todo")]
    public async Task<ActionResult<object>> Todo()
    {
        var pendingReceivables = await _db.FinanceRecords.Where(x => x.RecordType == "receivable" && x.Status != "done").OrderBy(x => x.RecordDate ?? x.CreatedAt).Take(20).ToListAsync();
        var pendingPayables = await _db.FinanceRecords.Where(x => x.RecordType == "payable" && x.Status != "done").OrderBy(x => x.RecordDate ?? x.CreatedAt).Take(20).ToListAsync();
        var pendingContainers = await _db.ContainerLoads.Where(x => x.Status != "shipment_created").OrderByDescending(x => x.Id).Take(20).ToListAsync();
        var pendingShipments = await _db.Shipments.Where(x => x.Status != "arrived" && x.Status != "done").OrderBy(x => x.Eta ?? x.Etd ?? x.CreatedAt).Take(20).ToListAsync();
        return new { pendingReceivables = pendingReceivables.Select(x => new { x.No, x.CustomerId, x.Currency, x.Amount, x.PaidAmount, balance = x.Amount - x.PaidAmount, x.RecordDate, x.Status }), pendingPayables = pendingPayables.Select(x => new { x.No, x.SupplierId, x.Currency, x.Amount, x.PaidAmount, balance = x.Amount - x.PaidAmount, x.RecordDate, x.Status }), pendingContainers = pendingContainers.Select(x => new { x.No, x.SummaryOrderId, x.ContainerType, x.LoadDate, x.TotalCbm, x.TotalGwKg, x.Status }), pendingShipments = pendingShipments.Select(x => new { x.No, x.ContainerLoadId, x.SummaryOrderId, x.ShipmentMode, x.Carrier, x.Etd, x.Eta, x.Status }) };
    }

    [HttpGet("recent")]
    public async Task<ActionResult<object>> Recent()
    {
        var latestPo = await _db.PurchaseOrders.OrderByDescending(x => x.Id).Take(8).Select(x => new { x.Id, x.No, x.SupplierId, x.CustomerId, x.OrderDate, x.Currency, x.Status, x.PayStatus }).ToListAsync();
        var latestSo = await _db.SummaryOrders.OrderByDescending(x => x.Id).Take(8).Select(x => new { x.Id, x.No, x.CustomerId, x.OrderDate, x.Currency, x.ReceivableAmount, x.ReceivedAmount, x.Status }).ToListAsync();
        var latestContainers = await _db.ContainerLoads.OrderByDescending(x => x.Id).Take(8).Select(x => new { x.Id, x.No, x.SummaryOrderId, x.ContainerType, x.LoadDate, x.TotalCbm, x.TotalGwKg, x.Status }).ToListAsync();
        var latestShipments = await _db.Shipments.OrderByDescending(x => x.Id).Take(8).Select(x => new { x.Id, x.No, x.ContainerLoadId, x.SummaryOrderId, x.ShipmentMode, x.Carrier, x.Etd, x.Eta, x.Status }).ToListAsync();
        var latestPayments = await _db.Payments.OrderByDescending(x => x.Id).Take(8).Select(x => new { x.Id, x.No, x.Direction, x.Currency, x.Amount, x.PaymentDate, x.PaymentMethod }).ToListAsync();
        return new { latestPo, latestSo, latestContainers, latestShipments, latestPayments };
    }

    [HttpGet("warnings")]
    public async Task<ActionResult<object>> Warnings()
    {
        var today = DateTime.Today;
        var soon = today.AddDays(7);
        var pos = await _db.PurchaseOrders.Where(x => x.Status != "done" && x.Status != "closed").ToListAsync();
        var receiving = await _db.ReceivingOrders.Where(x => x.Status != "done" && x.Status != "closed").ToListAsync();
        var qcs = await _db.QcOrders.Where(x => x.Status != "done" && x.Result != "passed").ToListAsync();
        var containers = await _db.ContainerLoads.Where(x => x.Status != "shipment_created").ToListAsync();
        var shipments = await _db.Shipments.Where(x => x.Status != "arrived" && x.Status != "done").ToListAsync();
        var receivables = await _db.FinanceRecords.Where(x => x.RecordType == "receivable" && x.Status != "done").ToListAsync();
        var payables = await _db.FinanceRecords.Where(x => x.RecordType == "payable" && x.Status != "done").ToListAsync();

        object Warning(string type, string level, string no, string message, DateTime? date, decimal? amount = null) => new { type, level, no, message, date, amount };
        int Days(DateTime? date) => date.HasValue ? (date.Value.Date - today).Days : 9999;

        var poDue = pos.Where(x => x.ExpectedDeliveryDate.HasValue && x.ExpectedDeliveryDate.Value.Date <= soon).Select(x => Warning("PO交期", Days(x.ExpectedDeliveryDate) < 0 ? "danger" : "warning", x.No, Days(x.ExpectedDeliveryDate) < 0 ? "采购订单已超过预计交期" : "采购订单即将到交期", x.ExpectedDeliveryDate));
        var receivingOverdue = receiving.Where(x => !x.ReceiveDate.HasValue && x.CreatedAt.Date <= today.AddDays(-3)).Select(x => Warning("收货", "warning", x.No, "收货单创建超过3天未完成", x.CreatedAt));
        var qcPending = qcs.Where(x => !x.QcDate.HasValue || x.QcDate.Value.Date <= soon).Select(x => Warning("QC", x.Result == "failed" ? "danger" : "warning", x.No, x.Result == "failed" ? "QC异常未处理" : "QC待完成", x.QcDate));
        var containerPending = containers.Where(x => !x.LoadDate.HasValue || x.LoadDate.Value.Date <= soon).Select(x => Warning("装柜", Days(x.LoadDate) < 0 ? "danger" : "warning", x.No, Days(x.LoadDate) < 0 ? "装柜日期已过仍未出运" : "装柜后未生成出运", x.LoadDate));
        var etdEta = shipments.Where(x => (x.Etd.HasValue && x.Etd.Value.Date <= soon) || (x.Eta.HasValue && x.Eta.Value.Date <= soon)).Select(x => Warning("出运", (Days(x.Etd) < 0 || Days(x.Eta) < 0) ? "danger" : "warning", x.No, (x.Eta.HasValue && x.Eta.Value.Date <= soon) ? "ETA即将到港或已超期" : "ETD即将到期或已超期", x.Eta ?? x.Etd));
        var ar = receivables.Where(x => (today - (x.RecordDate ?? x.CreatedAt).Date).Days >= 30).Select(x => Warning("应收", (today - (x.RecordDate ?? x.CreatedAt).Date).Days >= 60 ? "danger" : "warning", x.No, "应收账款超过30天未结清", x.RecordDate ?? x.CreatedAt, x.Amount - x.PaidAmount));
        var ap = payables.Where(x => (today - (x.RecordDate ?? x.CreatedAt).Date).Days >= 30).Select(x => Warning("应付", (today - (x.RecordDate ?? x.CreatedAt).Date).Days >= 60 ? "danger" : "warning", x.No, "应付账款超过30天未结清", x.RecordDate ?? x.CreatedAt, x.Amount - x.PaidAmount));
        var list = poDue.Concat(receivingOverdue).Concat(qcPending).Concat(containerPending).Concat(etdEta).Concat(ar).Concat(ap).ToList();
        return new { total = list.Count, danger = list.Count(x => (string)x.GetType().GetProperty("level")!.GetValue(x)! == "danger"), warning = list.Count(x => (string)x.GetType().GetProperty("level")!.GetValue(x)! == "warning"), items = list.Take(100) };
    }
}

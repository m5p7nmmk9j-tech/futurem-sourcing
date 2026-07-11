using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public record ShipmentMeasurementResult(decimal Cbm, decimal GrossWeightKg, decimal NetWeightKg);

public class ShipmentMeasurementService
{
    private readonly AppDbContext _db;

    public ShipmentMeasurementService(AppDbContext db) => _db = db;

    public async Task<ShipmentMeasurementResult> CalculateAsync(long shipmentId)
    {
        var lines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == "SHP" && x.DocumentId == shipmentId)
            .ToListAsync();

        return new ShipmentMeasurementResult(
            FinanceBalanceService.Round2(lines.Sum(x => x.TotalCbm)),
            FinanceBalanceService.Round2(lines.Sum(x => x.TotalGwKg)),
            FinanceBalanceService.Round2(lines.Sum(x => x.TotalNwKg)));
    }

    public async Task<Shipment> RecalculateAsync(long shipmentId, bool overwriteFinalValues)
    {
        var shipment = await _db.Shipments.FindAsync(shipmentId);
        if (shipment == null) throw new KeyNotFoundException("Shipment not found");

        var result = await CalculateAsync(shipmentId);
        shipment.CalculatedTotalCbm = result.Cbm;
        shipment.CalculatedGrossWeightKg = result.GrossWeightKg;
        shipment.CalculatedNetWeightKg = result.NetWeightKg;

        if (overwriteFinalValues || (shipment.FinalTotalCbm == 0m && shipment.FinalGrossWeightKg == 0m && shipment.FinalNetWeightKg == 0m))
        {
            shipment.FinalTotalCbm = result.Cbm;
            shipment.FinalGrossWeightKg = result.GrossWeightKg;
            shipment.FinalNetWeightKg = result.NetWeightKg;
        }

        shipment.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return shipment;
    }
}

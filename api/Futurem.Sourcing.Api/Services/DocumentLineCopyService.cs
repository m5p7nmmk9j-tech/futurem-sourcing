using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Services;

public static class DocumentLineCopyService
{
    public static async Task CopyAsync(AppDbContext db, string fromType, long fromId, string toType, long toId)
    {
        var sourceLines = await db.DocumentLines
            .Where(x => x.DocumentType == fromType && x.DocumentId == fromId)
            .OrderBy(x => x.SortNo)
            .ThenBy(x => x.Id)
            .ToListAsync();

        foreach (var line in sourceLines)
        {
            db.DocumentLines.Add(new DocumentLine
            {
                DocumentType = toType,
                DocumentId = toId,
                ProductId = line.ProductId,
                Sku = line.Sku,
                ProductName = line.ProductName,
                Unit = line.Unit,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                Amount = line.Amount,
                CartonQty = line.CartonQty,
                Cartons = line.Cartons,
                CartonLengthCm = line.CartonLengthCm,
                CartonWidthCm = line.CartonWidthCm,
                CartonHeightCm = line.CartonHeightCm,
                CartonCbm = line.CartonCbm,
                TotalCbm = line.TotalCbm,
                CartonGwKg = line.CartonGwKg,
                TotalGwKg = line.TotalGwKg,
                CartonNwKg = line.CartonNwKg,
                TotalNwKg = line.TotalNwKg,
                SupplierItemNo = line.SupplierItemNo,
                CustomerItemNo = line.CustomerItemNo,
                Remark = line.Remark,
                SortNo = line.SortNo,
                CreatedAt = DateTime.Now
            });
        }
    }
}

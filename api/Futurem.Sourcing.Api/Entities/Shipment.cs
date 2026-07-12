using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class Shipment : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public long? ContainerLoadId { get; set; }
    public long? SummaryOrderId { get; set; }
    public long? CustomerId { get; set; }
    public long? WarehouseId { get; set; }
    public string? ContainerType { get; set; }
    public string? ContainerNo { get; set; }
    public string? SealNo { get; set; }
    public string ShipmentMode { get; set; } = "SEA";
    public string? Carrier { get; set; }
    public string? VesselVoyage { get; set; }
    public string? BillOfLadingNo { get; set; }
    public string? DeparturePort { get; set; }
    public string? DestinationPort { get; set; }
    public DateTime? Etd { get; set; }
    public DateTime? Eta { get; set; }
    public string Status { get; set; } = "draft";
    public string Currency { get; set; } = "RMB";

    [Column(TypeName = "decimal(18,2)")]
    public decimal CalculatedTotalCbm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalTotalCbm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CalculatedGrossWeightKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalGrossWeightKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CalculatedNetWeightKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalNetWeightKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpenseTotal { get; set; }

    public string FinanceSyncStatus { get; set; } = "not_synced";
    public string? FinanceSyncMessage { get; set; }
    public DateTime? FinanceSyncedAt { get; set; }
}

using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class SummaryOrder : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public long? BuyingTripId { get; set; }
    public long CustomerId { get; set; }
    public DateTime? OrderDate { get; set; }
    public string Currency { get; set; } = "RMB";
    public string Status { get; set; } = "draft";

    [Column(TypeName = "decimal(18,4)")]
    public decimal GoodsAmount { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal CommissionFee { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal WarehouseFee { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal LoadingFee { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal LogisticsFee { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal OtherFee { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal ReceivableAmount { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal ReceivedAmount { get; set; }
}

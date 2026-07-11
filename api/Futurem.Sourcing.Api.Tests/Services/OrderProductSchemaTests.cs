using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class OrderProductSchemaTests
{
    [Fact]
    public void OrderProduct_HasRequiredSnapshotFields()
    {
        var names = typeof(OrderProduct).GetProperties().Select(x => x.Name).ToHashSet();
        Assert.Contains(nameof(OrderProduct.CustomerBarcode), names);
        Assert.Contains(nameof(OrderProduct.PurchaseUnitPrice), names);
        Assert.Contains(nameof(OrderProduct.SalesUnitPrice), names);
        Assert.Contains(nameof(OrderProduct.ImporterSnapshotJson), names);
        Assert.Contains(nameof(OrderProduct.LabelTemplateSnapshotJson), names);
        Assert.Contains(nameof(OrderProduct.MarkTemplateSnapshotJson), names);
    }

    [Fact]
    public void CustomerOrder_HasOneImporterAndTwoTemplateSelections()
    {
        var names = typeof(CustomerOrder).GetProperties().Select(x => x.Name).ToHashSet();
        Assert.Contains(nameof(CustomerOrder.ImporterProfileId), names);
        Assert.Contains(nameof(CustomerOrder.LabelTemplateId), names);
        Assert.Contains(nameof(CustomerOrder.MarkTemplateId), names);
        Assert.Contains(nameof(CustomerOrder.ConfirmedAt), names);
    }

    [Fact]
    public void DocumentLine_LinksOrderProductAndPriceSnapshots()
    {
        var names = typeof(DocumentLine).GetProperties().Select(x => x.Name).ToHashSet();
        Assert.Contains(nameof(DocumentLine.OrderProductId), names);
        Assert.Contains(nameof(DocumentLine.SourceDocumentLineId), names);
        Assert.Contains(nameof(DocumentLine.PurchaseUnitPriceSnapshot), names);
        Assert.Contains(nameof(DocumentLine.SalesUnitPriceSnapshot), names);
        Assert.Contains(nameof(DocumentLine.InventoryLotId), names);
    }

    [Fact]
    public void Model_HasCustomerBarcodeAndMainImageIndexes()
    {
        using var db = TestDbFactory.Create();
        var orderProduct = db.Model.FindEntityType(typeof(OrderProduct));
        Assert.NotNull(orderProduct);
        Assert.Contains(orderProduct!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(new[]
            {
                nameof(OrderProduct.CustomerId),
                nameof(OrderProduct.CustomerBarcode)
            }));

        var image = db.Model.FindEntityType(typeof(OrderProductImage));
        Assert.NotNull(image);
        Assert.Contains(image!.GetIndexes(), index =>
            index.Properties.Select(x => x.Name).SequenceEqual(new[]
            {
                nameof(OrderProductImage.OrderProductId),
                nameof(OrderProductImage.ImageType)
            }));
    }

    [Fact]
    public void AuditCorrelation_IsBoundedForMySqlIndexing()
    {
        using var db = TestDbFactory.Create();
        var audit = db.Model.FindEntityType(typeof(AuditLog));
        Assert.NotNull(audit);
        Assert.Equal(120, audit!.FindProperty(nameof(AuditLog.CorrelationId))!.GetMaxLength());
        Assert.Equal(80, audit.FindProperty(nameof(AuditLog.SourceDocumentType))!.GetMaxLength());
    }

    [Fact]
    public void PrintTemplate_SupportsCustomerVisualLabelAndMarkLayouts()
    {
        var names = typeof(PrintTemplate).GetProperties().Select(x => x.Name).ToHashSet();
        Assert.Contains(nameof(PrintTemplate.TemplateType), names);
        Assert.Contains(nameof(PrintTemplate.CustomerId), names);
        Assert.Contains(nameof(PrintTemplate.DesignerMode), names);
        Assert.Contains(nameof(PrintTemplate.LayoutJson), names);
        Assert.Contains(nameof(PrintTemplate.PaperWidthMm), names);
        Assert.Contains(nameof(PrintTemplate.PaperHeightMm), names);
    }
}

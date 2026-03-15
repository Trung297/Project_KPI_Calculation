using System;
using System.Text.Json.Serialization;

namespace InventoryKpiSystem.Core.Entities;

// Sử dụng readonly record struct để tối ưu bộ nhớ cho Purchase Orders
public readonly record struct PurchaseOrder
{
    [JsonPropertyName("OrderId")]
    public string OrderId { get; init; }

    [JsonPropertyName("ProductId")]
    public string ProductId { get; init; }

    // Đồng bộ tuyệt đối key trong JSON và tên biến trong C#
    [JsonPropertyName("QuantityPurchased")]
    public int QuantityPurchased { get; init; }

    [JsonPropertyName("UnitCost")]
    public decimal UnitCost { get; init; }

    [JsonPropertyName("PurchaseDate")]
    public DateTimeOffset PurchaseDate { get; init; }
}

// Sử dụng readonly record struct để tối ưu bộ nhớ cho Sales Invoices
public readonly record struct SalesInvoice
{
    [JsonPropertyName("InvoiceId")]
    public string InvoiceId { get; init; }

    [JsonPropertyName("ProductId")]
    public string ProductId { get; init; }

    // Đồng bộ tuyệt đối key trong JSON và tên biến trong C#
    [JsonPropertyName("QuantitySold")]
    public int QuantitySold { get; init; }

    [JsonPropertyName("UnitSellingPrice")]
    public decimal UnitSellingPrice { get; init; }

    [JsonPropertyName("InvoiceDate")]
    public DateTimeOffset InvoiceDate { get; init; }
}
namespace Backend.Domain;

/// <summary>
/// supplier_price 表内部 POCO，不在 OpenAPI 生成的 Models/ 下。
/// </summary>
public class SupplierPrice
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public long MaterialId { get; set; }
    public decimal Price { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}

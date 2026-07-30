using Org.OpenAPITools.Models;

namespace Backend.Services.Interfaces;

/// <summary>
/// A 模块提供给 B 模块的窄接口：递归展开 BOM 树。
/// </summary>
public interface IBomExpansionService
{
    List<BomExpansionNode> Expand(long materialId, long versionId);
}

public sealed record BomExpansionNode(
    long MaterialId,
    int Level,
    long? ParentMaterialId,
    decimal Quantity,
    decimal LossRate
);

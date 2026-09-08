using Org.OpenAPITools.Models;

namespace Backend.Services;

public interface IProductionLineService
{
    ProductionResourceResult<ProductionResourcePage<LineType>> ListLineTypes(
        int page,
        int pageSize,
        string? typeName);

    ProductionResourceResult<LineType> SaveLineType(LineTypeSaveRequest request);

    ProductionResourceResult<ProductionResourcePage<ProductionLine>> ListLines(
        int page,
        int pageSize,
        long? typeId,
        ProductionLineRunStatus? status);

    ProductionResourceResult<ProductionLine> AddLine(ProductionLineCreateRequest request);

    ProductionResourceResult<ProductionLine> UpdateLine(ProductionLineUpdateRequest request);

    ProductionResourceResult<FaultRecord> ReportFault(
        FaultRecordCreateRequest request,
        CurrentUser currentUser);

    ProductionResourceResult<FaultRecord> UpdateFault(
        FaultRecordUpdateRequest request,
        CurrentUser currentUser);

    ProductionResourceResult<ProductionResourcePage<FaultRecord>> ListFaults(
        int page,
        int pageSize,
        long? lineId,
        FaultStatus? status);

    ProductionResourceResult<ProductionLineStatus> UpdateLineStatus(
        ProductionLineStatusUpdateRequest request,
        CurrentUser currentUser);
}

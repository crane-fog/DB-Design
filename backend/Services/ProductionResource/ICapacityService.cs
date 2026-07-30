using Org.OpenAPITools.Models;

namespace Backend.Services;

public interface ICapacityService
{
    ProductionResourceResult<ProductionResourcePage<CapacityConfig>> ListConfigs(
        int page,
        int pageSize,
        long? materialId,
        long? typeId);

    ProductionResourceResult<CapacityConfig> SaveConfig(CapacityConfigSaveRequest request);

    ProductionResourceResult<ProductionResourcePage<ProductionCalendar>> ListCalendars(
        int page,
        int pageSize,
        long? lineId,
        DateOnly? startDate,
        DateOnly? endDate,
        long? configId);

    ProductionResourceResult<ProductionCalendar> SaveCalendar(ProductionCalendarSaveRequest request);

    ProductionResourceResult<object> DeleteCalendar(ProductionCalendarDeleteRequest request);

    ProductionResourceResult<ProductionCapacityEstimateResult> Estimate(
        ProductionCapacityEstimateRequest request);

    ProductionResourceResult<CapacityDetection> RunDetection(CapacityDetectionRunRequest request);

    ProductionResourceResult<CapacityBalance> SaveBalance(
        CapacityBalanceSaveRequest request,
        CurrentUser currentUser);
}

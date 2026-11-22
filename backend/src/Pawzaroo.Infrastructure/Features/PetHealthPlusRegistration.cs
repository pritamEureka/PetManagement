using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pawzaroo.Application.Modules.Pets.HealthPlus;
using Pawzaroo.Infrastructure.Modules.Pets;

namespace Pawzaroo.Infrastructure.Features;

public static class PetHealthPlusRegistration
{
    public static IServiceCollection AddPetHealthPlusFeatures(this IServiceCollection s)
    {
        s.AddScoped<ICarePlanService, CarePlanService>();
        s.AddScoped<IExposureService, ExposureService>();
        s.AddScoped<IPetHealthRecordsService, PetHealthRecordsService>();
        s.AddScoped<IHealthRecordIntegrityService, HealthRecordIntegrityService>();
        s.AddScoped<IVaccinationCertificateService, VaccinationCertificateService>();
        s.AddScoped<IFhirExportService, FhirExportService>();
        s.AddScoped<IPetInsightService, PetInsightService>();
        s.AddScoped<ILivestockHealthService, LivestockHealthService>();
        // No OCR engine ships by default; a real implementation registered earlier wins.
        s.TryAddSingleton<IDocumentOcr, NoOpDocumentOcr>();
        return s;
    }
}

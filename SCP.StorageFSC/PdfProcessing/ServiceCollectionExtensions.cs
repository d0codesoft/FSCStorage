using scp.filestorage.PdfProcessing.Interfaces;

namespace scp.filestorage.PdfProcessing
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPdfProcessingServices(this IServiceCollection services)
        {
            services.AddSingleton<IPageImageProcessingService, ImageSharpPageImageProcessingService>();
            services.AddSingleton<IPdfAnalysisService, DocnetPdfAnalysisService>();
            return services;
        }
    }
}

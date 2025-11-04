using Audicob.Models.ViewModels.Metricas;
using System;
using System.Threading.Tasks;

namespace Audicob.Services
{
    public interface IMetricasService
    {
        Task<MetricaDesempenoViewModel> GenerarMetricasDesempenoAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<byte[]> GenerarReportePdfAsync(MetricaDesempenoViewModel metricas);
        Task<byte[]> GenerarReporteExcelAsync(MetricaDesempenoViewModel metricas);
    }
}
using Audicob.Data;
using Audicob.Models.ViewModels.Metricas;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Audicob.Services
{
    public class MetricasService : IMetricasService
    {
        private readonly ApplicationDbContext _context;

        public MetricasService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MetricaDesempenoViewModel> GenerarMetricasDesempenoAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var metricas = new MetricaDesempenoViewModel
            {
                FechaInicio = fechaInicio,
                FechaFin = fechaFin
            };

            // Datos de ejemplo para el prototipo
            metricas.MetricasAsesores = new List<MetricaAsesorViewModel>
            {
                new MetricaAsesorViewModel { NombreAsesor = "María", CobrosExitosos = 24, MontoRecuperado = 18500, Eficiencia = 85.2m, TotalClientes = 28, DeudaTotal = 32000, VariacionMonto = "+8%", VariacionDeuda = "-5%" },
                new MetricaAsesorViewModel { NombreAsesor = "Carlos", CobrosExitosos = 18, MontoRecuperado = 15200, Eficiencia = 78.5m, TotalClientes = 23, DeudaTotal = 28500, VariacionMonto = "+12%", VariacionDeuda = "-3%" },
                new MetricaAsesorViewModel { NombreAsesor = "Ana", CobrosExitosos = 22, MontoRecuperado = 16800, Eficiencia = 82.1m, TotalClientes = 27, DeudaTotal = 30500, VariacionMonto = "+15%", VariacionDeuda = "-7%" },
                new MetricaAsesorViewModel { NombreAsesor = "Pedro", CobrosExitosos = 16, MontoRecuperado = 12550, Eficiencia = 72.3m, TotalClientes = 25, DeudaTotal = 34600, VariacionMonto = "+5%", VariacionDeuda = "+2%" }
            };

            metricas.ResumenGeneral = new ResumenGeneralViewModel
            {
                TotalClientes = 103,
                VariacionClientes = "+5%",
                MontoRecuperado = 63050.5m,
                VariacionMonto = "+12%",
                EficienciaGeneral = 73.2m,
                DeudaTotal = 125600.75m,
                VariacionDeuda = "-8%"
            };

            metricas.DistribucionEstados = new DistribucionEstadosViewModel
            {
                AlDia = 24,
                EnGestion = 15,
                Vencidos = 31,
                Morosos = 27,
                Judicial = 3
            };

            return await Task.FromResult(metricas);
        }

        public async Task<byte[]> GenerarReportePdfAsync(MetricaDesempenoViewModel metricas)
        {
            // Implementación básica de PDF
            var pdfContent = $"REPORTE DE MÉTRICAS - {DateTime.Now:dd/MM/yyyy}\n\n";
            pdfContent += $"Total Clientes: {metricas.ResumenGeneral.TotalClientes}\n";
            pdfContent += $"Monto Recuperado: S/ {metricas.ResumenGeneral.MontoRecuperado:N2}\n";
            pdfContent += $"Eficiencia General: {metricas.ResumenGeneral.EficienciaGeneral}%\n\n";
            
            foreach (var asesor in metricas.MetricasAsesores)
            {
                pdfContent += $"{asesor.NombreAsesor}: {asesor.CobrosExitosos} cobros - S/ {asesor.MontoRecuperado:N2}\n";
            }

            return await Task.FromResult(System.Text.Encoding.UTF8.GetBytes(pdfContent));
        }

        public async Task<byte[]> GenerarReporteExcelAsync(MetricaDesempenoViewModel metricas)
        {
            // Implementación básica de Excel
            var excelContent = "Asesor,Cobros Exitosos,Monto Recuperado,Eficiencia\n";
            
            foreach (var asesor in metricas.MetricasAsesores)
            {
                excelContent += $"{asesor.NombreAsesor},{asesor.CobrosExitosos},{asesor.MontoRecuperado},{asesor.Eficiencia}\n";
            }

            return await Task.FromResult(System.Text.Encoding.UTF8.GetBytes(excelContent));
        }
    }
}
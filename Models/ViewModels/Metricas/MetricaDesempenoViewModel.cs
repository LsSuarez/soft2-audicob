using System;
using System.Collections.Generic;

namespace Audicob.Models.ViewModels.Metricas
{
    public class MetricaDesempenoViewModel
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public List<MetricaAsesorViewModel> MetricasAsesores { get; set; } = new List<MetricaAsesorViewModel>();
        public ResumenGeneralViewModel ResumenGeneral { get; set; } = new ResumenGeneralViewModel();
        public DistribucionEstadosViewModel DistribucionEstados { get; set; } = new DistribucionEstadosViewModel();
    }

    public class MetricaAsesorViewModel
    {
        public string NombreAsesor { get; set; }
        public int CobrosExitosos { get; set; }
        public decimal MontoRecuperado { get; set; }
        public decimal Eficiencia { get; set; }
        public int TotalClientes { get; set; }
        public decimal DeudaTotal { get; set; }
        public string VariacionMonto { get; set; }
        public string VariacionDeuda { get; set; }
    }

    public class ResumenGeneralViewModel
    {
        public int TotalClientes { get; set; }
        public string VariacionClientes { get; set; }
        public decimal MontoRecuperado { get; set; }
        public string VariacionMonto { get; set; }
        public decimal EficienciaGeneral { get; set; }
        public decimal DeudaTotal { get; set; }
        public string VariacionDeuda { get; set; }
    }

    public class DistribucionEstadosViewModel
    {
        public decimal AlDia { get; set; }
        public decimal EnGestion { get; set; }
        public decimal Vencidos { get; set; }
        public decimal Morosos { get; set; }
        public decimal Judicial { get; set; }
    }
}
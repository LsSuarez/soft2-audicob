using System.ComponentModel.DataAnnotations;

namespace Audicob.Models.ViewModels.Supervisor
{
    public class ReporteMoraViewModel
    {
        // Estadísticas generales
        public int TotalClientes { get; set; }
        public int ClientesAlDia { get; set; }
        public int ClientesMoraTemrpana { get; set; }
        public int ClientesMoraModerada { get; set; }
        public int ClientesMoraGrave { get; set; }
        public int ClientesMoraCritica { get; set; }
        
        // Montos
        public decimal MontoTotalDeuda { get; set; }
        public decimal MontoAlDia { get; set; }
        public decimal MontoMoraTemplana { get; set; }
        public decimal MontoMoraModerada { get; set; }
        public decimal MontoMoraGrave { get; set; }
        public decimal MontoMoraCritica { get; set; }
        
        // Listas de datos
        public List<ClienteReporteMora> ClientesMayorDeuda { get; set; } = new List<ClienteReporteMora>();
        public List<EvolucionMoraMensual> EvolucionMensual { get; set; } = new List<EvolucionMoraMensual>();
        
        // Propiedades calculadas
        public decimal PorcentajeClientesEnMora => TotalClientes > 0 
            ? (decimal)(TotalClientes - ClientesAlDia) / TotalClientes * 100 
            : 0;
            
        public decimal PorcentajeMontoEnMora => MontoTotalDeuda > 0 
            ? (MontoTotalDeuda - MontoAlDia) / MontoTotalDeuda * 100 
            : 0;
    }

    public class ClienteReporteMora
    {
        public string Nombre { get; set; } = string.Empty;
        public string Documento { get; set; } = string.Empty;
        public string EstadoMora { get; set; } = string.Empty;
        public decimal DeudaTotal { get; set; }
        public int DiasEnMora { get; set; }
        public decimal IngresosMensuales { get; set; }
        
        public string ColorEstado => EstadoMora switch
        {
            "Al día" => "success",
            "Temprana" => "warning",
            "Moderada" => "info",
            "Grave" => "danger",
            "Crítica" => "dark",
            _ => "secondary"
        };
    }

    public class EvolucionMoraMensual
    {
        public string Mes { get; set; } = string.Empty;
        public string NombreMes { get; set; } = string.Empty;
        public int TotalClientes { get; set; }
        public int ClientesEnMora { get; set; }
        public decimal MontoTotalMora { get; set; }
        public decimal PorcentajeMora { get; set; }
    }
}
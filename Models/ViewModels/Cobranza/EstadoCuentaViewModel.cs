using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Audicob.Models.ViewModels.Cobranza
{
    public class EstadoCuentaViewModel
    {
        // Información del cliente
        public string Cliente { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        
        // Resumen de deuda (propiedades existentes)
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal TotalDeuda { get; set; }
        
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Capital { get; set; }
        
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Intereses { get; set; }
        
        // Nuevas propiedades para el estado de cuenta completo
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal SaldoAnterior { get; set; }
        
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal TotalAbonos { get; set; }
        
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal TotalCargos { get; set; }
        
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal SaldoActual { get; set; }
        
        // Historial de transacciones
        public List<TransaccionViewModel> HistorialTransacciones { get; set; } = new();
        
        // Propiedades para filtros
        public string? SearchTerm { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
    }
}
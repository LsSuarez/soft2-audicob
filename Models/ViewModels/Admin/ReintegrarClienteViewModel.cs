using System;
using System.Collections.Generic;

namespace Audicob.Models.ViewModels.Admin
{
    public class ReintegrarClienteViewModel
    {
        public string Documento { get; set; } = string.Empty;
        public string? Nombre { get; set; }
        public string? Estado { get; set; }
        public bool ClienteEncontrado { get; set; }

        public List<HistorialCreditoItem> Historial { get; set; } = new();
        // 🔹 NUEVA PROPIEDAD
        public List<HistorialAuditoria> Auditorias { get; set; } = new();

        public class HistorialCreditoItem
        {
            public decimal MontoOperacion { get; set; }
            public string TipoOperacion { get; set; } = string.Empty;
            public DateTime FechaOperacion { get; set; }
            public string Observaciones { get; set; } = string.Empty;
        }
    }
}

using System;

namespace Audicob.Models
{
    public class PagoPendiente
    {
        public int Id { get; set; }
        public string Descripcion { get; set; }
        public decimal Monto { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public string? MetodoPago { get; set; }
        public int ClienteId { get; set; }      // Clave foránea hacia Cliente
        public Cliente Cliente { get; set; } 

    }
}
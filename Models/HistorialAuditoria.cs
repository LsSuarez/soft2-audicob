using System;
using System.ComponentModel.DataAnnotations;

namespace Audicob.Models
{
    public class HistorialAuditoria
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string NombreCliente { get; set; }

        [Required]
        public string DniCliente { get; set; }

        [Required]
        public string Accion { get; set; } // Ejemplo: "Reintegrado por el Gerente General"

        [Required]
        public DateTime FechaAccion { get; set; }
    }
}

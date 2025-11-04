using System;
using System.ComponentModel.DataAnnotations;

namespace Audicob.Models
{
    public class AlertaCobranza
    {
        public int Id { get; set; }

        [Required]
        public int ClienteId { get; set; }

        // Propiedad de navegación, opcional para POST
        public Cliente Cliente { get; set; }

        [Required]
        public decimal Monto { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime FechaLimite { get; set; }

        public string Estado { get; set; } = "Pendiente"; // Pendiente | Atendida
        public string Prioridad { get; set; } = "Normal"; // Baja | Normal | Alta
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}

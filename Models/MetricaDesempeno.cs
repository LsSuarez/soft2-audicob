using System;
using System.ComponentModel.DataAnnotations;

namespace Audicob.Models
{
    public class MetricaDesempeno
    {
        public int Id { get; set; }
        
        [Required]
        public int AsesorId { get; set; }
        
        [Required]
        public DateTime Fecha { get; set; }
        
        public int CobrosExitosos { get; set; }
        public decimal MontoRecuperado { get; set; }
        public decimal Eficiencia { get; set; }
        public int TotalClientes { get; set; }
        public decimal DeudaTotal { get; set; }
        
        // Navigation properties
        public virtual Asesor Asesor { get; set; }
    }

    public class EstadoCartera
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public decimal AlDia { get; set; }
        public decimal EnGestion { get; set; }
        public decimal Vencidos { get; set; }
        public decimal Morosos { get; set; }
        public decimal Judicial { get; set; }
    }

    public class Asesor
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public bool Activo { get; set; }
    }
}
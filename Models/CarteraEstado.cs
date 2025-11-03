// Models/CarteraEstado.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Audicob.Models
{
    public class CarteraEstado
    {
        public int Id { get; set; }
        
        [Required]
        public int ClienteId { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Estado { get; set; } = "vigente"; // vigente, morosa, castigada
        
        [StringLength(500)]
        public string? Comentario { get; set; }
        
        public DateTime FechaModificacion { get; set; }
        
        public string? UsuarioModificacion { get; set; }
        
        // Navigation property
        [ForeignKey("ClienteId")]
        public virtual Cliente? Cliente { get; set; }
    }

    public class CarteraEstadoUpdateDto
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "El estado es requerido")]
        [Display(Name = "Estado de Cartera")]
        public string Estado { get; set; } = string.Empty;
        
        [Display(Name = "Comentario")]
        public string? Comentario { get; set; }
        
        public int ClienteId { get; set; }
        public string? ClienteNombre { get; set; }
        public string? EstadoActual { get; set; }
    }
}
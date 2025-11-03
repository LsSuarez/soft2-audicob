using System.ComponentModel.DataAnnotations;

namespace Audicob.Models.ViewModels.Supervisor
{
    public class CarteraEstadoUpdateDto
    {
        public int Id { get; set; }
        
        [Required]
        public int ClienteId { get; set; }
        
        public string ClienteNombre { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Estado { get; set; }
        
        public string EstadoActual { get; set; }
        
        [StringLength(500)]
        public string Comentario { get; set; }
    }
}
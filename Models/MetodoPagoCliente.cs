using System.ComponentModel.DataAnnotations;

namespace Audicob.Models
{
    public class MetodoPagoCliente
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }  // ID del usuario autenticado

        [Required]
        [Display(Name = "Método de Pago")]
        public string Metodo { get; set; }
    }
}

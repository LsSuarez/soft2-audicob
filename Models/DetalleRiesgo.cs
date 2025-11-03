using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Audicob.Models
{
    public class DetalleRiesgo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("PerfilCliente")]
        public int PerfilClienteId { get; set; }

        public string Elemento { get; set; }
        public string Valor { get; set; }
        public string Comentario { get; set; }

        // Relación con PerfilCliente
        public PerfilCliente PerfilCliente { get; set; }
    }
}

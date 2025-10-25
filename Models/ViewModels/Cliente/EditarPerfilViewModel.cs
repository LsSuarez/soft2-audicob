using System.ComponentModel.DataAnnotations;

namespace Audicob.Models.ViewModels.Cliente
{
    public class EditarPerfilViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        public string Telefono { get; set; }

        [Required(ErrorMessage = "El correo es obligatorio")]
        public string Correo { get; set; }

        [Required(ErrorMessage = "La dirección es obligatoria")]
        public string Direccion { get; set; }
    }
}

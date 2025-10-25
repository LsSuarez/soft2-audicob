using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;


namespace Audicob.Models
{
    public class PerfilCliente
    {
        public int Id { get; set; }

        [Required] // Solo los que quieres que se editen
        public string Nombre { get; set; }

        [Required]
        public string Telefono { get; set; }

        [Required]
        public string Correo { get; set; }

        [Required]
        public string Direccion { get; set; }

        public string DocumentoIdentidad { get; set; } // OPCIONAL
        public DateTime FechaRegistro { get; set; }

        [Required]
        public string UserId { get; set; }

        [BindNever] // Esto evita que ASP.NET Core intente llenar User en el POST
        public ApplicationUser User { get; set; }
    }


}
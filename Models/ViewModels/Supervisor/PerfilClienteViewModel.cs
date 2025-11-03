using Audicob.Models;
using System.Collections.Generic;

namespace Audicob.Models.ViewModels.Supervisor
{
    public class PerfilClienteViewModel
    {
        public Models.Cliente ClienteInfo { get; set; }
        public List<Transaccion> TransaccionesRecientes { get; set; } = new List<Transaccion>();
        public decimal TotalPagos { get; set; }
        public int PagosValidados { get; set; }
        public int PagosPendientes { get; set; }
    }
}
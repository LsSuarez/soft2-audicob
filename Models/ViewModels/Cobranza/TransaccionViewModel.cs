using System.ComponentModel.DataAnnotations;

namespace Audicob.Models.ViewModels.Cobranza
{
    public class TransaccionViewModel
    {
        public int Id { get; set; }
        public string Fecha { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Concepto { get; set; } = string.Empty;
        
        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Monto { get; set; }
        
        public string Estado { get; set; } = string.Empty;
    }
}
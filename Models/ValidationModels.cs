using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Audicob.Models
{
    public class PaymentValidation
    {
        public int Id { get; set; }
        
        [Required]
        public string ClientId { get; set; }
        
        [Required]
        public string VoucherNumber { get; set; }
        
        [Required]
        public decimal PaymentAmount { get; set; }
        
        [Required]
        public DateTime PaymentDate { get; set; }
        
        public ValidationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SynchronizedAt { get; set; }
        public string ValidationMessage { get; set; }
        
        // Relación con Cliente
        public int? ClienteId { get; set; }
        public Cliente Cliente { get; set; }
    }

    public enum ValidationStatus
    {
        Pending,
        Validated,
        Synchronized,
        Error
    }

    public class ValidationMetrics
    {
        public int TotalRecords { get; set; }
        public int ValidRecords { get; set; }
        public int ErrorRecords { get; set; }
        public decimal ValidPercentage => TotalRecords > 0 ? (ValidRecords * 100m) / TotalRecords : 0;
        public decimal ErrorPercentage => TotalRecords > 0 ? (ErrorRecords * 100m) / TotalRecords : 0;
        public int PendingAlerts { get; set; }
        public int PendientesSincronizacion { get; set; }

        public int ResolvedAlerts { get; set; }
        public string LastAlert { get; set; }
        
        // Métricas basadas en Clientes
        public int TotalClientes { get; set; }
        public int ClientesAlDia { get; set; }
        public int ClientesEnMora { get; set; }
        public decimal MontoTotalDeuda { get; set; }
    }

    public class PaymentValidationViewModel
    {
        [Required(ErrorMessage = "El Documento/DNI es requerido")]
        [Display(Name = "Documento / DNI")]
        public string ClientId { get; set; }

        [Required(ErrorMessage = "El número de voucher es requerido")]
        [Display(Name = "Número de Voucher")]
        public string VoucherNumber { get; set; }

        [Required(ErrorMessage = "El monto del pago es requerido")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
        [Display(Name = "Monto del Pago")]
        public decimal PaymentAmount { get; set; }

        [Required(ErrorMessage = "La fecha de pago es requerida")]
        [Display(Name = "Fecha de Pago")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; }
    }

    public class DashboardViewModel
    {
        public ValidationMetrics Metrics { get; set; }
        public List<PaymentValidation> RecentValidations { get; set; }
        public PaymentValidationViewModel ValidationForm { get; set; }
        public List<Cliente> ClientesRecientes { get; set; }
    }
}
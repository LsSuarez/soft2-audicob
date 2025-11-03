using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Audicob.Models;
using System.Collections.Generic;
using System.Linq;

namespace Audicob.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class ValidationController : Controller
    {
        private static List<PaymentValidation> _validations = new();
        private static List<Cliente> _clientesDemo = new()
        {
            new Cliente { 
                Id = 1, 
                Documento = "12345678", 
                Nombre = "Cliente Demo", 
                DeudaTotal = 2500.00m, 
                EstadoMora = "Al día", 
                EstadoAdmin = "Aceptado" 
            },
            new Cliente { 
                Id = 2, 
                Documento = "87654321", 
                Nombre = "María López", 
                DeudaTotal = 3200.00m, 
                EstadoMora = "Temprana", 
                EstadoAdmin = "Pendiente" 
            },
            new Cliente { 
                Id = 3, 
                Documento = "11223344", 
                Nombre = "Carlos Ruiz", 
                DeudaTotal = 2800.00m, 
                EstadoMora = "Temprana", 
                EstadoAdmin = "Activo" 
            }
        };

        public IActionResult Index()
        {
            var viewModel = new DashboardViewModel
            {
                Metrics = GetValidationMetrics(),
                RecentValidations = _validations.Take(5).ToList(),
                ValidationForm = new PaymentValidationViewModel(),
                ClientesRecientes = _clientesDemo
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ValidateData(PaymentValidationViewModel model)
        {
            // Debug info
            System.Console.WriteLine($"🔍 ValidateData llamado - ClientId: '{model.ClientId}'");

            if (!ModelState.IsValid)
            {
                var dashboardModel = new DashboardViewModel
                {
                    Metrics = GetValidationMetrics(),
                    RecentValidations = _validations.Take(5).ToList(),
                    ValidationForm = model,
                    ClientesRecientes = _clientesDemo
                };
                return View("Index", dashboardModel);
            }

            // Validar si el cliente existe
            var cliente = _clientesDemo.FirstOrDefault(c => c.Documento == model.ClientId);

            if (cliente == null)
            {
                ModelState.AddModelError("ClientId", "Cliente no encontrado con este documento");
                var dashboardModel = new DashboardViewModel
                {
                    Metrics = GetValidationMetrics(),
                    RecentValidations = _validations.Take(5).ToList(),
                    ValidationForm = model,
                    ClientesRecientes = _clientesDemo
                };
                return View("Index", dashboardModel);
            }

            // Validar monto vs deuda
            if (model.PaymentAmount > cliente.DeudaTotal)
            {
                ModelState.AddModelError("PaymentAmount", $"El monto excede la deuda total del cliente. Deuda actual: {cliente.DeudaTotal:C}");
                var dashboardModel = new DashboardViewModel
                {
                    Metrics = GetValidationMetrics(),
                    RecentValidations = _validations.Take(5).ToList(),
                    ValidationForm = model,
                    ClientesRecientes = _clientesDemo
                };
                return View("Index", dashboardModel);
            }

            // Validación exitosa
            var validation = new PaymentValidation
            {
                Id = _validations.Count + 1,
                ClientId = model.ClientId,
                VoucherNumber = model.VoucherNumber,
                PaymentAmount = model.PaymentAmount,
                PaymentDate = model.PaymentDate,
                Status = ValidationStatus.Validated,
                CreatedAt = DateTime.Now,
                ValidationMessage = $"Validación exitosa para {cliente.Nombre}",
                ClienteId = cliente.Id,
                Cliente = cliente
            };

            _validations.Insert(0, validation);

            TempData["SuccessMessage"] = $"✅ Validación exitosa para {cliente.Nombre}. Ahora puede sincronizar el pago.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Synchronize(int id)
        {
            var validation = _validations.FirstOrDefault(v => v.Id == id);
            if (validation != null)
            {
                validation.Status = ValidationStatus.Synchronized;
                validation.SynchronizedAt = DateTime.Now;
                validation.ValidationMessage = "Pago sincronizado exitosamente";
            }

            TempData["SuccessMessage"] = "✅ Datos sincronizados exitosamente.";
            return RedirectToAction("Index");
        }

        public IActionResult Dashboard()
        {
            var metrics = GetValidationMetrics();
            return View(metrics);
        }

        private ValidationMetrics GetValidationMetrics()
        {
            var totalClientes = _clientesDemo.Count;
            var clientesAlDia = _clientesDemo.Count(c => c.EstadoMora == "Al día");
            var clientesEnMora = totalClientes - clientesAlDia;
            var montoTotalDeuda = _clientesDemo.Sum(c => c.DeudaTotal);

            var validRecords = _validations.Count(v => v.Status == ValidationStatus.Validated || 
                                                     v.Status == ValidationStatus.Synchronized);
            var errorRecords = _validations.Count(v => v.Status == ValidationStatus.Error);

            return new ValidationMetrics
            {
                TotalRecords = _validations.Count,
                ValidRecords = validRecords,
                ErrorRecords = errorRecords,
                PendingAlerts = 5,
                ResolvedAlerts = 12,
                LastAlert = $"Hoy {DateTime.Now:hh:mm tt} - Voucher #45892 con inconsistencias",
                TotalClientes = totalClientes,
                ClientesAlDia = clientesAlDia,
                ClientesEnMora = clientesEnMora,
                MontoTotalDeuda = montoTotalDeuda
            };
        }
    }
}
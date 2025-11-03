using Audicob.Data;
using Audicob.Models;
using Audicob.Models.ViewModels.Cliente;
using Audicob.Models.ViewModels.Cobranza;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Font;
using iText.IO.Font.Constants;

namespace Audicob.Controllers
{
    [Authorize(Roles = "Cliente")]
    public class ClienteController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClienteController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // Dashboard del cliente
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            
            // Buscar cliente por UserId
            var cliente = await _db.Clientes
                .Include(c => c.Pagos)
                .Include(c => c.Evaluaciones)
                .FirstOrDefaultAsync(c => c.UserId == user.Id);

            if (cliente == null)
            {
                TempData["Error"] = "No se encontró información del cliente.";
                return RedirectToAction("Index", "Home");
            }

            var vm = new ClienteDashboardViewModel
            {
                Nombre = cliente.Nombre,
                DeudaTotal = cliente.DeudaTotal,
                IngresosMensuales = cliente.IngresosMensuales,
                PagosFechas = cliente.Pagos.Select(p => p.Fecha.ToString("dd/MM/yyyy")).ToList(),
                PagosMontos = cliente.Pagos.Select(p => p.Monto).ToList(),
                PagosRecientes = cliente.Pagos.OrderByDescending(p => p.Fecha).Take(5).Select(p => new PagoResumen
                {
                    Fecha = p.Fecha,
                    Monto = p.Monto,
                    Validado = p.Validado
                }).ToList(),
                Evaluaciones = cliente.Evaluaciones.Select(e => new EvaluacionResumen
                {
                    Fecha = e.Fecha,
                    Estado = e.Estado,
                    Responsable = e.Responsable,
                    Comentario = e.Comentario
                }).ToList()
            };

            return View(vm);
        }

        // HU 13: Abonar cliente
        public async Task<IActionResult> DetalleDeudaTotal()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var cliente = await _db.Clientes
                .Include(c => c.PagosPendientes)
                .FirstOrDefaultAsync(c => c.UserId == user.Id);

            if (cliente == null)
            {
                TempData["Error"] = "No se encontró información del cliente.";
                return RedirectToAction("Index", "Home");
            }

            // Obtener método guardado (si lo tienes en MetodosPagoClientes)
            var metodo = await _db.MetodosPagoClientes
                .FirstOrDefaultAsync(m => m.UserId == user.Id);
            ViewBag.MetodoSeleccionado = metodo?.Metodo ?? "—";

            var deudas = cliente.PagosPendientes?.ToList() ?? new List<PagoPendiente>();
            return View(deudas);
        }

        // Acción: Devuelve el partial con el PagoPendiente solicitado
        [HttpGet]
        public async Task<IActionResult> DetallePago(int id)
        {
            var pago = await _db.PagoPendiente.FirstOrDefaultAsync(p => p.Id == id);
            if (pago == null) return NotFound();
            return PartialView("_DetallePagoPartial", pago);
        }

        // Acción: Marca deuda como cancelada
        [HttpPost]
        public async Task<IActionResult> PagarDeuda(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Buscar y validar que la deuda pertenece al cliente autenticado
            var deuda = await _db.PagoPendiente
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.Id == id && p.Cliente.UserId == user.Id);

            if (deuda == null) return NotFound();

            if (deuda.Estado == "Cancelado")
                return BadRequest(new { success = false, message = "Ya está cancelado." });

            deuda.Estado = "Cancelado";
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = "Pago registrado correctamente" });
        }

        // ===============================
        // PERFIL DEL CLIENTE
        // ===============================
        public async Task<IActionResult> MiPerfil()
        {
            var user = await _userManager.GetUserAsync(User);

            // Buscar el perfil del cliente autenticado
            var perfil = await _db.PerfilesCliente.FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (perfil == null)
            {
                // Si no existe, crear un registro inicial con los datos del usuario
                perfil = new PerfilCliente
                {
                    UserId = user.Id,
                    Nombre = string.Empty,
                    Correo = user.Email,
                    Telefono = string.Empty,
                    Direccion = string.Empty,
                    DocumentoIdentidad = string.Empty,
                    FechaRegistro = DateTime.UtcNow 
                };
                _db.PerfilesCliente.Add(perfil);
                await _db.SaveChangesAsync();
            }

            return View(perfil);
        }

        [HttpGet]
        public async Task<IActionResult> EditarPerfil()
        {
            var user = await _userManager.GetUserAsync(User);
            var perfil = await _db.PerfilesCliente.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (perfil == null)
                return RedirectToAction("MiPerfil");

            var vm = new EditarPerfilViewModel
            {
                Id = perfil.Id,
                UserId = perfil.UserId,
                Nombre = perfil.Nombre,
                Telefono = perfil.Telefono,
                Correo = perfil.Correo,
                Direccion = perfil.Direccion
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPerfil(EditarPerfilViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var perfil = await _db.PerfilesCliente.FirstOrDefaultAsync(p => p.Id == vm.Id);
            if (perfil == null)
                return NotFound();

            // Actualizar solo los campos permitidos
            perfil.Nombre = vm.Nombre;
            perfil.Telefono = vm.Telefono;
            perfil.Correo = vm.Correo;
            perfil.Direccion = vm.Direccion;

            await _db.SaveChangesAsync();

            TempData["MensajeExito"] = "Tu perfil se actualizó correctamente.";
            return RedirectToAction("MiPerfil");
        }

        // ===============================
        // MÉTODO DE PAGO HU-25
        // ===============================
        public IActionResult MetodoPago()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GuardarMetodoPago(string metodo)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            // Buscar si ya tiene un método registrado
            var registroExistente = await _db.MetodosPagoClientes
                .FirstOrDefaultAsync(m => m.UserId == user.Id);

            if (registroExistente != null)
            {
                registroExistente.Metodo = metodo;
            }
            else
            {
                var nuevoRegistro = new MetodoPagoCliente
                {
                    UserId = user.Id,
                    Metodo = metodo
                };
                _db.MetodosPagoClientes.Add(nuevoRegistro);
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Método de pago '{metodo}' guardado correctamente.";

            return RedirectToAction("DetalleDeudaTotal");
        }

        // ===============================
        // ESTADO DE CUENTA - HU Exportar Estado de Cuenta
        // ===============================
        
        public async Task<IActionResult> EstadoCuenta(string searchTerm, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var accountStatement = await GetAccountStatementData(user.Id, searchTerm, fechaDesde, fechaHasta);
            return View(accountStatement);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DescargarEstadoCuenta()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");

                var accountData = await GetAccountStatementData(user.Id);
                var pdfBytes = GeneratePdfWithiText(accountData);

                var fileName = $"EstadoCuenta_{(user.FullName ?? "Cliente").Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar el PDF: {ex.Message}";
                return RedirectToAction("EstadoCuenta");
            }
        }

        private async Task<EstadoCuentaViewModel> GetAccountStatementData(string userId, string searchTerm = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
        {
            // Obtener el cliente asociado al usuario
            var cliente = await _db.Clientes
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cliente == null)
            {
                throw new Exception("Cliente no encontrado");
            }

            // Obtener transacciones
            var transacciones = await GetTransaccionesFromDatabase(userId, searchTerm, fechaDesde, fechaHasta);

            // Calcular totales basados en las transacciones
            var totalAbonos = transacciones.Where(t => t.Monto > 0).Sum(t => t.Monto);
            var totalCargos = Math.Abs(transacciones.Where(t => t.Monto < 0).Sum(t => t.Monto));

            return new EstadoCuentaViewModel
            {
                Cliente = cliente.Nombre,
                Fecha = DateTime.Now,
                TotalDeuda = cliente.DeudaTotal,
                Capital = cliente.DeudaTotal * 0.8m, // 80% capital
                Intereses = cliente.DeudaTotal * 0.2m, // 20% intereses
                SaldoAnterior = 1250.00m,
                TotalAbonos = totalAbonos,
                TotalCargos = totalCargos,
                SaldoActual = cliente.DeudaTotal,
                HistorialTransacciones = transacciones,
                SearchTerm = searchTerm,
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta
            };
        }

        private async Task<List<TransaccionViewModel>> GetTransaccionesFromDatabase(string userId, string searchTerm, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            // Crear lista de transacciones de ejemplo
            var transacciones = new List<TransaccionViewModel>
            {
                new TransaccionViewModel { 
                    Id = 1, 
                    Fecha = DateTime.Now.AddDays(-10).ToString("dd/MM/yyyy"),
                    Descripcion = "Pago en línea realizado", 
                    Monto = 500.00m, 
                    Estado = "Completado" 
                },
                new TransaccionViewModel { 
                    Id = 2, 
                    Fecha = DateTime.Now.AddDays(-15).ToString("dd/MM/yyyy"),
                    Descripcion = "Compra en tienda física", 
                    Monto = -350.00m, 
                    Estado = "Procesado" 
                },
                new TransaccionViewModel { 
                    Id = 3, 
                    Fecha = DateTime.Now.AddDays(-20).ToString("dd/MM/yyyy"),
                    Descripcion = "Transferencia bancaria", 
                    Monto = 250.00m, 
                    Estado = "Completado" 
                },
                new TransaccionViewModel { 
                    Id = 4, 
                    Fecha = DateTime.Now.AddDays(-25).ToString("dd/MM/yyyy"),
                    Descripcion = "Compra en línea - Ecommerce", 
                    Monto = -420.00m, 
                    Estado = "Procesado" 
                },
                new TransaccionViewModel { 
                    Id = 5, 
                    Fecha = DateTime.Now.AddDays(-30).ToString("dd/MM/yyyy"),
                    Descripcion = "Saldo inicial del periodo", 
                    Monto = 1250.00m, 
                    Estado = "Activo" 
                },
                new TransaccionViewModel { 
                    Id = 6, 
                    Fecha = DateTime.Now.AddDays(-35).ToString("dd/MM/yyyy"),
                    Descripcion = "Pago con tarjeta de crédito", 
                    Monto = 300.00m, 
                    Estado = "Completado" 
                }
            };

            // Aplicar filtros
            if (!string.IsNullOrEmpty(searchTerm))
            {
                transacciones = transacciones.Where(t => 
                    t.Descripcion.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return transacciones.OrderByDescending(t => DateTime.ParseExact(t.Fecha, "dd/MM/yyyy", null)).ToList();
        }

        private byte[] GeneratePdfWithiText(EstadoCuentaViewModel data)
        {
            using (var memoryStream = new MemoryStream())
            {
                var writer = new PdfWriter(memoryStream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf);

                try
                {
                    // Configurar fuentes
                    var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                    var normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                    // Título principal
                    document.Add(new Paragraph("SISTEMA DE COBRANZA DE BELLEZA")
                        .SetFont(boldFont)
                        .SetFontSize(16)
                        .SetTextAlignment(TextAlignment.CENTER));

                    document.Add(new Paragraph("Estado de Cuenta Detallado")
                        .SetFont(boldFont)
                        .SetFontSize(14)
                        .SetTextAlignment(TextAlignment.CENTER));

                    document.Add(new Paragraph(" "));

                    // Información del cliente
                    document.Add(new Paragraph("INFORMACIÓN DEL CLIENTE")
                        .SetFont(boldFont)
                        .SetFontSize(12));
                    
                    document.Add(new Paragraph($"Cliente: {data.Cliente}")
                        .SetFont(normalFont));
                    document.Add(new Paragraph($"Fecha: {data.Fecha:dd/MM/yyyy}")
                        .SetFont(normalFont));
                    document.Add(new Paragraph($"Hora: {data.Fecha:HH:mm}")
                        .SetFont(normalFont));

                    document.Add(new Paragraph(" "));

                    // Resumen de Deuda
                    document.Add(new Paragraph("RESUMEN DE DEUDA")
                        .SetFont(boldFont)
                        .SetFontSize(12));
                    
                    document.Add(new Paragraph($"Total Deuda: ${data.TotalDeuda:N2}")
                        .SetFont(normalFont));
                    document.Add(new Paragraph($"Capital: ${data.Capital:N2}")
                        .SetFont(normalFont));
                    document.Add(new Paragraph($"Intereses: ${data.Intereses:N2}")
                        .SetFont(normalFont));

                    document.Add(new Paragraph(" "));

                    // Saldos
                    document.Add(new Paragraph($"SALDO ANTERIOR: ${data.SaldoAnterior:N2}")
                        .SetFont(boldFont));
                    document.Add(new Paragraph($"SALDO ACTUAL: ${data.SaldoActual:N2}")
                        .SetFont(boldFont));

                    document.Add(new Paragraph(" "));

                    // Movimientos
                    document.Add(new Paragraph("MOVIMIENTOS")
                        .SetFont(boldFont)
                        .SetFontSize(12));
                    
                    document.Add(new Paragraph($"Abonos: ${data.TotalAbonos:N2}")
                        .SetFont(normalFont));
                    document.Add(new Paragraph($"Cargos: ${data.TotalCargos:N2}")
                        .SetFont(normalFont));

                    document.Add(new Paragraph(" "));

                    // Historial de transacciones
                    document.Add(new Paragraph("HISTORIAL DE TRANSACCIONES")
                        .SetFont(boldFont)
                        .SetFontSize(12));

                    // Crear tabla para transacciones
                    var table = new Table(4, true);
                    
                    // Encabezados de tabla
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Fecha").SetFont(boldFont)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Descripción").SetFont(boldFont)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Monto").SetFont(boldFont)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Estado").SetFont(boldFont)));

                    foreach (var transaccion in data.HistorialTransacciones)
                    {
                        var signo = transaccion.Monto >= 0 ? "+" : "";
                        
                        table.AddCell(new Cell().Add(new Paragraph(transaccion.Fecha).SetFont(normalFont)));
                        table.AddCell(new Cell().Add(new Paragraph(transaccion.Descripcion).SetFont(normalFont)));
                        table.AddCell(new Cell().Add(new Paragraph($"{signo}${Math.Abs(transaccion.Monto):N2}").SetFont(normalFont)));
                        table.AddCell(new Cell().Add(new Paragraph(transaccion.Estado).SetFont(normalFont)));
                    }

                    document.Add(table);
                    document.Add(new Paragraph(" "));
                    
                    // Pie de página
                    document.Add(new Paragraph($"Documento generado el: {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .SetFont(normalFont)
                        .SetFontSize(8)
                        .SetTextAlignment(TextAlignment.CENTER));
                }
                finally
                {
                    document.Close();
                }

                return memoryStream.ToArray();
            }
        }
    }
}
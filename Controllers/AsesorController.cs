using Audicob.Data;
using Audicob.Models;
using Audicob.Models.ViewModels.Asesor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using iTextSharp.text;
using iTextSharp.text.pdf;
using ClosedXML.Excel;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Audicob.Controllers
{
    [Authorize(Roles = "AsesorCobranza")]
    public class AsesorController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AsesorController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            var asignaciones = await _db.AsignacionesAsesores
                .Include(a => a.Clientes)
                .ThenInclude(c => c.Deuda)
                .Where(a => a.AsesorUserId == user.Id)
                .ToListAsync();

            var clientes = asignaciones.SelectMany(a => a.Clientes).ToList();

            var vm = new AsesorDashboardViewModel
            {
                TotalClientesAsignados = clientes.Count,
                TotalDeudaCartera = clientes.Sum(c => c.Deuda?.TotalAPagar ?? 0),
                TotalPagosRecientes = await _db.Pagos
                    .Where(p => clientes.Select(c => c.Id).Contains(p.ClienteId) &&
                                p.Fecha >= DateTime.UtcNow.AddMonths(-1))
                    .SumAsync(p => p.Monto),
                Clientes = clientes.Select(c => c.Nombre).ToList(),
                DeudasPorCliente = clientes.Select(c => c.Deuda?.TotalAPagar ?? 0).ToList(),
                ClientesAsignados = clientes.Select(c => new ClienteResumen
                {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    Documento = c.Documento,
                    Deuda = c.Deuda?.TotalAPagar ?? 0,
                    DeudaTotal = c.DeudaTotal,
                    IngresosMensuales = c.IngresosMensuales,
                    FechaActualizacion = c.FechaActualizacion,
                    EstadoMora = c.EstadoMora
                }).ToList()
            };

            return View(vm);
        }


        // ==========================================================
        // HU-23 GUARDAR EL HISTORIAL CREDITICIO
        // ==========================================================

        [HttpGet]
        public async Task<IActionResult> HistorialCredito()
        {
            var registros = await _db.HistorialCreditos
                .OrderByDescending(h => h.FechaOperacion)
                .ToListAsync();

            // El modelo que se envía a la vista será una lista de registros
            return View(registros);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HistorialCredito(HistorialCredito model)
        {
            if (ModelState.IsValid)
            {
                model.FechaOperacion = DateTime.SpecifyKind(model.FechaOperacion, DateTimeKind.Utc);
                // Guardar los datos en la BD
                _db.HistorialCreditos.Add(model);
                await _db.SaveChangesAsync();

                ViewBag.Mensaje = "✅ Datos guardados correctamente en el historial.";

                // Limpiar el modelo para que los campos del formulario se vacíen
                ModelState.Clear();
                model = new HistorialCredito();
            }

            // Mostrar nuevamente el historial
            var registros = await _db.HistorialCreditos
                .OrderByDescending(h => h.FechaOperacion)
                .ToListAsync();

            // Devuelve la vista con los registros actualizados
            return View(registros);
        }
        // ==========================================================
        // EDITAR REGISTRO EXISTENTE EN EL HISTORIAL
        // ==========================================================

        // GET: Muestra el formulario de edición
        [HttpGet]
        public async Task<IActionResult> EditarHistorial(int id)
        {
            var registro = await _db.HistorialCreditos.FindAsync(id);
            if (registro == null)
            {
                return NotFound();
            }
            return View(registro);
        }

        // POST: Guarda los cambios del registro editado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarHistorial(HistorialCredito model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // ✅ Asegurar que la fecha sea UTC para evitar el error de PostgreSQL
            model.FechaOperacion = DateTime.SpecifyKind(model.FechaOperacion, DateTimeKind.Utc);

            _db.HistorialCreditos.Update(model);
            await _db.SaveChangesAsync();

            TempData["Mensaje"] = "✅ Registro actualizado correctamente.";
            return RedirectToAction(nameof(HistorialCredito));
        }

        // ==========================================================
        // HU-30: CAMBIAR ESTADO DE MOROSIDAD
        // ==========================================================

        /// <summary>
        /// Muestra la vista para seleccionar un cliente y cambiar su estado de morosidad
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CambiarEstadoMora(int? clienteId)
        {
            try
            {
                if (!User.Identity?.IsAuthenticated ?? true)
                {
                    TempData["Error"] = "Usuario no autenticado";
                    return RedirectToAction("Login", "Account");
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["Error"] = "No se pudo obtener la información del usuario.";
                    return RedirectToAction("Login", "Account");
                }

                // Verificar rol de forma segura
                var userRoles = await _userManager.GetRolesAsync(user);
                if (!userRoles.Contains("AsesorCobranza"))
                {
                    TempData["Error"] = $"No tienes permisos para acceder a esta funcionalidad.";
                    return RedirectToAction("Index", "Home");
                }

                // Obtener clientes asignados al asesor
                var clientesAsignados = new List<Cliente>();

                try
                {
                    var asignaciones = await _db.AsignacionesAsesores
                        .Include(a => a.Clientes)
                        .Where(a => a.AsesorUserId == user.Id)
                        .ToListAsync();

                    clientesAsignados = asignaciones
                        .SelectMany(a => a.Clientes)
                        .ToList();
                }
                catch (Exception dataEx)
                {
                    TempData["Warning"] = $"Error al cargar clientes: {dataEx.Message}";
                }

                var vm = new CambiarEstadoMoraViewModel();

                if (clienteId.HasValue && clientesAsignados.Any())
                {
                    var cliente = clientesAsignados.FirstOrDefault(c => c.Id == clienteId.Value);
                    if (cliente != null)
                    {
                        vm.ClienteId = cliente.Id;
                        vm.ClienteNombre = cliente.Nombre;
                        vm.ClienteDocumento = cliente.Documento;
                        vm.EstadoActual = cliente.EstadoMora;
                    }
                }

                ViewBag.ClientesAsignados = clientesAsignados;

                if (!clientesAsignados.Any())
                {
                    ViewBag.InfoMessage = "No tienes clientes asignados para gestionar.";
                }

                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error en CambiarEstadoMora: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        /// <summary>
        /// Procesa el cambio de estado de morosidad
        /// Criterio de Aceptación 1, 2: Cambiar estado y guardarlo en historial
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstadoMora(CambiarEstadoMoraViewModel modelo)
        {
            try
            {
                if (!User.Identity?.IsAuthenticated ?? true)
                {
                    return Challenge();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    ModelState.AddModelError("", "No se pudo obtener la información del usuario. Por favor, inicie sesión nuevamente.");
                    return await CargarDatosFormulario(modelo);
                }

                if (!User.IsInRole("AsesorCobranza"))
                {
                    ModelState.AddModelError("", "No tienes permisos para realizar esta acción.");
                    return await CargarDatosFormulario(modelo);
                }

                // Validar que el cliente esté asignado al asesor
                var asignacion = await _db.AsignacionesAsesores
                    .Include(a => a.Clientes)
                    .FirstOrDefaultAsync(a => a.AsesorUserId == user.Id && a.Clientes.Any(c => c.Id == modelo.ClienteId));

                if (asignacion == null)
                {
                    ModelState.AddModelError("", "No tienes permisos para modificar este cliente.");
                    return await CargarDatosFormulario(modelo);
                }

                var cliente = asignacion.Clientes.FirstOrDefault(c => c.Id == modelo.ClienteId);

                if (cliente == null)
                {
                    ModelState.AddModelError("", "Cliente no encontrado.");
                    return await CargarDatosFormulario(modelo);
                }

                // Validar el cambio de estado
                modelo.EstadoActual = cliente.EstadoMora;
                var errores = modelo.ValidarCambioEstado();

                if (errores.Any())
                {
                    foreach (var error in errores)
                    {
                        ModelState.AddModelError("", error);
                    }
                    return await CargarDatosFormulario(modelo);
                }

                if (!ModelState.IsValid)
                {
                    return await CargarDatosFormulario(modelo);
                }

                // Crear registro en el historial antes de cambiar el estado
                var historial = new HistorialEstadoMora
                {
                    ClienteId = cliente.Id,
                    EstadoAnterior = cliente.EstadoMora,
                    NuevoEstado = modelo.NuevoEstado,
                    UsuarioId = user.Id,
                    FechaCambio = DateTime.UtcNow,
                    MotivoCambio = modelo.MotivoCambio,
                    Observaciones = modelo.Observaciones,
                    DireccionIP = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Desconocida",
                    UserAgent = HttpContext.Request.Headers["User-Agent"].ToString()
                };

                // Cambiar el estado del cliente
                cliente.EstadoMora = modelo.NuevoEstado;
                cliente.FechaActualizacion = DateTime.UtcNow;

                // Guardar cambios en la base de datos
                _db.HistorialEstadosMora.Add(historial);
                _db.Clientes.Update(cliente);
                await _db.SaveChangesAsync();

                // Criterio de Aceptación 3: Enviar notificación si está habilitado
                if (modelo.EnviarNotificacion)
                {
                    await EnviarNotificacionCambioEstado(cliente, historial);
                }

                TempData["Success"] = $"Estado de morosidad cambiado exitosamente de '{historial.EstadoAnterior}' a '{historial.NuevoEstado}' para el cliente {cliente.Nombre}.";

                return RedirectToAction("VerHistorialEstado", new { clienteId = cliente.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error al cambiar el estado: {ex.Message}");
                return await CargarDatosFormulario(modelo);
            }
        }

        /// <summary>
        /// Muestra el historial de cambios de estado de un cliente
        /// Criterio de Aceptación 5: El cambio debe ser visible en el historial
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> VerHistorialEstado(int clienteId)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return Challenge();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "No se pudo obtener la información del usuario. Por favor, inicie sesión nuevamente.";
                return RedirectToAction("Login", "Account");
            }

            if (!User.IsInRole("AsesorCobranza"))
            {
                TempData["Error"] = "No tienes permisos para acceder a esta funcionalidad.";
                return RedirectToAction("Index", "Home");
            }

            // Verificar que el cliente esté asignado al asesor
            var asignacion = await _db.AsignacionesAsesores
                .Include(a => a.Clientes)
                .FirstOrDefaultAsync(a => a.AsesorUserId == user.Id && a.Clientes.Any(c => c.Id == clienteId));

            if (asignacion == null)
            {
                TempData["Error"] = "No tienes permisos para ver el historial de este cliente.";
                return RedirectToAction("Dashboard");
            }

            var cliente = asignacion.Clientes.FirstOrDefault(c => c.Id == clienteId);

            if (cliente == null)
            {
                TempData["Error"] = "Cliente no encontrado.";
                return RedirectToAction("Dashboard");
            }

            // Obtener historial de cambios de estado
            var historial = await _db.HistorialEstadosMora
                .Include(h => h.Usuario)
                .Where(h => h.ClienteId == clienteId)
                .OrderByDescending(h => h.FechaCambio)
                .ToListAsync();

            var vm = new HistorialEstadoMoraViewModel
            {
                ClienteId = cliente.Id,
                ClienteNombre = cliente.Nombre,
                ClienteDocumento = cliente.Documento,
                EstadoActual = cliente.EstadoMora,
                TotalCambios = historial.Count,
                FechaUltimoCambio = historial.FirstOrDefault()?.FechaCambio,
                Cambios = historial.Select(h => new HistorialCambioViewModel
                {
                    Id = h.Id,
                    EstadoAnterior = h.EstadoAnterior,
                    NuevoEstado = h.NuevoEstado,
                    FechaCambio = h.FechaCambio,
                    UsuarioNombre = h.Usuario?.UserName ?? "Usuario desconocido",
                    MotivoCambio = h.MotivoCambio,
                    Observaciones = h.Observaciones
                }).ToList()
            };

            return View(vm);
        }

        /// <summary>
        /// Obtiene los datos del cliente por AJAX
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerDatosCliente(int clienteId)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return Json(new { success = false, message = "Usuario no autenticado" });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "No se pudo obtener la información del usuario" });
            }

            if (!User.IsInRole("AsesorCobranza"))
            {
                return Json(new { success = false, message = "No tienes permisos para realizar esta acción" });
            }

            var cliente = await _db.AsignacionesAsesores
                .Include(a => a.Clientes)
                .Where(a => a.AsesorUserId == user.Id && a.Clientes.Any(c => c.Id == clienteId))
                .SelectMany(a => a.Clientes)
                .FirstOrDefaultAsync(c => c.Id == clienteId);

            if (cliente == null)
            {
                return Json(new { success = false, message = "Cliente no encontrado o no asignado" });
            }

            return Json(new
            {
                success = true,
                cliente = new
                {
                    id = cliente.Id,
                    nombre = cliente.Nombre,
                    documento = cliente.Documento,
                    estadoActual = cliente.EstadoMora,
                    deudaTotal = cliente.DeudaTotal,
                    fechaActualizacion = cliente.FechaActualizacion.ToString("dd/MM/yyyy")
                }
            });
        }

        // ==========================================================
        // MÉTODOS AUXILIARES PRIVADOS PARA MORA
        // ==========================================================

        /// <summary>
        /// Carga los datos necesarios para el formulario
        /// </summary>
        private async Task<IActionResult> CargarDatosFormulario(CambiarEstadoMoraViewModel modelo)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return Challenge();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var clientesAsignados = await _db.AsignacionesAsesores
                .Include(a => a.Clientes)
                .Where(a => a.AsesorUserId == user.Id)
                .SelectMany(a => a.Clientes)
                .ToListAsync();

            ViewBag.ClientesAsignados = clientesAsignados;
            return View(modelo);
        }

        /// <summary>
        /// Envía notificación al cliente sobre el cambio de estado
        /// Criterio de Aceptación 3: Notificación automática al cliente
        /// </summary>
        private async Task EnviarNotificacionCambioEstado(Cliente cliente, HistorialEstadoMora historial)
        {
            try
            {
                // Aquí se implementaría la lógica de notificación
                // Por ejemplo: email, SMS, notificación push, etc.

                // Simulación de envío de notificación
                // En una implementación real, aquí iría la integración con:
                // - Servicio de email (SendGrid, AWS SES, etc.)
                // - Servicio de SMS (Twilio, etc.)
                // - Sistema de notificaciones push

                var mensaje = $"Estimado {cliente.Nombre}, " +
                             $"su estado de morosidad ha sido actualizado de '{historial.EstadoAnterior}' " +
                             $"a '{historial.NuevoEstado}' el {historial.FechaCambio:dd/MM/yyyy HH:mm}. " +
                             $"Motivo: {historial.MotivoCambio}";

                // Log para auditoría
                Console.WriteLine($"[NOTIFICACIÓN] Cliente: {cliente.Nombre}, Mensaje: {mensaje}");

                await Task.CompletedTask; // Simular operación asíncrona
            }
            catch (Exception ex)
            {
                // Log del error pero no fallar la operación principal
                Console.WriteLine($"[ERROR] No se pudo enviar notificación: {ex.Message}");
            }
        }

        // ======================================
        // HU-31 CONSULTA DE RIESGO DEL CLIENTE 
        // ======================================

        [HttpGet]
        public IActionResult ConsultaRiesgo()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ConsultaRiesgo(string dni)
        {
            if (string.IsNullOrWhiteSpace(dni))
            {
                ViewBag.Error = "Por favor, ingrese un DNI válido.";
                return View();
            }

            var cliente = _db.PerfilesCliente.FirstOrDefault(c => c.DocumentoIdentidad == dni);

            if (cliente == null)
            {
                ViewBag.Error = "No se encontró ningún cliente con ese DNI.";
                return View();
            }

            // Buscar el puntaje calculado del cliente
            var riesgo = _db.DetalleRiesgo
                .FirstOrDefault(r => r.PerfilClienteId == cliente.Id && r.Elemento == "Puntaje Calculado");

            ViewBag.Riesgo = riesgo;

            return View(cliente);
        }

        // =======================================
        // HU-31 CARGAR DETALLE DE RIESGO (AJAX)
        // =======================================
        [HttpGet]
        public async Task<IActionResult> CargarDetalleRiesgo(int id)
        {
            var riesgos = await _db.DetalleRiesgo
                .Where(r => r.PerfilClienteId == id)
                .ToListAsync();

            return PartialView("_DetalleRiesgoPartial", riesgos);
        }

        // =======================================
// HU-31 EXPORTAR RIESGO A PDF
// =======================================
[HttpGet]
public IActionResult ExportarRiesgo(int id)
{
    var cliente = _db.PerfilesCliente.FirstOrDefault(c => c.Id == id);
    if (cliente == null)
        return NotFound();

    var riesgos = _db.DetalleRiesgo
        .Where(r => r.PerfilClienteId == id)
        .ToList();

    using (var ms = new MemoryStream())
    {
        // Crear documento PDF
        var document = new iTextSharp.text.Document(PageSize.A4, 40, 40, 40, 40);
        var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, ms);
        document.Open();

        // Título
        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
        var subFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
        var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

        document.Add(new Paragraph("Reporte de Riesgo del Cliente", titleFont));
        document.Add(new Paragraph(" ")); // espacio

        // Datos del cliente
        var tableCliente = new PdfPTable(2);
        tableCliente.WidthPercentage = 100;
        tableCliente.AddCell(new Phrase("DNI:", subFont));
        tableCliente.AddCell(new Phrase(cliente.DocumentoIdentidad ?? "", normalFont));
        tableCliente.AddCell(new Phrase("Nombre:", subFont));
        tableCliente.AddCell(new Phrase(cliente.Nombre ?? "", normalFont));
        tableCliente.AddCell(new Phrase("Teléfono:", subFont));
        tableCliente.AddCell(new Phrase(cliente.Telefono ?? "", normalFont));
        tableCliente.AddCell(new Phrase("Correo:", subFont));
        tableCliente.AddCell(new Phrase(cliente.Correo ?? "", normalFont));
        tableCliente.AddCell(new Phrase("Dirección:", subFont));
        tableCliente.AddCell(new Phrase(cliente.Direccion ?? "", normalFont));
        document.Add(tableCliente);

        document.Add(new Paragraph(" "));
        document.Add(new Paragraph("Detalle de Riesgo", subFont));
        document.Add(new Paragraph(" "));

        // Tabla de riesgo
        var table = new PdfPTable(3);
        table.WidthPercentage = 100;
        table.AddCell(new Phrase("Elemento", subFont));
        table.AddCell(new Phrase("Valor", subFont));
        table.AddCell(new Phrase("Comentario", subFont));

        foreach (var r in riesgos)
        {
            var color = BaseColor.White;

            if (r.Elemento == "Puntaje Calculado")
            {
                color = r.Comentario switch
                {
                    "Bajo" => BaseColor.Green,
                    "Medio" => BaseColor.Yellow,
                    "Alto" => BaseColor.Red,
                    _ => BaseColor.White
                };
            }

            var cellElemento = new PdfPCell(new Phrase(r.Elemento, normalFont));
            var cellValor = new PdfPCell(new Phrase(r.Valor, normalFont));
            var cellComentario = new PdfPCell(new Phrase(r.Comentario, normalFont));

            cellElemento.BackgroundColor = color;
            cellValor.BackgroundColor = color;
            cellComentario.BackgroundColor = color;

            table.AddCell(cellElemento);
            table.AddCell(cellValor);
            table.AddCell(cellComentario);
        }

        document.Add(table);
        document.Close();

        byte[] bytes = ms.ToArray();
        return File(bytes, "application/pdf", $"RiesgoCliente_{cliente.DocumentoIdentidad}.pdf");
    }
}


    }
}

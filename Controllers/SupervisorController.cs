using Audicob.Data;
using Audicob.Models;
using Audicob.Models.ViewModels.Asesor;
using Audicob.Models.ViewModels.Supervisor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using iTextSharp.text;
using iTextSharp.text.pdf;
using ClosedXML.Excel;
using System.IO;

namespace Audicob.Controllers
{
    [Authorize(Roles = "Supervisor")]
    public class SupervisorController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public SupervisorController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        // Dashboard principal
        public async Task<IActionResult> Dashboard()
        {
            var vm = new SupervisorDashboardViewModel
            {
                TotalClientes = await _db.Clientes.CountAsync(),
                EvaluacionesPendientes = await _db.Evaluaciones.CountAsync(e => e.Estado == "Pendiente"),
                TotalDeuda = await _db.Clientes.SumAsync(c => c.DeudaTotal),
                TotalPagosUltimoMes = await _db.Pagos
                    .Where(p => p.Fecha >= DateTime.UtcNow.AddMonths(-1))
                    .SumAsync(p => p.Monto)
            };

            var pagos = await _db.Pagos
                .Where(p => p.Fecha >= DateTime.UtcNow.AddMonths(-6))
                .GroupBy(p => new { p.Fecha.Year, p.Fecha.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Total = g.Sum(x => x.Monto)
                })
                .OrderBy(x => x.Month)
                .ToListAsync();

            var pagosFormat = pagos.Select(g => new
            {
                Mes = $"{g.Month}/{g.Year}",
                Total = g.Total
            }).ToList();

            vm.Meses = pagosFormat.Select(p => p.Mes).ToList();
            vm.PagosPorMes = pagosFormat.Select(p => p.Total).ToList();

            var deudas = await _db.Clientes
                .OrderByDescending(c => c.DeudaTotal)
                .Take(5)
                .Select(c => new { c.Nombre, c.DeudaTotal })
                .ToListAsync();

            vm.Clientes = deudas.Select(d => d.Nombre).ToList();
            vm.DeudasPorCliente = deudas.Select(d => d.DeudaTotal).ToList();

            var pagosPendientes = await _db.Pagos
                .Where(p => p.Estado == "Pendiente")
                .Include(p => p.Cliente)
                .OrderBy(p => p.Fecha)
                .Take(10)
                .ToListAsync();

            vm.PagosPendientes = pagosPendientes;

            return View(vm);
        }

        // GET: Supervisor/CrearClienteDemo
        public async Task<IActionResult> CrearClienteDemo()
        {
            // Verificar si ya existe el cliente demo
            var clienteExistente = await _db.Clientes
                .FirstOrDefaultAsync(c => c.Documento == "DEMO001");

            if (clienteExistente == null)
            {
                var clienteDemo = new Cliente
                {
                    Nombre = "Cliente Demo",
                    Documento = "DEMO001",
                    IngresosMensuales = 5000.00m,
                    DeudaTotal = 2500.00m,
                    EstadoMora = "Temprana",
                    FechaActualizacion = DateTime.Now.AddDays(-15),
                    EstadoAdmin = "Activo"
                };

                _db.Clientes.Add(clienteDemo);
                await _db.SaveChangesAsync();

                // Crear también el estado de cartera inicial
                var carteraEstado = new CarteraEstado
                {
                    ClienteId = clienteDemo.Id,
                    Estado = "vigente",
                    Comentario = "Cliente demo creado automáticamente",
                    FechaModificacion = DateTime.Now,
                    UsuarioModificacion = "Sistema"
                };

                _db.CarteraEstados.Add(carteraEstado);
                await _db.SaveChangesAsync();

                TempData["Success"] = "Cliente demo creado exitosamente.";
                return RedirectToAction("PerfilCliente", new { id = clienteDemo.Id });
            }
            else
            {
                TempData["Info"] = "El cliente demo ya existe.";
                return RedirectToAction("PerfilCliente", new { id = clienteExistente.Id });
            }
        }

        // HU7: Validar pago
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidarPago(int pagoId)
        {
            var pago = await _db.Pagos
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.Id == pagoId);

            if (pago == null)
            {
                TempData["Error"] = "Pago no encontrado.";
                return RedirectToAction("Dashboard");
            }

            if (pago.Estado != "Pendiente")
            {
                TempData["Error"] = "Este pago ya ha sido validado.";
                return RedirectToAction("Dashboard");
            }

            var user = await _userManager.GetUserAsync(User);

            pago.Validado = true;
            pago.Estado = "Cancelado";

            var fechaValidacion = DateTime.UtcNow;
            pago.Observacion = $"Validado por {user.FullName} el {fechaValidacion:dd/MM/yyyy HH:mm:ss}";

            if (pago.Cliente != null)
            {
                pago.Cliente.DeudaTotal -= pago.Monto;
                if (pago.Cliente.DeudaTotal < 0) pago.Cliente.DeudaTotal = 0;
                pago.Cliente.FechaActualizacion = DateTime.UtcNow;
            }

            _db.Update(pago);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Pago de S/ {pago.Monto:N2} validado exitosamente por {user.FullName}. Estado de cuenta actualizado.";
            return RedirectToAction("Dashboard");
        }

        // GET: Asignar línea de crédito (HU3)
        public async Task<IActionResult> AsignarLineaCredito()
        {
            var clientes = await _db.Clientes
                .Select(c => new { c.Id, c.Nombre })
                .ToListAsync();

            Console.WriteLine("Clientes encontrados (GET): " + clientes.Count);

            ViewBag.Clientes = clientes;
            return View();
        }

        // POST: Asignar línea de crédito (HU3)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AsignarLineaCredito(int clienteId, decimal monto)
        {
            if (!ModelState.IsValid)
            {
                var clientes = await _db.Clientes
                    .Select(c => new { c.Id, c.Nombre })
                    .ToListAsync();
                ViewBag.Clientes = clientes;
                return View();
            }

            if (monto < 180)
            {
                TempData["Error"] = "Debe ingresar un monto mayor a 180.";
                return RedirectToAction("AsignarLineaCredito");
            }

            var cliente = await _db.Clientes
                .Include(c => c.LineaCredito)
                .FirstOrDefaultAsync(c => c.Id == clienteId);
            if (cliente == null)
            {
                TempData["Error"] = "Cliente no válido.";
                return RedirectToAction("AsignarLineaCredito");
            }

            if (cliente.LineaCredito != null)
            {
                TempData["Error"] = "El cliente ya tiene asignada una línea de crédito.";
                return RedirectToAction("AsignarLineaCredito");
            }

            var linea = new LineaCredito
            {
                ClienteId = cliente.Id,
                Monto = monto,
                FechaAsignacion = DateTime.UtcNow,
                UsuarioAsignador = User.Identity?.Name ?? "Supervisor"
            };

            _db.LineasCredito.Add(linea);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Línea de crédito asignada a {cliente.Nombre}.";
            return RedirectToAction("AsignarLineaCredito");
        }

        // HU1: Ver informe financiero detallado
        public async Task<IActionResult> VerInformeFinanciero(int id)
        {
            var cliente = await _db.Clientes
                .Include(c => c.Pagos)
                .Include(c => c.Deuda)
                .Include(c => c.Evaluaciones)
                .Include(c => c.LineaCredito)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cliente == null)
            {
                TempData["Error"] = "Cliente no encontrado.";
                return RedirectToAction("Dashboard");
            }

            var pagosUltimos12Meses = await _db.Pagos
                .Where(p => p.ClienteId == cliente.Id && p.Fecha >= DateTime.UtcNow.AddMonths(-12))
                .OrderByDescending(p => p.Fecha)
                .ToListAsync();

            var vm = new InformeFinancieroViewModel
            {
                ClienteId = cliente.Id,
                ClienteNombre = cliente.Nombre,
                Documento = cliente.Documento,
                IngresosMensuales = cliente.IngresosMensuales,
                DeudaTotal = cliente.DeudaTotal,
                FechaActualizacion = cliente.FechaActualizacion,
                PagosUltimos12Meses = pagosUltimos12Meses,
                TotalPagado12Meses = pagosUltimos12Meses.Sum(p => p.Monto),
                LineaCredito = cliente.LineaCredito,
                Deuda = cliente.Deuda,
                Evaluaciones = cliente.Evaluaciones.OrderByDescending(e => e.Fecha).ToList()
            };

            return View(vm);
        }

        // HU2: Ver evaluaciones pendientes
        public async Task<IActionResult> EvaluacionesPendientes()
        {
            var evaluaciones = await _db.Evaluaciones
                .Include(e => e.Cliente)
                .Where(e => e.Estado == "Pendiente")
                .OrderBy(e => e.Fecha)
                .ToListAsync();

            return View(evaluaciones);
        }

        // HU2: Ver detalle de evaluación
        public async Task<IActionResult> DetalleEvaluacion(int id)
        {
            var evaluacion = await _db.Evaluaciones
                .Include(e => e.Cliente)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evaluacion == null)
            {
                TempData["Error"] = "Evaluación no encontrada.";
                return RedirectToAction("EvaluacionesPendientes");
            }

            var vm = new EvaluacionViewModel
            {
                ClienteId = evaluacion.ClienteId,
                NombreCliente = evaluacion.Cliente.Nombre,
                IngresosMensuales = evaluacion.Cliente.IngresosMensuales,
                DeudaTotal = evaluacion.Cliente.DeudaTotal,
                Estado = evaluacion.Estado,
                Responsable = evaluacion.Responsable,
                Comentario = evaluacion.Comentario,
                FechaEvaluacion = evaluacion.Fecha
            };

            return View(vm);
        }

        // HU2: Confirmar evaluación
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarEvaluacion(int id)
        {
            var evaluacion = await _db.Evaluaciones
                .Include(e => e.Cliente)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evaluacion == null)
            {
                TempData["Error"] = "Evaluación no encontrado.";
                return RedirectToAction("EvaluacionesPendientes");
            }

            var user = await _userManager.GetUserAsync(User);

            evaluacion.Estado = "Marcado";
            evaluacion.Responsable = user.FullName;
            evaluacion.Fecha = DateTime.UtcNow;

            _db.Update(evaluacion);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Evaluación confirmada y marcada por {user.FullName}.";
            return RedirectToAction("EvaluacionesPendientes");
        }

        // HU2: Rechazar evaluación
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RechazarEvaluacion(int id, string comentario)
        {
            if (string.IsNullOrWhiteSpace(comentario))
            {
                TempData["Error"] = "El comentario es obligatorio al rechazar una evaluación.";
                return RedirectToAction("DetalleEvaluacion", new { id });
            }

            var evaluacion = await _db.Evaluaciones
                .Include(e => e.Cliente)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evaluacion == null)
            {
                TempData["Error"] = "Evaluación no encontrada.";
                return RedirectToAction("EvaluacionesPendientes");
            }

            var user = await _userManager.GetUserAsync(User);

            evaluacion.Estado = "Rechazado";
            evaluacion.Responsable = user.FullName;
            evaluacion.Comentario = comentario;
            evaluacion.Fecha = DateTime.UtcNow;

            _db.Update(evaluacion);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Evaluación rechazada por {user.FullName}.";
            return RedirectToAction("EvaluacionesPendientes");
        }

        // HU4: Buscar cliente
        public async Task<IActionResult> BuscarCliente(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                TempData["Error"] = "Debe ingresar un código o documento para buscar.";
                return RedirectToAction("Dashboard");
            }

            var cliente = await _db.Clientes
                .Include(c => c.Pagos)
                .Include(c => c.Deuda)
                .Include(c => c.Evaluaciones)
                .Include(c => c.LineaCredito)
                .Include(c => c.AsignacionAsesor)
                .FirstOrDefaultAsync(c => c.Documento == codigo || c.Id.ToString() == codigo);

            if (cliente == null)
            {
                TempData["Error"] = "Cliente no encontrado.";
                return RedirectToAction("Dashboard");
            }

            return RedirectToAction("PerfilCliente", new { id = cliente.Id });
        }

        // HU4: Ver perfil completo del cliente
        public async Task<IActionResult> PerfilCliente(int id)
        {
            var cliente = await _db.Clientes
                .Include(c => c.Pagos)
                .Include(c => c.Deuda)
                .Include(c => c.Evaluaciones)
                .Include(c => c.LineaCredito)
                .Include(c => c.AsignacionAsesor)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cliente == null)
            {
                TempData["Error"] = "Cliente no encontrado.";
                return RedirectToAction("Dashboard");
            }

            var transacciones = await _db.Transacciones
                .Where(t => t.ClienteId == cliente.Id)
                .OrderByDescending(t => t.Fecha)
                .Take(10)
                .ToListAsync();

            var vm = new PerfilClienteViewModel
            {
                ClienteInfo = cliente,
                TransaccionesRecientes = transacciones,
                TotalPagos = cliente.Pagos.Sum(p => p.Monto),
                PagosValidados = cliente.Pagos.Count(p => p.Validado),
                PagosPendientes = cliente.Pagos.Count(p => !p.Validado)
            };

            return View(vm);
        }

        // ==========================================================
        // HU: MODIFICAR ESTADO DE CARTERA - FUNCIONALIDADES CORREGIDAS
        // ==========================================================

        // GET: Supervisor/ModificarEstado
        public IActionResult ModificarEstado()
        {
            return View();
        }

        // POST: Supervisor/BuscarClientePorDNI
        [HttpPost]
        public async Task<JsonResult> BuscarClientePorDNI([FromBody] BuscarClienteRequest request)
        {
            try
            {
                var cliente = await _db.Clientes
                    .FirstOrDefaultAsync(c => c.Documento == request.DNI);

                if (cliente != null)
                {
                    // Buscar el estado de cartera más reciente
                    var estadoCartera = await _db.CarteraEstados
                        .Where(ce => ce.ClienteId == cliente.Id)
                        .OrderByDescending(ce => ce.FechaModificacion)
                        .FirstOrDefaultAsync();

                    var estadoActual = estadoCartera?.Estado ?? "Vigente";

                    var clienteDto = new
                    {
                        Id = cliente.Id,
                        DNI = cliente.Documento,
                        Nombre = cliente.Nombre,
                        Estado = estadoActual
                    };

                    return Json(new { success = true, data = clienteDto });
                }
                else
                {
                    return Json(new { success = false, message = "Cliente no encontrado" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error en la búsqueda" });
            }
        }
        // POST: Supervisor/DescartarCambiosCartera
        [HttpPost]
        public async Task<JsonResult> DescartarCambiosCartera([FromBody] DescartarCambiosRequest request)
        {
            try
            {
                var cliente = await _db.Clientes.FindAsync(request.ClienteId);
                if (cliente == null)
                {
                    return Json(new { success = false, message = "Cliente no encontrado" });
                }

                var estadoActual = await _db.CarteraEstados
                    .Where(ce => ce.ClienteId == request.ClienteId)
                    .OrderByDescending(ce => ce.FechaModificacion)
                    .FirstOrDefaultAsync();

                return Json(new {
                    success = true,
                    estadoActual = estadoActual?.Estado ?? "Vigente",
                    message = "Cambios descartados correctamente"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al descartar cambios" });
            }
        }

        // HU: Modificar Estado de Cartera - Vistas existentes (mantenidas para compatibilidad)
        // GET: Supervisor/DetalleCartera/5
        public async Task<IActionResult> DetalleCartera(int id)
        {
            var cliente = await _db.Clientes
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cliente == null)
            {
                TempData["Error"] = "Cliente no encontrado.";
                return RedirectToAction("Dashboard");
            }

            // Buscar estado de cartera existente o crear uno nuevo
            var carteraEstado = await _db.CarteraEstados
                .FirstOrDefaultAsync(c => c.ClienteId == id);

            if (carteraEstado == null)
            {
                // Crear estado por defecto si no existe - USAR DateTime.UtcNow para PostgreSQL
                carteraEstado = new CarteraEstado
                {
                    ClienteId = id,
                    Estado = "Vigente",
                    FechaModificacion = DateTime.UtcNow, // CAMBIADO: DateTime.Now -> DateTime.UtcNow
                    UsuarioModificacion = User.Identity?.Name ?? "Sistema"
                };
                _db.CarteraEstados.Add(carteraEstado);
                await _db.SaveChangesAsync();
            }

            var model = new Audicob.Models.CarteraEstadoUpdateDto
            {
                Id = carteraEstado.Id,
                ClienteId = cliente.Id,
                ClienteNombre = cliente.Nombre,
                Estado = carteraEstado.Estado,
                EstadoActual = carteraEstado.Estado,
                Comentario = carteraEstado.Comentario
            };

            ViewBag.FechaModificacion = carteraEstado.FechaModificacion;
            return View(model);
        }

        // POST: Supervisor/ActualizarEstadoCartera (versión original mantenida)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarEstadoCartera(Audicob.Models.CarteraEstadoUpdateDto model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var carteraEstado = await _db.CarteraEstados
                        .FirstOrDefaultAsync(c => c.ClienteId == model.ClienteId);

                    var user = await _userManager.GetUserAsync(User);
                    var userName = user?.FullName ?? User.Identity?.Name ?? "Sistema";

                    if (carteraEstado != null)
                    {
                        // Actualizar el estado de cartera
                        carteraEstado.Estado = model.Estado;
                        carteraEstado.Comentario = model.Comentario;
                        carteraEstado.FechaModificacion = DateTime.UtcNow;
                        carteraEstado.UsuarioModificacion = userName;

                        _db.CarteraEstados.Update(carteraEstado);
                    }

                    await _db.SaveChangesAsync();

                    // Preparar la respuesta de éxito
                    var cliente = await _db.Clientes.FindAsync(model.ClienteId);
                    ViewBag.OperacionExitosa = true;
                    ViewBag.ClienteNombre = cliente?.Nombre ?? "Cliente";
                    ViewBag.FechaModificacion = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm");

                    TempData["Success"] = "Estado de cartera actualizado exitosamente.";

                    // Actualizar el modelo para la vista
                    model.EstadoActual = model.Estado;
                    return View("DetalleCartera", model);
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Error al actualizar el estado: {ex.Message}";
                    ModelState.AddModelError("", "Error al guardar los cambios.");
                }
            }

            // Si hay errores, recargar los datos del cliente
            var clienteReload = await _db.Clientes.FindAsync(model.ClienteId);
            if (clienteReload != null)
            {
                model.ClienteNombre = clienteReload.Nombre;
                model.EstadoActual = (await _db.CarteraEstados
                    .FirstOrDefaultAsync(c => c.ClienteId == model.ClienteId))?.Estado ?? "Vigente";
            }

            return View("DetalleCartera", model);
        }
        // GET: Supervisor/DescartarCambios/5 (versión original mantenida)
        public async Task<IActionResult> DescartarCambios(int id)
        {
            // Simplemente redirige de vuelta al detalle sin guardar cambios
            TempData["Info"] = "Cambios descartados correctamente.";
            return RedirectToAction("DetalleCartera", new { id });
        }

        // ==========================================================
        // RESTANTE DEL CÓDIGO EXISTENTE (NO MODIFICADO)
        // ==========================================================

        private async Task<Cliente?> TryGetClienteAsync(int clienteId)
        {
            return await _db.Clientes
                .Include(c => c.LineaCredito)
                .FirstOrDefaultAsync(c => c.Id == clienteId);
        }

        //HU11: Gestionar asignación de asesores
        // GET: Mostrar la lista de asesores asignados
        public async Task<IActionResult> GestionAsignacion()
        {
            var asesores = await _db.AsesoresAsignados.ToListAsync();
            return View(asesores);
        }

        // POST: Reportar la asignación (copiar los datos y registrar en la tabla ReportesAsignacion)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportarAsignacion()
        {
            var asesores = await _db.AsesoresAsignados.ToListAsync();

            if (!asesores.Any())
            {
                TempData["Error"] = "No hay datos para reportar.";
                return RedirectToAction("GestionAsignacion");
            }

            var user = await _userManager.GetUserAsync(User);

            foreach (var a in asesores)
            {
                var reporte = new ReporteAsignacion
                {
                    AsesorNombre = a.AsesorNombre,
                    CantidadCarteras = a.CantidadCarteras,
                    MontoTotal = a.MontoTotal,
                    CantidadCuentas = a.CantidadCuentas,
                    Estado = a.Estado,
                    Responsable = user.FullName ?? user.UserName,
                    FechaRegistro = DateTime.UtcNow
                };

                _db.ReportesAsignacion.Add(reporte);
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Asignaciones reportadas exitosamente.";
            return RedirectToAction("GestionAsignacion");
        }

        // HU11 Ver reportes anteriores
        public async Task<IActionResult> ReportesAnteriores()
        {
            var reportes = await _db.ReportesAsignacion
                .OrderByDescending(r => r.FechaRegistro)
                .ToListAsync();

            return PartialView("_ReportesAnteriores", reportes);
        }

        // HU11: Exportar reportes a PDF y Excel
        public async Task<IActionResult> ExportarPDF()
        {
            var asesores = await _db.AsesoresAsignados.ToListAsync();

            using (var memoryStream = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4);
                PdfWriter.GetInstance(doc, memoryStream);
                doc.Open();

                Paragraph title = new Paragraph("Reporte de Asignación de Carteras\n\n")
                {
                    Alignment = Element.ALIGN_CENTER
                };
                doc.Add(title);

                PdfPTable table = new PdfPTable(5);
                table.WidthPercentage = 100;
                table.AddCell("Asesor");
                table.AddCell("Cantidad Carteras");
                table.AddCell("Monto Total");
                table.AddCell("Cantidad Cuentas");
                table.AddCell("Estado");

                foreach (var a in asesores)
                {
                    table.AddCell(a.AsesorNombre);
                    table.AddCell(a.CantidadCarteras.ToString());
                    table.AddCell(a.MontoTotal.ToString("C"));
                    table.AddCell(a.CantidadCuentas.ToString());
                    table.AddCell(a.Estado);
                }

                doc.Add(table);
                doc.Close();

                byte[] bytes = memoryStream.ToArray();
                return File(bytes, "application/pdf", "ReporteAsignacion.pdf");
            }
        }

        public async Task<IActionResult> ExportarExcel()
        {
            var asesores = await _db.AsesoresAsignados.ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Asignaciones");
                worksheet.Cell(1, 1).Value = "Asesor";
                worksheet.Cell(1, 2).Value = "Cantidad Carteras";
                worksheet.Cell(1, 3).Value = "Monto Total";
                worksheet.Cell(1, 4).Value = "Cantidad Cuentas";
                worksheet.Cell(1, 5).Value = "Estado";

                int row = 2;
                foreach (var a in asesores)
                {
                    worksheet.Cell(row, 1).Value = a.AsesorNombre;
                    worksheet.Cell(row, 2).Value = a.CantidadCarteras;
                    worksheet.Cell(row, 3).Value = a.MontoTotal;
                    worksheet.Cell(row, 4).Value = a.CantidadCuentas;
                    worksheet.Cell(row, 5).Value = a.Estado;
                    row++;
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ReporteAsignacion.xlsx");
                }
            }
        }

        // ==========================================================
        // HU-29: FILTRADOR DE ESTADO DE MORA
        // ==========================================================

        /// <summary>
        /// Muestra la vista del filtrador avanzado de mora
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> FiltrarMora()
        {
            var vm = new FiltroMoraViewModel();

            // Cargar filtros guardados del usuario actual
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                vm.FiltrosGuardados = await _db.FiltrosGuardados
                    .Where(f => f.UserId == user.Id)
                    .OrderByDescending(f => f.FechaCreacion)
                    .ToListAsync();
            }

            return View(vm);
        }

        /// <summary>
        /// Procesa el filtrado de mora con criterios específicos
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FiltrarMora(FiltroMoraViewModel modelo, string? exportar)
        {
            var startTime = DateTime.Now;

            try
            {
                // CORRECCIÓN: Actualizar registros con EstadoAdmin NULL
                await CorregirEstadoAdminNull();

                // Si es una exportación, redirigir al método correspondiente
                if (!string.IsNullOrEmpty(exportar))
                {
                    return ExportarFiltroMora(modelo);
                }

                // Iniciar con clientes que tienen datos válidos
                var query = _db.Clientes
                    .Where(c => !string.IsNullOrEmpty(c.Nombre) && !string.IsNullOrEmpty(c.Documento))
                    .AsQueryable();

                // Aplicar filtros por rango de días en mora
                if (modelo.RangoDiasDesde.HasValue || modelo.RangoDiasHasta.HasValue)
                {
                    if (modelo.RangoDiasDesde.HasValue)
                    {
                        var fechaMaxima = DateTime.UtcNow.AddDays(-modelo.RangoDiasDesde.Value);
                        query = query.Where(c => c.FechaActualizacion <= fechaMaxima);
                    }
                    if (modelo.RangoDiasHasta.HasValue)
                    {
                        var fechaMinima = DateTime.UtcNow.AddDays(-modelo.RangoDiasHasta.Value);
                        query = query.Where(c => c.FechaActualizacion >= fechaMinima);
                    }
                }

                // Filtrar por estado de mora
                if (!string.IsNullOrEmpty(modelo.EstadoMora))
                {
                    query = query.Where(c => c.EstadoMora == modelo.EstadoMora);
                }

                // Filtrar por monto de deuda
                if (modelo.MontoDesde.HasValue)
                {
                    query = query.Where(c => c.DeudaTotal >= modelo.MontoDesde.Value);
                }

                if (modelo.MontoHasta.HasValue)
                {
                    query = query.Where(c => c.DeudaTotal <= modelo.MontoHasta.Value);
                }

                // Ejecutar consulta y mapear resultados
                var clientes = await query.ToListAsync();

                modelo.ResultadosFiltrados = clientes.Select(c => {
                    var diasEnMora = (DateTime.UtcNow - c.FechaActualizacion).Days;
                    var clienteInfo = new ClienteMoraInfo
                    {
                        ClienteId = c.Id,
                        Nombre = c.Nombre,
                        Documento = c.Documento,
                        DeudaTotal = c.DeudaTotal,
                        EstadoMora = c.EstadoMora,
                        IngresosMensuales = c.IngresosMensuales,
                        DiasEnMora = diasEnMora > 0 ? diasEnMora : 0,
                        MontoEnMora = c.DeudaTotal,
                        TipoCliente = "Estándar" // Por defecto, podrías calcularlo según tus reglas de negocio
                    };

                    clienteInfo.CalcularPrioridad();
                    return clienteInfo;
                }).OrderByDescending(c => c.DiasEnMora).ThenByDescending(c => c.MontoEnMora).ToList();

                // Actualizar metadatos
                modelo.TotalRegistros = modelo.ResultadosFiltrados.Count;
                modelo.TiempoRespuesta = DateTime.Now - startTime;

                // Guardar filtro si se solicita
                if (modelo.GuardarFiltro && !string.IsNullOrEmpty(modelo.NombreFiltroGuardado))
                {
                    await GuardarFiltro(modelo);
                    TempData["Success"] = $"Filtro '{modelo.NombreFiltroGuardado}' guardado correctamente.";
                }

                // Recargar filtros guardados
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    modelo.FiltrosGuardados = await _db.FiltrosGuardados
                        .Where(f => f.UserId == user.Id)
                        .OrderByDescending(f => f.FechaCreacion)
                        .ToListAsync();
                }

                TempData["Success"] = $"Filtrado completado: {modelo.TotalRegistros} clientes encontrados en {modelo.TiempoRespuesta.TotalMilliseconds:F0}ms";

                return View(modelo);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al procesar el filtro: {ex.Message}";
                return View(modelo);
            }
        }
        // GET: /Supervisor/MetricasDesempeno
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Supervisor/MetricasDesempeno/MetricasDesempeno
        public IActionResult MetricasDesempeno()
        {
            return View();
        }

        // GET: /Supervisor/MetricasDesempeno/VistaMetricas
        public IActionResult VistaMetricas()
        {
            return View();
        }

        // GET: /Supervisor/MetricasDesempeno/PanelMetricas
        public IActionResult PanelMetricas()
        {
            return View();
        }
    

        /// <summary>
        /// Carga un filtro guardado
        /// </summary>
        /// 
        public async Task<IActionResult> DescargarReportePDF()
        {
            try
            {
                // Obtén los datos para el reporte desde la base de datos
                var clientes = await _db.Clientes.ToListAsync();

                using (var memoryStream = new MemoryStream())
                {
                    // Crear documento PDF
                    Document doc = new Document(PageSize.A4);
                    PdfWriter.GetInstance(doc, memoryStream);
                    doc.Open();

                    // Agregar título al reporte
                    Paragraph title = new Paragraph("Reporte de Morosidad\n\n")
                    {
                        Alignment = Element.ALIGN_CENTER
                    };
                    doc.Add(title);

                    // Agregar datos del reporte (estadísticas generales)
                    string statsText = $"Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}\n" +
                                       $"Total de clientes: {clientes.Count}\n" +
                                       $"Clientes al día: {clientes.Count(c => c.EstadoMora == "Al día")}\n" +
                                       $"Clientes en mora: {clientes.Count(c => c.EstadoMora != "Al día")}\n" +
                                       $"Monto total deuda: S/ {clientes.Sum(c => c.DeudaTotal):N2}\n\n";

                    Paragraph stats = new Paragraph(statsText);
                    doc.Add(stats);

                    // Agregar tabla con los clientes
                    PdfPTable table = new PdfPTable(5);
                    table.WidthPercentage = 100;
                    table.AddCell("Cliente");
                    table.AddCell("Documento");
                    table.AddCell("Estado Mora");
                    table.AddCell("Deuda Total");
                    table.AddCell("Días en Mora");

                    foreach (var cliente in clientes.OrderByDescending(c => c.DeudaTotal))
                    {
                        table.AddCell(cliente.Nombre);
                        table.AddCell(cliente.Documento);
                        table.AddCell(cliente.EstadoMora);
                        table.AddCell($"S/ {cliente.DeudaTotal:N2}");
                        table.AddCell(((DateTime.UtcNow - cliente.FechaActualizacion).Days).ToString());
                    }

                    doc.Add(table);
                    doc.Close();

                    byte[] bytes = memoryStream.ToArray();
                    return File(bytes, "application/pdf", $"ReporteMorosidad_{DateTime.Now:yyyyMMdd}.pdf");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar el reporte en PDF: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        public async Task<IActionResult> DescargarReporteExcel()
        {
            try
            {
                // Obtén los datos para el reporte desde la base de datos
                var clientes = await _db.Clientes.ToListAsync();

                using (var workbook = new XLWorkbook())
                {
                    // Crear una hoja de trabajo
                    var worksheet = workbook.Worksheets.Add("Reporte Mora");

                    // Agregar los encabezados de la tabla
                    worksheet.Cell(1, 1).Value = "Cliente";
                    worksheet.Cell(1, 2).Value = "Documento";
                    worksheet.Cell(1, 3).Value = "Estado Mora";
                    worksheet.Cell(1, 4).Value = "Deuda Total";
                    worksheet.Cell(1, 5).Value = "Días en Mora";

                    // Agregar los datos a la tabla
                    int row = 2;
                    foreach (var cliente in clientes.OrderByDescending(c => c.DeudaTotal))
                    {
                        worksheet.Cell(row, 1).Value = cliente.Nombre;
                        worksheet.Cell(row, 2).Value = cliente.Documento;
                        worksheet.Cell(row, 3).Value = cliente.EstadoMora;
                        worksheet.Cell(row, 4).Value = cliente.DeudaTotal;
                        worksheet.Cell(row, 5).Value = (DateTime.UtcNow - cliente.FechaActualizacion).Days;
                        row++;
                    }

                    // Ajustar el tamaño de las columnas
                    worksheet.ColumnsUsed().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        // Guardar el archivo Excel en un stream de memoria
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ReporteMorosidad.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar el reporte en Excel: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CargarFiltro(int filtroId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }
                
                var filtro = await _db.FiltrosGuardados
                    .FirstOrDefaultAsync(f => f.Id == filtroId && f.UserId == user.Id);

                if (filtro == null)
                {
                    return Json(new { success = false, message = "Filtro no encontrado" });
                }

                // Deserializar la configuración JSON
                var configuracion = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(filtro.ConfiguracionJson);

                return Json(new { success = true, configuracion = configuracion });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Exporta los resultados del filtro a Excel
        /// </summary>
        [HttpPost]
        public IActionResult ExportarFiltroMora(FiltroMoraViewModel modelo)
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Reporte Mora");
                    
                    // Headers
                    worksheet.Cell(1, 1).Value = "Cliente";
                    worksheet.Cell(1, 2).Value = "Documento";
                    worksheet.Cell(1, 3).Value = "Estado Mora";
                    worksheet.Cell(1, 4).Value = "Deuda Total";
                    worksheet.Cell(1, 5).Value = "Días en Mora";
                    worksheet.Cell(1, 6).Value = "Prioridad";
                    worksheet.Cell(1, 7).Value = "Ingresos Mensuales";
                    
                    // Data
                    for (int i = 0; i < modelo.ResultadosFiltrados.Count; i++)
                    {
                        var cliente = modelo.ResultadosFiltrados[i];
                        worksheet.Cell(i + 2, 1).Value = cliente.Nombre;
                        worksheet.Cell(i + 2, 2).Value = cliente.Documento;
                        worksheet.Cell(i + 2, 3).Value = cliente.EstadoMora;
                        worksheet.Cell(i + 2, 4).Value = cliente.DeudaTotal;
                        worksheet.Cell(i + 2, 5).Value = cliente.DiasEnMora;
                        worksheet.Cell(i + 2, 6).Value = cliente.NivelPrioridad;
                        worksheet.Cell(i + 2, 7).Value = cliente.IngresosMensuales;
                    }
                    
                    worksheet.ColumnsUsed().AdjustToContents();
                    
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "FiltroMora.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al exportar: {ex.Message}";
                return RedirectToAction("FiltrarMora");
            }
        }

        // ==========================================================
        // MÉTODOS AUXILIARES PARA HU-29
        // ==========================================================

        /// <summary>
        /// Guarda un filtro para uso futuro
        /// </summary>
        private async Task GuardarFiltro(FiltroMoraViewModel modelo)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return;

                var configuracion = new
                {
                    RangoDiasDesde = modelo.RangoDiasDesde,
                    RangoDiasHasta = modelo.RangoDiasHasta,
                    TipoCliente = modelo.TipoCliente,
                    MontoDesde = modelo.MontoDesde,
                    MontoHasta = modelo.MontoHasta,
                    EstadoMora = modelo.EstadoMora
                };

                var filtroGuardado = new FiltroGuardado
                {
                    Nombre = modelo.NombreFiltroGuardado ?? "Filtro sin nombre",
                    UserId = user.Id,
                    ConfiguracionJson = System.Text.Json.JsonSerializer.Serialize(configuracion),
                    FechaCreacion = DateTime.UtcNow
                };

                _db.FiltrosGuardados.Add(filtroGuardado);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't fail the main operation
                Console.WriteLine($"Error al guardar filtro: {ex.Message}");
            }
        }

        // ==========================================================
        // MÉTODOS AUXILIARES
        // ==========================================================

        /// <summary>
        /// Corrige valores NULL en EstadoAdmin
        /// </summary>
        private async Task CorregirEstadoAdminNull()
        {
            // Corregir EstadoAdmin NULL
            var clientesConNull = await _db.Clientes
                .Where(c => c.EstadoAdmin == null || c.EstadoAdmin == "")
                .ToListAsync();

            if (clientesConNull.Any())
            {
                foreach (var cliente in clientesConNull)
                {
                    cliente.EstadoAdmin = "Pendiente";
                }
                await _db.SaveChangesAsync();
            }
            
            // Eliminar clientes sin nombre o documento (datos incompletos)
            var clientesVacios = await _db.Clientes
                .Where(c => string.IsNullOrEmpty(c.Nombre) || string.IsNullOrEmpty(c.Documento))
                .ToListAsync();
                
            if (clientesVacios.Any())
                {
                _db.Clientes.RemoveRange(clientesVacios);
                await _db.SaveChangesAsync();
            }
        }

        // ==========================================================
        // REPORTE DE MOROSIDAD
        // ==========================================================

        /// <summary>
        /// Muestra el reporte completo de morosidad
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ReporteMora()
        {
            try
            {
                // CORRECCIÓN: Actualizar registros con EstadoAdmin NULL
                await CorregirEstadoAdminNull();
                
                var reporteViewModel = new ReporteMoraViewModel();
                
                // Obtener todos los clientes con información de mora (solo con datos válidos)
                var clientes = await _db.Clientes
                    .Where(c => !string.IsNullOrEmpty(c.Nombre) && !string.IsNullOrEmpty(c.Documento))
                    .ToListAsync();
                
                // Estadísticas generales
                reporteViewModel.TotalClientes = clientes.Count;
                reporteViewModel.ClientesAlDia = clientes.Count(c => c.EstadoMora == "Al día");
                reporteViewModel.ClientesMoraTemprana = clientes.Count(c => c.EstadoMora == "Temprana");
                reporteViewModel.ClientesMoraModerada = clientes.Count(c => c.EstadoMora == "Moderada");
                reporteViewModel.ClientesMoraGrave = clientes.Count(c => c.EstadoMora == "Grave");
                reporteViewModel.ClientesMoraCritica = clientes.Count(c => c.EstadoMora == "Crítica");
                
                // Montos por estado
                reporteViewModel.MontoTotalDeuda = clientes.Sum(c => c.DeudaTotal);
                reporteViewModel.MontoAlDia = clientes.Where(c => c.EstadoMora == "Al día").Sum(c => c.DeudaTotal);
                reporteViewModel.MontoMoraTemprana = clientes.Where(c => c.EstadoMora == "Temprana").Sum(c => c.DeudaTotal);
                reporteViewModel.MontoMoraModerada = clientes.Where(c => c.EstadoMora == "Moderada").Sum(c => c.DeudaTotal);
                reporteViewModel.MontoMoraGrave = clientes.Where(c => c.EstadoMora == "Grave").Sum(c => c.DeudaTotal);
                reporteViewModel.MontoMoraCritica = clientes.Where(c => c.EstadoMora == "Crítica").Sum(c => c.DeudaTotal);
                
                // Clientes con mayor deuda
                reporteViewModel.ClientesMayorDeuda = clientes
                    .OrderByDescending(c => c.DeudaTotal)
                    .Take(10)
                    .Select(c => new ClienteReporteMora
                    {
                        Nombre = c.Nombre,
                        Documento = c.Documento,
                        EstadoMora = c.EstadoMora,
                        DeudaTotal = c.DeudaTotal,
                        DiasEnMora = (DateTime.UtcNow - c.FechaActualizacion).Days,
                        IngresosMensuales = c.IngresosMensuales
                    }).ToList();
                
                // Evolución mensual (últimos 6 meses)
                reporteViewModel.EvolucionMensual = new List<EvolucionMoraMensual>();
                for (int i = 5; i >= 0; i--)
                {
                    var fecha = DateTime.UtcNow.AddMonths(-i);
                    var fechaInicio = new DateTime(fecha.Year, fecha.Month, 1);
                    var fechaFin = fechaInicio.AddMonths(1).AddDays(-1);
                    
                    // Simular datos históricos (en un escenario real, tendrías tabla de históricos)
                    var clientesEnFecha = clientes.Where(c => c.FechaActualizacion <= fechaFin).ToList();
                    
                    reporteViewModel.EvolucionMensual.Add(new EvolucionMoraMensual
                    {
                        Mes = fecha.ToString("yyyy-MM"),
                        NombreMes = fecha.ToString("MMMM yyyy"),
                        TotalClientes = clientesEnFecha.Count,
                        ClientesEnMora = clientesEnFecha.Count(c => c.EstadoMora != "Al día"),
                        MontoTotalMora = clientesEnFecha.Where(c => c.EstadoMora != "Al día").Sum(c => c.DeudaTotal),
                        PorcentajeMora = clientesEnFecha.Count > 0 
                            ? (decimal)clientesEnFecha.Count(c => c.EstadoMora != "Al día") / clientesEnFecha.Count * 100 
                            : 0
                    });
                }
                
                return View(reporteViewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar el reporte: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        /// <summary>
        /// Exporta el reporte de morosidad a PDF
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ExportarReporteMoraPdf()
        {
            try
            {
                var clientes = await _db.Clientes.ToListAsync();
                
                using (var memoryStream = new MemoryStream())
                {
                    Document doc = new Document(PageSize.A4);
                    PdfWriter.GetInstance(doc, memoryStream);
                    doc.Open();

                    // Título
                    Paragraph title = new Paragraph("Reporte de Morosidad\n\n")
                    {
                        Alignment = Element.ALIGN_CENTER
                    };
                    doc.Add(title);

                    // Estadísticas generales
                    string statsText = $"Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}\n" +
                                     $"Total de clientes: {clientes.Count}\n" +
                                     $"Clientes al día: {clientes.Count(c => c.EstadoMora == "Al día")}\n" +
                                     $"Clientes en mora: {clientes.Count(c => c.EstadoMora != "Al día")}\n" +
                                     $"Monto total deuda: S/ {clientes.Sum(c => c.DeudaTotal):N2}\n\n";
                    
                    Paragraph stats = new Paragraph(statsText);
                    doc.Add(stats);

                    // Tabla de clientes
                    PdfPTable table = new PdfPTable(5);
                    table.WidthPercentage = 100;
                    table.AddCell("Cliente");
                    table.AddCell("Documento");
                    table.AddCell("Estado Mora");
                    table.AddCell("Deuda Total");
                    table.AddCell("Días en Mora");

                    foreach (var cliente in clientes.OrderByDescending(c => c.DeudaTotal))
                    {
                        table.AddCell(cliente.Nombre);
                        table.AddCell(cliente.Documento);
                        table.AddCell(cliente.EstadoMora);
                        table.AddCell($"S/ {cliente.DeudaTotal:N2}");
                        table.AddCell(((DateTime.UtcNow - cliente.FechaActualizacion).Days).ToString());
                    }

                    doc.Add(table);
                    doc.Close();

                    byte[] bytes = memoryStream.ToArray();
                    return File(bytes, "application/pdf", $"ReporteMorosidad_{DateTime.Now:yyyyMMdd}.pdf");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al exportar el reporte: {ex.Message}";
                return RedirectToAction("ReporteMora");
            }
        }
    }

    // ==========================================================
    // CLASES PARA LAS NUEVAS FUNCIONALIDADES
    // ==========================================================

    public class BuscarClienteRequest
    {
        public string DNI { get; set; }
    }

    public class ActualizarEstadoRequest
    {
        public int ClienteId { get; set; }
        public string NuevoEstado { get; set; }
    }

    public class DescartarCambiosRequest
    {
        public int ClienteId { get; set; }
    }
}
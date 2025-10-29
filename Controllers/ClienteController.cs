using Audicob.Data;
using Audicob.Models;
using Audicob.Models.ViewModels.Cliente;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

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

        //HU 13Abonar cliente

        // Asegúrate de tener estos using


        // Acción: DetalleDeudaTotal (muestra la lista y setea ViewBag)
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
                    Nombre = string.Empty, // depende de tu modelo ApplicationUser
                    Correo = user.Email,
                    Telefono = string.Empty,       // 👈 evita null
                    Direccion = string.Empty,      // 👈 evita null
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



    }
}
    using Audicob.Models;
    using Audicob.Models.ViewModels.Admin;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;  // Asegúrate de tener esto
    using System.Linq;
    using System.Threading.Tasks;
    using Audicob.Data;


namespace Audicob.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;

        public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
        }

        // Acción para cargar el Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var vm = new AdminDashboardViewModel();

            try
            {
                // Cargar todos los usuarios asincrónicamente usando ToListAsync()
                var allUsers = await _userManager.Users.ToListAsync();
                vm.TotalUsuarios = allUsers.Count;

                // Obtener los roles disponibles de forma asincrónica usando ToListAsync()
                var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                vm.RolesDisponibles = roles;

                // Inicializar el diccionario para contar usuarios por rol
                vm.UsuariosPorRol = new Dictionary<string, int>();

                // Cargar la cantidad de usuarios por cada rol
                foreach (var role in roles)
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                    vm.UsuariosPorRol[role] = usersInRole.Count;
                }

                // Calcular los usuarios activos, suspendidos y nuevos esta semana
                vm.CalcularUsuariosActivos(allUsers);
                vm.CalcularUsuariosNuevosEstaSemana(allUsers);
                vm.CalcularUsuariosSuspendidos(allUsers);

            }
            catch (Exception ex)
            {
                // Manejo de errores básicos para detectar problemas con la carga de datos
                ModelState.AddModelError("", "Ocurrió un error al cargar la información. Intenta nuevamente.");
            }

            return View(vm);
        }

        // =============================================
        // HU-20: REINTEGRAR CLIENTE
        // =============================================

        //GET
        public IActionResult ReintegrarCliente(string documento = null)
        {
            var vm = new ReintegrarClienteViewModel();

            // Si se pasó un documento, mostrar los datos del cliente reintegrado
            if (!string.IsNullOrEmpty(documento))
            {
                var cliente = _db.Clientes.FirstOrDefault(c => c.Documento == documento);
                if (cliente != null)
                {
                    vm.Documento = cliente.Documento;
                    vm.Nombre = cliente.Nombre;
                    vm.Estado = cliente.Estado;
                    vm.ClienteEncontrado = true;
                }
            }

            // Cargar auditorías (todas o filtradas por cliente)
            vm.Auditorias = _db.HistorialAuditorias
                .OrderByDescending(a => a.FechaAccion)
                .ToList();

            return View(vm);
        }


        // POST: Buscar cliente por documento
        [HttpPost]
        public async Task<IActionResult> ReintegrarCliente(ReintegrarClienteViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Documento))
            {
                ModelState.AddModelError("", "Debe ingresar un documento de cliente.");
                return View(vm);
            }

            var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Documento == vm.Documento);

            if (cliente == null)
            {
                vm.ClienteEncontrado = false;
                ModelState.AddModelError("", "No se encontró un cliente con ese documento.");
                return View(vm);
            }

            vm.ClienteEncontrado = true;
            vm.Nombre = cliente.Nombre;
            vm.Estado = cliente.Estado;

            vm.Historial = await _db.HistorialCreditos
                .Where(h => h.DniCliente == vm.Documento)
                .Select(h => new ReintegrarClienteViewModel.HistorialCreditoItem
                {
                    MontoOperacion = h.MontoOperacion,
                    TipoOperacion = h.TipoOperacion,
                    FechaOperacion = h.FechaOperacion,
                    Observaciones = h.Observaciones
                })
                .ToListAsync();

            // ✅ Cargar historial de auditoría para ese cliente
            vm.Auditorias = _db.HistorialAuditorias
                .OrderByDescending(a => a.FechaAccion)
                .ToList();

            return View(vm);
        }

        // POST: Confirmar reintegración del cliente
        [HttpPost]
        public async Task<IActionResult> ReintegrarClienteConfirmado(string documento)
        {
            if (string.IsNullOrEmpty(documento))
            {
                TempData["Error"] = "Documento inválido.";
                return RedirectToAction("ReintegrarCliente");
            }

            var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Documento == documento);
            if (cliente == null)
            {
                TempData["Error"] = "No se encontró el cliente.";
                return RedirectToAction("ReintegrarCliente");
            }

            // Actualizar estado del cliente a Activo
            cliente.Estado = "Activo";
            _db.Clientes.Update(cliente);

            // Registrar acción en HistorialAuditorias
            var auditoria = new HistorialAuditoria
            {
                NombreCliente = cliente.Nombre,
                DniCliente = cliente.Documento,
                Accion = "Reintegrado por el Gerente General",
                FechaAccion = DateTime.UtcNow
            };

            _db.HistorialAuditorias.Add(auditoria);
            await _db.SaveChangesAsync();

            TempData["Exito"] = "Reintegración exitosa.";
            return RedirectToAction("ReintegrarCliente", new { documento = cliente.Documento });
        }

    }
}
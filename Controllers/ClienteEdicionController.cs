using Audicob.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Audicob.Data;

namespace Audicob.Controllers
{
    public class ClienteEdicionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClienteEdicionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Index: lista de clientes (y datos para el select)
        public async Task<IActionResult> Index(string criterio, string tipoBusqueda)
        {
            var query = _context.Clientes.AsQueryable();

            if (!string.IsNullOrWhiteSpace(criterio))
            {
                switch (tipoBusqueda)
                {
                    case "id":
                        query = query.Where(c => c.Id.ToString().Contains(criterio));
                        break;
                    case "documento":
                        query = query.Where(c => c.Documento.Contains(criterio));
                        break;
                    case "nombre":
                        query = query.Where(c => c.Nombre.Contains(criterio));
                        break;
                }
            }

            var clientes = await query.ToListAsync();
            return View(clientes);
        }

        // Devuelve partial con detalle (AJAX)
        public async Task<IActionResult> Detalle(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return Content("No se encontró el cliente");

            return PartialView("_DetalleCliente", cliente);
        }

        // GET: Editar (form)
        public async Task<IActionResult> Editar(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return RedirectToAction("Index");

            return View(cliente);
        }

        // POST: Editar (guardar)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, Cliente cliente)
        {
            if (id != cliente.Id) return RedirectToAction("Index");

            if (ModelState.IsValid)
            {
                cliente.FechaActualizacion = DateTime.UtcNow;
                _context.Update(cliente);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = "✅ Datos actualizados correctamente";
                return RedirectToAction("Index");
            }

            return View(cliente);
        }
    }
}

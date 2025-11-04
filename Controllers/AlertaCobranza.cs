using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Audicob.Data;
using Audicob.Models;
using System.Threading.Tasks;
using System;

namespace Audicob.Controllers
{
    public class AlertasCobranzaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AlertasCobranzaController(ApplicationDbContext context)
        {
            _context = context;
        }

        // LISTAR ALERTAS
        public async Task<IActionResult> Index()
        {
            var alertas = await _context.AlertasCobranza
                .Include(a => a.Cliente) // cargar cliente
                .ToListAsync();

            return View(alertas);
        }

        // MOSTRAR FORMULARIO CREAR ALERTA PARA CLIENTE NUEVO
        public IActionResult Crear()
        {
            var alerta = new AlertaCobranza
            {
                FechaLimite = DateTime.Today.AddDays(7) // Por defecto 7 días
            };
            return View(alerta);
        }

        // GUARDAR ALERTA Y CLIENTE NUEVO
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(AlertaCobranza alerta, string ClienteNombre)
        {
            if (string.IsNullOrWhiteSpace(ClienteNombre))
            {
                TempData["Mensaje"] = "❌ El nombre del cliente es obligatorio.";
                return View(alerta);
            }

            if (!ModelState.IsValid)
            {
                return View(alerta);
            }

            // 1️⃣ Crear cliente nuevo
            var cliente = new Cliente { Nombre = ClienteNombre };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync(); // espera a generar Id

            // 2️⃣ Asignar ClienteId a la alerta
            alerta.ClienteId = cliente.Id;
            alerta.Estado = "Pendiente";
            alerta.FechaCreacion = DateTime.UtcNow;

            _context.AlertasCobranza.Add(alerta);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "✅ Alerta generada correctamente.";

            // 3️⃣ Redirigir a Index donde se cargan alertas con Include
            return RedirectToAction(nameof(Index));
        }

        // MARCAR ALERTA COMO ATENDIDA
        public async Task<IActionResult> Atender(int id)
        {
            var alerta = await _context.AlertasCobranza.FindAsync(id);
            if (alerta == null) return NotFound();

            alerta.Estado = "Atendida";
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "✅ Alerta atendida.";
            return RedirectToAction(nameof(Index));
        }
    }
}

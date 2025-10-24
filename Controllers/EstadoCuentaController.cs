using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Audicob.Data;
using iTextSharp.text;
using iTextSharp.text.pdf;

public class EstadoCuentaController : Controller
{
    private readonly ApplicationDbContext _context;

    public EstadoCuentaController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Página inicial con campo de búsqueda
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    // Buscar cliente por documento
    [HttpPost]
    public async Task<IActionResult> Buscar(string documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            TempData["Error"] = "Por favor, ingrese un número de documento.";
            return RedirectToAction("Index");
        }

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Documento == documento);

        if (cliente == null)
        {
            TempData["Error"] = "No se encontró un cliente con ese documento.";
            return RedirectToAction("Index");
        }

        var deudas = await _context.Deudas
            .Where(d => d.ClienteId == cliente.Id)
            .ToListAsync();

        var pagos = await _context.Pagos
            .Where(p => p.ClienteId == cliente.Id)
            .ToListAsync();

        var transacciones = await _context.Transacciones
            .Where(t => t.ClienteId == cliente.Id)
            .ToListAsync();

        ViewData["Deudas"] = deudas;
        ViewData["Pagos"] = pagos;
        ViewData["Transacciones"] = transacciones;

        return View("Index", cliente);
    }

    // 🔹 Descargar PDF completo (Deudas + Pagos + Transacciones)
    [HttpGet]
    public async Task<IActionResult> DescargarPDF(string documento)
    {
        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Documento == documento);
        if (cliente == null)
            return NotFound();

        var deudas = await _context.Deudas.Where(d => d.ClienteId == cliente.Id).ToListAsync();
        var pagos = await _context.Pagos.Where(p => p.ClienteId == cliente.Id).ToListAsync();
        var transacciones = await _context.Transacciones.Where(t => t.ClienteId == cliente.Id).ToListAsync();

        using (var ms = new MemoryStream())
        {
            var doc = new Document(PageSize.A4, 40, 40, 40, 40);
            var writer = PdfWriter.GetInstance(doc, ms);
            doc.Open();

            var titulo = new Paragraph($"ESTADO DE CUENTA - {cliente.Nombre}\n\n", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16));
            titulo.Alignment = Element.ALIGN_CENTER;
            doc.Add(titulo);

            doc.Add(new Paragraph($"Documento: {cliente.Documento}"));
            doc.Add(new Paragraph($"Fecha de actualización: {cliente.FechaActualizacion.ToShortDateString()}\n\n"));

            doc.Add(new Paragraph("== DEUDAS ==\n", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
            if (deudas.Any())
            {
                foreach (var d in deudas)
                {
                    doc.Add(new Paragraph($"Monto: S/ {d.Monto} | Intereses: S/ {d.Intereses} | Penalidad: S/ {d.PenalidadCalculada} | Total: S/ {d.TotalAPagar} | Vence: {d.FechaVencimiento.ToShortDateString()}"));
                }
            }
            else
                doc.Add(new Paragraph("No se registran deudas.\n"));

            doc.Add(new Paragraph("\n== PAGOS ==\n", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
            if (pagos.Any())
            {
                foreach (var p in pagos)
                {
                    doc.Add(new Paragraph($"Fecha: {p.Fecha.ToShortDateString()} | Monto: S/ {p.Monto} | Estado: {p.Estado}"));
                }
            }
            else
                doc.Add(new Paragraph("No hay pagos registrados.\n"));

            doc.Add(new Paragraph("\n== TRANSACCIONES ==\n", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
            if (transacciones.Any())
            {
                foreach (var t in transacciones)
                {
                    doc.Add(new Paragraph($"Transacción N° {t.NumeroTransaccion} | Fecha: {t.Fecha.ToShortDateString()} | Monto: S/ {t.Monto} | Estado: {t.Estado}"));
                }
            }
            else
                doc.Add(new Paragraph("No hay transacciones.\n"));

            doc.Close();
            writer.Close();

            return File(ms.ToArray(), "application/pdf", $"EstadoCuenta_{cliente.Documento}.pdf");
        }
    }

    // 🔹 Descargar solo PDF de Deudas
    [HttpGet]
    public async Task<IActionResult> DescargarDeudasPDF(string documento)
    {
        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Documento == documento);
        if (cliente == null)
            return NotFound();

        var deudas = await _context.Deudas.Where(d => d.ClienteId == cliente.Id).ToListAsync();

        using (var ms = new MemoryStream())
        {
            var doc = new Document(PageSize.A4, 40, 40, 40, 40);
            var writer = PdfWriter.GetInstance(doc, ms);
            doc.Open();

            var titulo = new Paragraph($"REPORTE DE DEUDAS - {cliente.Nombre}\n\n", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16));
            titulo.Alignment = Element.ALIGN_CENTER;
            doc.Add(titulo);

            doc.Add(new Paragraph($"Documento: {cliente.Documento}\n\n"));

            if (deudas.Any())
            {
                foreach (var d in deudas)
                {
                    doc.Add(new Paragraph($"Monto: S/ {d.Monto} | Intereses: S/ {d.Intereses} | Penalidad: S/ {d.PenalidadCalculada} | Total: S/ {d.TotalAPagar} | Vence: {d.FechaVencimiento.ToShortDateString()}"));
                }
            }
            else
                doc.Add(new Paragraph("No se registran deudas.\n"));

            doc.Close();
            writer.Close();

            return File(ms.ToArray(), "application/pdf", $"Deudas_{cliente.Documento}.pdf");
        }
    }
}

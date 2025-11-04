using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Audicob.Data;
using Audicob.Models;
using Audicob.Data.SeedData;
using Audicob.Services;

namespace Audicob
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 🔐 Configuración de conexión a PostgreSQL
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // Configuración del contexto de la base de datos con PostgreSQL
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // 🧪 Página de errores detallados en desarrollo
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            // 🔐 Configuración de Identity con roles y confirmación de cuenta
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 6;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // 🌐 MVC + Razor Pages con configuración JSON para API
            builder.Services.AddControllersWithViews()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.WriteIndented = true;
                    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                });

            builder.Services.AddRazorPages();

            // 📬 SERVICIOS DE NOTIFICACIONES
            builder.Services.AddScoped<INotificacionService, NotificacionService>();
            builder.Services.AddScoped<IPdfService, PdfService>();
            
            // 📊 SERVICIO DE MÉTRICAS DE DESEMPEÑO (AGREGADO)
            builder.Services.AddScoped<IMetricasService, MetricasService>();
            
            builder.Services.AddHostedService<RecordatorioHostedService>();

            // 🔧 Configuración del middleware de la aplicación
            var app = builder.Build();

            // 🌍 Configuración del pipeline HTTP para desarrollo y producción
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            // 🧭 Definir rutas principales y personalizadas
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Ruta personalizada para la vista de "Cobranza/Cliente"
            app.MapControllerRoute(
                name: "cobranza_cliente",
                pattern: "Cobranza/Cliente",
                defaults: new { controller = "Cobranza", action = "Cliente" }
            );

            // Ruta para el dashboard de validación
            app.MapControllerRoute(
                name: "validation",
                pattern: "Validation/{action=Index}/{id?}",
                defaults: new { controller = "Validation" }
            );

            app.MapRazorPages();

            // 🌱 Seed de roles y usuarios iniciales
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    // Inicializa los datos por defecto (roles, usuarios, etc.)
                    await SeedData.InitializeAsync(services);
                    
                    // 🔧 CORRECCIÓN: Actualizar registros con EstadoAdmin NULL
                    var db = services.GetRequiredService<ApplicationDbContext>();
                    await db.Database.ExecuteSqlRawAsync(
                        "UPDATE \"Clientes\" SET \"EstadoAdmin\" = 'Pendiente' WHERE \"EstadoAdmin\" IS NULL"
                    );
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Hubo un problema al inicializar los datos de la base de datos.");
                }
            }

            app.Run();
        }
    }
}
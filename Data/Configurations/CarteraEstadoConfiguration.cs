using Audicob.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Audicob.Data.Configurations
{
    public class CarteraEstadoConfiguration : IEntityTypeConfiguration<CarteraEstado>
    {
        public void Configure(EntityTypeBuilder<CarteraEstado> builder)
        {
            builder.HasKey(e => e.Id);
            
            // Relación con Cliente (uno a uno - un cliente tiene un estado de cartera)
            builder.HasOne(ce => ce.Cliente)
                  .WithOne() // Si no hay navegación inversa en Cliente
                  .HasForeignKey<CarteraEstado>(ce => ce.ClienteId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Índice único para evitar múltiples estados por cliente
            builder.HasIndex(ce => ce.ClienteId)
                  .IsUnique();
            
            // Configuración de propiedades
            builder.Property(ce => ce.Estado)
                  .IsRequired()
                  .HasMaxLength(50)
                  .HasDefaultValue("vigente");
            
            builder.Property(ce => ce.Comentario)
                  .HasMaxLength(500);
            
            builder.Property(ce => ce.FechaModificacion)
                  .IsRequired()
                  .HasDefaultValueSql("GETDATE()"); // Para SQL Server
            
            builder.Property(ce => ce.UsuarioModificacion)
                  .HasMaxLength(256);
            
            // Configuración del nombre de la tabla (opcional)
            builder.ToTable("CarteraEstados");
        }
    }
}
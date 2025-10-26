using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Audicob.Models;

namespace Audicob.Data.Configurations
{
    public class FiltroGuardadoConfiguration : IEntityTypeConfiguration<FiltroGuardado>
    {
        public void Configure(EntityTypeBuilder<FiltroGuardado> builder)
        {
            builder.HasKey(f => f.Id);
            
            builder.Property(f => f.Nombre)
                .IsRequired()
                .HasMaxLength(100);
            
            builder.Property(f => f.UserId)
                .IsRequired()
                .HasMaxLength(450);
            
            builder.Property(f => f.ConfiguracionJson)
                .IsRequired()
                .HasColumnType("text");
            
            builder.Property(f => f.FechaCreacion)
                .HasDefaultValueSql("NOW()");
            
            builder.Property(f => f.EsPredeterminado)
                .HasDefaultValue(false);
            
            // Relación con ApplicationUser
            builder.HasOne(f => f.Usuario)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Índices
            builder.HasIndex(f => new { f.UserId, f.Nombre })
                .IsUnique();
                
            builder.HasIndex(f => f.FechaCreacion);
        }
    }
}
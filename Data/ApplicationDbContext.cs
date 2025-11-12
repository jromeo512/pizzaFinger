using Microsoft.EntityFrameworkCore;
using FingerprintAttendanceSystem.Models;

namespace FingerprintAttendanceSystem.Data
{
    /// <summary>
    /// Contexto de base de datos para SQLite
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; } = null!;
        public DbSet<Empleado> Empleados { get; set; } = null!;
        public DbSet<Huella> Huellas { get; set; } = null!;
        public DbSet<Asistencia> Asistencias { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de Empleado
            modelBuilder.Entity<Empleado>(entity =>
            {
                entity.HasIndex(e => e.Codigo).IsUnique();
                entity.Property(e => e.LastSync).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.TieneHuella).HasDefaultValue(false);
            });

            // Configuración de Huella
            modelBuilder.Entity<Huella>(entity =>
            {
                entity.HasIndex(e => e.Codigo);
                entity.Property(e => e.FechaEnrolamiento).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.Activa).HasDefaultValue(true);

                entity.HasOne(h => h.Empleado)
                    .WithMany(e => e.Huellas)
                    .HasForeignKey(h => h.Codigo)
                    .HasPrincipalKey(e => e.Codigo)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuración de Asistencia
            modelBuilder.Entity<Asistencia>(entity =>
            {
                entity.HasIndex(e => new { e.Codigo, e.FechaMovimiento });
                entity.HasIndex(e => e.Sincronizado);
                entity.Property(e => e.FechaCreacion).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.Sincronizado).HasDefaultValue(false);
                entity.Property(e => e.IntentosSincronizacion).HasDefaultValue(0);

                entity.HasOne(a => a.Empleado)
                    .WithMany(e => e.Asistencias)
                    .HasForeignKey(a => a.Codigo)
                    .HasPrincipalKey(e => e.Codigo)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuración de Usuario
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.FechaCreacion).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.Activo).HasDefaultValue(true);
                entity.Property(e => e.Rol).HasDefaultValue("Operador");
            });

            // Datos iniciales - Usuario Admin por defecto
            // Password: Admin123! (debe cambiarse en producción)
            modelBuilder.Entity<Usuario>().HasData(
                new Usuario
                {
                    UsuarioId = 1,
                    Username = "admin",
                    PasswordHash = "$2a$11$8Z8Z8Z8Z8Z8Z8Z8Z8Z8Z8u.rC/OXlhZ5Y1EY1Y1Y1Y1Y1Y1Y1Y1Y1Y",
                    Rol = Roles.Administrador,
                    SucursalId = 1,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                }
            );
        }
    }
}

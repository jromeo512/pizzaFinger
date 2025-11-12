using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FingerprintAttendanceSystem.Models
{
    /// <summary>
    /// Modelo de Usuario para autenticación
    /// </summary>
    public class Usuario
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UsuarioId { get; set; }

        /// <summary>
        /// Nombre de usuario único
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Hash de la contraseña (BCrypt)
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Rol del usuario (Admin o Operador)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Rol { get; set; } = "Operador";

        /// <summary>
        /// ID de la sucursal a la que pertenece
        /// </summary>
        public int SucursalId { get; set; }

        /// <summary>
        /// Indica si el usuario está activo
        /// </summary>
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Fecha de creación del usuario
        /// </summary>
        public DateTime FechaCreacion { get; set; }

        /// <summary>
        /// Último inicio de sesión
        /// </summary>
        public DateTime? UltimoLogin { get; set; }
    }

    /// <summary>
    /// Roles disponibles en el sistema
    /// </summary>
    public static class Roles
    {
        public const string Administrador = "Admin";
        public const string Operador = "Operador";
    }
}

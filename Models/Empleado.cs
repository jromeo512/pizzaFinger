using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FingerprintAttendanceSystem.Models
{
    /// <summary>
    /// Modelo de Empleado - Cache local de la tabla EMPLEADOS de AppSheet
    /// </summary>
    public class Empleado
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// ID del empleado en AppSheet
        /// </summary>
        public string? IdEmpleado { get; set; }

        /// <summary>
        /// Nombre completo del empleado
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Código único del empleado (usado para relacionar con huella)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Codigo { get; set; } = string.Empty;

        /// <summary>
        /// Puesto del empleado
        /// </summary>
        [MaxLength(100)]
        public string? Puesto { get; set; }

        /// <summary>
        /// ID de la sucursal (SUCURSALID de AppSheet)
        /// </summary>
        public int SucursalId { get; set; }

        /// <summary>
        /// Fecha de ingreso del empleado
        /// </summary>
        public DateTime? FechaIngreso { get; set; }

        /// <summary>
        /// URL de la foto en Google Drive
        /// </summary>
        [MaxLength(500)]
        public string? FotoUrl { get; set; }

        /// <summary>
        /// Última vez que se sincronizó con AppSheet
        /// </summary>
        public DateTime LastSync { get; set; }

        /// <summary>
        /// Indica si el empleado tiene huella registrada
        /// </summary>
        public bool TieneHuella { get; set; }

        // Relaciones
        public virtual ICollection<Huella> Huellas { get; set; } = new List<Huella>();
        public virtual ICollection<Asistencia> Asistencias { get; set; } = new List<Asistencia>();
    }
}

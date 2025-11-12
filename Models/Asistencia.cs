using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FingerprintAttendanceSystem.Models
{
    /// <summary>
    /// Modelo de Asistencia - Registros de marcajes (entrada/salida)
    /// Estructura compatible con tabla ASISTENCIA de AppSheet
    /// </summary>
    public class Asistencia
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// ID de asistencia en AppSheet (se genera al sincronizar)
        /// </summary>
        [MaxLength(100)]
        public string? IdAsistencia { get; set; }

        /// <summary>
        /// Código del empleado
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Codigo { get; set; } = string.Empty;

        /// <summary>
        /// Nombre del empleado (duplicado de EMPLEADOS para AppSheet)
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// ID de sucursal (duplicado de EMPLEADOS para AppSheet)
        /// </summary>
        public int Sucursal { get; set; }

        /// <summary>
        /// Puesto del empleado (duplicado de EMPLEADOS para AppSheet)
        /// </summary>
        [MaxLength(100)]
        public string? Puesto { get; set; }

        /// <summary>
        /// Fecha del movimiento (solo fecha, sin hora)
        /// </summary>
        public DateTime FechaMovimiento { get; set; }

        /// <summary>
        /// Hora de entrada
        /// </summary>
        public DateTime? HoraEntrada { get; set; }

        /// <summary>
        /// Hora de salida
        /// </summary>
        public DateTime? Salida { get; set; }

        /// <summary>
        /// Tiempo extra en horas (calculado: horas trabajadas - 8)
        /// </summary>
        public decimal? TiempoExtra { get; set; }

        /// <summary>
        /// Indica si el registro ya fue sincronizado con AppSheet
        /// </summary>
        public bool Sincronizado { get; set; } = false;

        /// <summary>
        /// Número de intentos de sincronización fallidos
        /// </summary>
        public int IntentosSincronizacion { get; set; } = 0;

        /// <summary>
        /// Última fecha de intento de sincronización
        /// </summary>
        public DateTime? UltimoIntentoSync { get; set; }

        /// <summary>
        /// Mensaje de error en caso de fallo de sincronización
        /// </summary>
        [MaxLength(500)]
        public string? ErrorSync { get; set; }

        /// <summary>
        /// Fecha de creación del registro local
        /// </summary>
        public DateTime FechaCreacion { get; set; }

        // Relación
        [ForeignKey(nameof(Codigo))]
        public virtual Empleado? Empleado { get; set; }

        /// <summary>
        /// Calcula el tiempo extra basado en horas trabajadas
        /// </summary>
        public void CalcularTiempoExtra(int horasJornadaEstandar = 8)
        {
            if (HoraEntrada.HasValue && Salida.HasValue)
            {
                var horasTrabajadas = (decimal)(Salida.Value - HoraEntrada.Value).TotalHours;
                TiempoExtra = horasTrabajadas > horasJornadaEstandar 
                    ? horasTrabajadas - horasJornadaEstandar 
                    : 0;
            }
            else
            {
                TiempoExtra = null;
            }
        }
    }
}

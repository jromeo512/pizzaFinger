using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FingerprintAttendanceSystem.Models
{
    /// <summary>
    /// Modelo de Huella Digital - Almacena las plantillas biométricas localmente
    /// </summary>
    public class Huella
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int HuellaId { get; set; }

        /// <summary>
        /// Código del empleado (FK)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Codigo { get; set; } = string.Empty;

        /// <summary>
        /// Plantilla biométrica (template) en formato binario
        /// </summary>
        [Required]
        public byte[] Template { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Tamaño del template en bytes
        /// </summary>
        public int TemplateSize { get; set; }

        /// <summary>
        /// Fecha y hora de enrolamiento
        /// </summary>
        public DateTime FechaEnrolamiento { get; set; }

        /// <summary>
        /// Usuario administrador que realizó el enrolamiento
        /// </summary>
        [MaxLength(100)]
        public string? UsuarioEnrolo { get; set; }

        /// <summary>
        /// Indica si la huella está activa
        /// </summary>
        public bool Activa { get; set; } = true;

        /// <summary>
        /// Número de dedo (opcional: 1-10)
        /// </summary>
        public int? NumeroDedo { get; set; }

        // Relación
        [ForeignKey(nameof(Codigo))]
        public virtual Empleado? Empleado { get; set; }
    }
}

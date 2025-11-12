namespace FingerprintAttendanceSystem.Models
{
    /// <summary>
    /// Respuesta de las operaciones del lector de huellas
    /// </summary>
    public class FingerprintResponse
    {
        /// <summary>
        /// Indica si la operación fue exitosa
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Mensaje descriptivo del resultado
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// ID de la huella (opcional)
        /// </summary>
        public int? FingerprintId { get; set; }

        /// <summary>
        /// Score de coincidencia en verificación/identificación (0-100)
        /// </summary>
        public int? Score { get; set; }

        /// <summary>
        /// Código del empleado identificado
        /// </summary>
        public string? Codigo { get; set; }

        /// <summary>
        /// Nombre del empleado identificado
        /// </summary>
        public string? NombreEmpleado { get; set; }

        /// <summary>
        /// Template de la huella capturada (base64)
        /// </summary>
        public string? Template { get; set; }

        /// <summary>
        /// Imagen de la huella en base64
        /// </summary>
        public string? ImageBase64 { get; set; }

        /// <summary>
        /// Datos adicionales
        /// </summary>
        public object? Data { get; set; }
    }
}

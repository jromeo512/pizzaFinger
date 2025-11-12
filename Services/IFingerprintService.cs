using FingerprintAttendanceSystem.Models;

namespace FingerprintAttendanceSystem.Services
{
    /// <summary>
    /// Interfaz para el servicio de lector de huellas ZK9500
    /// </summary>
    public interface IFingerprintService
    {
        /// <summary>
        /// Inicializa el dispositivo lector de huellas
        /// </summary>
        FingerprintResponse Initialize();

        /// <summary>
        /// Finaliza y libera los recursos del dispositivo
        /// </summary>
        FingerprintResponse Terminate();

        /// <summary>
        /// Captura una huella para enrolamiento (3 capturas necesarias)
        /// </summary>
        /// <param name="captureNumber">Número de captura (1-3)</param>
        FingerprintResponse CaptureForEnrollment(int captureNumber);

        /// <summary>
        /// Genera el template de enrolamiento final
        /// </summary>
        FingerprintResponse GenerateEnrollmentTemplate();

        /// <summary>
        /// Identifica una huella capturada contra la base de datos
        /// </summary>
        Task<FingerprintResponse> IdentifyAsync();

        /// <summary>
        /// Verifica si una huella capturada coincide con un template específico
        /// </summary>
        FingerprintResponse Verify(byte[] template);

        /// <summary>
        /// Obtiene la imagen de la última huella capturada
        /// </summary>
        byte[]? GetFingerprintImage();

        /// <summary>
        /// Obtiene el template de la última huella capturada
        /// </summary>
        byte[]? GetLastTemplate();

        /// <summary>
        /// Verifica si el dispositivo está conectado e inicializado
        /// </summary>
        bool IsDeviceReady();

        /// <summary>
        /// Obtiene información del dispositivo
        /// </summary>
        string GetDeviceInfo();
    }
}

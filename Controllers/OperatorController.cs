using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FingerprintAttendanceSystem.Services;
using FingerprintAttendanceSystem.Models;

namespace FingerprintAttendanceSystem.Controllers
{
    /// <summary>
    /// Controlador para funciones de Operador (marcaje)
    /// </summary>
    [Authorize(Roles = "Operador,Admin")]
    public class OperatorController : Controller
    {
        private readonly IFingerprintService _fingerprintService;
        private readonly IAttendanceService _attendanceService;
        private readonly ILogger<OperatorController> _logger;

        public OperatorController(
            IFingerprintService fingerprintService,
            IAttendanceService attendanceService,
            ILogger<OperatorController> logger)
        {
            _fingerprintService = fingerprintService;
            _attendanceService = attendanceService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Inicializa el dispositivo para marcaje
        /// </summary>
        [HttpPost]
        public IActionResult InitDevice()
        {
            var result = _fingerprintService.Initialize();
            return Json(result);
        }

        /// <summary>
        /// Procesa el marcaje completo: captura + identifica + registra
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CheckInOut()
        {
            try
            {
                _logger.LogInformation("Iniciando proceso de marcaje...");

                // Paso 1: Identificar huella
                var identifyResult = await _fingerprintService.IdentifyAsync();

                if (!identifyResult.Success || string.IsNullOrEmpty(identifyResult.Codigo))
                {
                    _logger.LogWarning($"Identificación fallida: {identifyResult.Message}");
                    return Json(new
                    {
                        success = false,
                        message = identifyResult.Message,
                        type = "identify_failed"
                    });
                }

                _logger.LogInformation($"Huella identificada: {identifyResult.Codigo} - {identifyResult.NombreEmpleado}");

                // Paso 2: Procesar el marcaje (entrada o salida)
                var (success, message, asistencia) = await _attendanceService.ProcessCheckInOutAsync(identifyResult.Codigo);

                if (success && asistencia != null)
                {
                    _logger.LogInformation($"Marcaje exitoso: {identifyResult.Codigo} - {message}");

                    return Json(new
                    {
                        success = true,
                        message = message,
                        type = asistencia.Salida.HasValue ? "checkout" : "checkin",
                        data = new
                        {
                            codigo = asistencia.Codigo,
                            nombre = asistencia.Nombre,
                            puesto = asistencia.Puesto,
                            horaEntrada = asistencia.HoraEntrada?.ToString("HH:mm:ss"),
                            horaSalida = asistencia.Salida?.ToString("HH:mm:ss"),
                            tiempoExtra = asistencia.TiempoExtra,
                            fecha = asistencia.FechaMovimiento.ToString("yyyy-MM-dd"),
                            imageBase64 = identifyResult.ImageBase64
                        }
                    });
                }
                else
                {
                    _logger.LogWarning($"Error al procesar marcaje: {message}");
                    return Json(new
                    {
                        success = false,
                        message = message,
                        type = "process_failed",
                        data = new
                        {
                            codigo = identifyResult.Codigo,
                            nombre = identifyResult.NombreEmpleado
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en proceso de marcaje");
                return Json(new
                {
                    success = false,
                    message = $"Error: {ex.Message}",
                    type = "error"
                });
            }
        }

        /// <summary>
        /// Obtiene el estado actual del dispositivo
        /// </summary>
        [HttpGet]
        public IActionResult GetDeviceStatus()
        {
            var isReady = _fingerprintService.IsDeviceReady();
            var info = _fingerprintService.GetDeviceInfo();

            return Json(new
            {
                isReady = isReady,
                info = info,
                message = isReady ? "Dispositivo listo" : "Dispositivo no inicializado"
            });
        }

        /// <summary>
        /// Vista de historial de marcajes del día
        /// </summary>
        public async Task<IActionResult> TodayHistory()
        {
            var asistencias = await _attendanceService.GetTodayAllAttendancesAsync();
            return View(asistencias);
        }
    }
}

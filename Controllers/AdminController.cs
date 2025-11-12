using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FingerprintAttendanceSystem.Data;
using FingerprintAttendanceSystem.Models;
using FingerprintAttendanceSystem.Services;

namespace FingerprintAttendanceSystem.Controllers
{
    /// <summary>
    /// Controlador para funciones de Administrador
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFingerprintService _fingerprintService;
        private readonly IAppSheetService _appSheetService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            IFingerprintService fingerprintService,
            IAppSheetService appSheetService,
            ILogger<AdminController> logger)
        {
            _context = context;
            _fingerprintService = fingerprintService;
            _appSheetService = appSheetService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var stats = new
            {
                TotalEmpleados = await _context.Empleados.CountAsync(),
                EmpleadosConHuella = await _context.Empleados.CountAsync(e => e.TieneHuella),
                AsistenciasHoy = await _context.Asistencias.CountAsync(a => a.FechaMovimiento.Date == DateTime.Today),
                PendientesSincronizar = await _context.Asistencias.CountAsync(a => !a.Sincronizado)
            };

            ViewBag.Stats = stats;
            return View();
        }

        // Vista de enrolamiento de huellas
        public async Task<IActionResult> Enroll()
        {
            var empleados = await _context.Empleados
                .OrderBy(e => e.Nombre)
                .ToListAsync();

            return View(empleados);
        }

        // Iniciar dispositivo
        [HttpPost]
        public IActionResult InitDevice()
        {
            var result = _fingerprintService.Initialize();
            return Json(result);
        }

        // Capturar huella para enrolamiento (1 de 3)
        [HttpPost]
        public IActionResult CaptureFingerprint([FromBody] int captureNumber)
        {
            var result = _fingerprintService.CaptureForEnrollment(captureNumber);
            return Json(result);
        }

        // Generar template final de enrolamiento
        [HttpPost]
        public IActionResult GenerateTemplate()
        {
            var result = _fingerprintService.GenerateEnrollmentTemplate();
            return Json(result);
        }

        // Guardar huella enrolada
        [HttpPost]
        public async Task<IActionResult> SaveFingerprint([FromBody] SaveFingerprintRequest request)
        {
            try
            {
                var empleado = await _context.Empleados
                    .FirstOrDefaultAsync(e => e.Codigo == request.Codigo);

                if (empleado == null)
                {
                    return Json(new { success = false, message = "Empleado no encontrado" });
                }

                var template = _fingerprintService.GetLastTemplate();
                if (template == null || template.Length == 0)
                {
                    return Json(new { success = false, message = "No hay template disponible. Capture la huella primero." });
                }

                // Eliminar huellas anteriores del empleado
                var huellasAnteriores = await _context.Huellas
                    .Where(h => h.Codigo == request.Codigo)
                    .ToListAsync();

                _context.Huellas.RemoveRange(huellasAnteriores);

                // Guardar nueva huella
                var huella = new Huella
                {
                    Codigo = request.Codigo,
                    Template = template,
                    TemplateSize = template.Length,
                    FechaEnrolamiento = DateTime.UtcNow,
                    UsuarioEnrolo = User.Identity?.Name,
                    Activa = true
                };

                _context.Huellas.Add(huella);
                
                empleado.TieneHuella = true;
                
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Huella enrolada exitosamente para empleado: {request.Codigo}");

                return Json(new { success = true, message = "Huella registrada exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar huella");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Sincronizar empleados desde AppSheet
        [HttpPost]
        public async Task<IActionResult> SyncEmployees()
        {
            try
            {
                var success = await _appSheetService.SyncEmpleadosFromAppSheetAsync();
                
                if (success)
                {
                    return Json(new { success = true, message = "Empleados sincronizados exitosamente" });
                }
                else
                {
                    return Json(new { success = false, message = "Error al sincronizar empleados" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al sincronizar empleados");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Sincronizar asistencias pendientes
        [HttpPost]
        public async Task<IActionResult> SyncAttendances()
        {
            try
            {
                var count = await _appSheetService.SyncPendingAsistenciasAsync();
                return Json(new { success = true, message = $"Se sincronizaron {count} asistencias", count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al sincronizar asistencias");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Ver reportes
        public async Task<IActionResult> Reports()
        {
            var hoy = DateTime.Today;
            var asistencias = await _context.Asistencias
                .Include(a => a.Empleado)
                .Where(a => a.FechaMovimiento.Date == hoy)
                .OrderByDescending(a => a.HoraEntrada)
                .ToListAsync();

            return View(asistencias);
        }

        // Gestión de empleados
        public async Task<IActionResult> Employees()
        {
            var empleados = await _context.Empleados
                .OrderBy(e => e.Nombre)
                .ToListAsync();

            return View(empleados);
        }
    }

    public class SaveFingerprintRequest
    {
        public string Codigo { get; set; } = string.Empty;
    }
}

using FingerprintAttendanceSystem.Models;
using FingerprintAttendanceSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace FingerprintAttendanceSystem.Services
{
    /// <summary>
    /// Servicio de gestión de asistencias con lógica de negocio
    /// </summary>
    public class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AttendanceService> _logger;
        private readonly int _jornadaEstandar;
        private readonly int _toleranciaMinutos;
        private readonly int _sucursalId;

        public AttendanceService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<AttendanceService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;

            _jornadaEstandar = configuration.GetValue<int>("Attendance:JornadaHorasEstandar", 8);
            _toleranciaMinutos = configuration.GetValue<int>("Attendance:ToleranciaMinutosEntreMarques", 1);
            _sucursalId = configuration.GetValue<int>("Sucursal:SucursalId", 1);
        }

        public async Task<(bool Success, string Message, Asistencia? Asistencia)> ProcessCheckInOutAsync(string codigo)
        {
            try
            {
                _logger.LogInformation($"Procesando marcaje para empleado: {codigo}");

                // Validar tolerancia de tiempo
                if (!await CanCheckInOutAsync(codigo))
                {
                    return (false, "Debe esperar al menos 1 minuto desde el último marcaje", null);
                }

                // Obtener empleado
                var empleado = await _context.Empleados
                    .FirstOrDefaultAsync(e => e.Codigo == codigo);

                if (empleado == null)
                {
                    _logger.LogWarning($"Empleado no encontrado: {codigo}");
                    return (false, "Empleado no encontrado en el sistema", null);
                }

                // Obtener asistencia del día actual
                var hoy = DateTime.Today;
                var asistenciaHoy = await _context.Asistencias
                    .Where(a => a.Codigo == codigo && a.FechaMovimiento.Date == hoy)
                    .FirstOrDefaultAsync();

                var ahora = DateTime.Now;

                if (asistenciaHoy == null)
                {
                    // Primera marcación del día - ENTRADA
                    asistenciaHoy = new Asistencia
                    {
                        Codigo = codigo,
                        Nombre = empleado.Nombre,
                        Sucursal = empleado.SucursalId,
                        Puesto = empleado.Puesto,
                        FechaMovimiento = hoy,
                        HoraEntrada = ahora,
                        Salida = null,
                        TiempoExtra = null,
                        FechaCreacion = DateTime.UtcNow,
                        Sincronizado = false
                    };

                    _context.Asistencias.Add(asistenciaHoy);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Marcaje de ENTRADA registrado: {codigo} a las {ahora:HH:mm:ss}");
                    return (true, $"Entrada registrada a las {ahora:HH:mm:ss}", asistenciaHoy);
                }
                else if (!asistenciaHoy.Salida.HasValue)
                {
                    // Ya tiene entrada, registrar SALIDA
                    asistenciaHoy.Salida = ahora;
                    asistenciaHoy.CalcularTiempoExtra(_jornadaEstandar);
                    asistenciaHoy.Sincronizado = false; // Marcar para re-sincronizar

                    await _context.SaveChangesAsync();

                    var horasTrabajadas = (ahora - asistenciaHoy.HoraEntrada!.Value).TotalHours;
                    _logger.LogInformation($"Marcaje de SALIDA registrado: {codigo} a las {ahora:HH:mm:ss}. Horas trabajadas: {horasTrabajadas:F2}");
                    
                    return (true, $"Salida registrada a las {ahora:HH:mm:ss}. Horas trabajadas: {horasTrabajadas:F2}", asistenciaHoy);
                }
                else
                {
                    // Ya tiene entrada y salida - ACTUALIZAR SALIDA (última marcación)
                    asistenciaHoy.Salida = ahora;
                    asistenciaHoy.CalcularTiempoExtra(_jornadaEstandar);
                    asistenciaHoy.Sincronizado = false; // Marcar para re-sincronizar

                    await _context.SaveChangesAsync();

                    var horasTrabajadas = (ahora - asistenciaHoy.HoraEntrada!.Value).TotalHours;
                    _logger.LogInformation($"Marcaje de SALIDA ACTUALIZADO: {codigo} a las {ahora:HH:mm:ss}. Horas trabajadas: {horasTrabajadas:F2}");
                    
                    return (true, $"Salida actualizada a las {ahora:HH:mm:ss}. Horas trabajadas: {horasTrabajadas:F2}", asistenciaHoy);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al procesar marcaje para {codigo}");
                return (false, $"Error al procesar marcaje: {ex.Message}", null);
            }
        }

        public async Task<Asistencia?> GetTodayAttendanceAsync(string codigo)
        {
            var hoy = DateTime.Today;
            return await _context.Asistencias
                .Include(a => a.Empleado)
                .Where(a => a.Codigo == codigo && a.FechaMovimiento.Date == hoy)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Asistencia>> GetAttendancesByDateRangeAsync(string codigo, DateTime startDate, DateTime endDate)
        {
            return await _context.Asistencias
                .Include(a => a.Empleado)
                .Where(a => a.Codigo == codigo && 
                           a.FechaMovimiento.Date >= startDate.Date && 
                           a.FechaMovimiento.Date <= endDate.Date)
                .OrderByDescending(a => a.FechaMovimiento)
                .ToListAsync();
        }

        public async Task<List<Asistencia>> GetTodayAllAttendancesAsync()
        {
            var hoy = DateTime.Today;
            return await _context.Asistencias
                .Include(a => a.Empleado)
                .Where(a => a.FechaMovimiento.Date == hoy && a.Sucursal == _sucursalId)
                .OrderByDescending(a => a.HoraEntrada)
                .ToListAsync();
        }

        public async Task<bool> CanCheckInOutAsync(string codigo)
        {
            var hoy = DateTime.Today;
            var ultimoMarcaje = await _context.Asistencias
                .Where(a => a.Codigo == codigo && a.FechaMovimiento.Date == hoy)
                .OrderByDescending(a => a.FechaCreacion)
                .FirstOrDefaultAsync();

            if (ultimoMarcaje == null)
            {
                // No hay marcajes hoy, puede marcar
                return true;
            }

            // Obtener la última hora marcada (entrada o salida)
            var ultimaHora = ultimoMarcaje.Salida ?? ultimoMarcaje.HoraEntrada;

            if (ultimaHora.HasValue)
            {
                var tiempoTranscurrido = DateTime.Now - ultimaHora.Value;
                
                // Validar tolerancia de 1 minuto
                if (tiempoTranscurrido.TotalMinutes < _toleranciaMinutos)
                {
                    _logger.LogWarning($"Marcaje rechazado por tolerancia: {codigo}. Tiempo transcurrido: {tiempoTranscurrido.TotalSeconds:F0} segundos");
                    return false;
                }
            }

            return true;
        }
    }
}

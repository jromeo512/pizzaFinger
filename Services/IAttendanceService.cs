using FingerprintAttendanceSystem.Models;

namespace FingerprintAttendanceSystem.Services
{
    /// <summary>
    /// Interfaz para el servicio de gestión de asistencias
    /// </summary>
    public interface IAttendanceService
    {
        /// <summary>
        /// Procesa un marcaje de entrada o salida
        /// </summary>
        Task<(bool Success, string Message, Asistencia? Asistencia)> ProcessCheckInOutAsync(string codigo);

        /// <summary>
        /// Obtiene el último marcaje del día para un empleado
        /// </summary>
        Task<Asistencia?> GetTodayAttendanceAsync(string codigo);

        /// <summary>
        /// Obtiene las asistencias de un empleado en un rango de fechas
        /// </summary>
        Task<List<Asistencia>> GetAttendancesByDateRangeAsync(string codigo, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Obtiene todas las asistencias del día actual
        /// </summary>
        Task<List<Asistencia>> GetTodayAllAttendancesAsync();

        /// <summary>
        /// Valida si se puede realizar un marcaje (tolerancia de tiempo)
        /// </summary>
        Task<bool> CanCheckInOutAsync(string codigo);
    }
}

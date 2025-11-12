using FingerprintAttendanceSystem.Models;

namespace FingerprintAttendanceSystem.Services
{
    /// <summary>
    /// Interfaz para el servicio de integración con AppSheet API
    /// </summary>
    public interface IAppSheetService
    {
        /// <summary>
        /// Sincroniza empleados desde AppSheet a la BD local
        /// </summary>
        Task<bool> SyncEmpleadosFromAppSheetAsync();

        /// <summary>
        /// Envía un registro de asistencia a AppSheet
        /// </summary>
        Task<bool> SendAsistenciaToAppSheetAsync(Asistencia asistencia);

        /// <summary>
        /// Sincroniza todas las asistencias pendientes
        /// </summary>
        Task<int> SyncPendingAsistenciasAsync();

        /// <summary>
        /// Obtiene un empleado específico por código desde AppSheet
        /// </summary>
        Task<Empleado?> GetEmpleadoByCodigoAsync(string codigo);

        /// <summary>
        /// Verifica la conectividad con AppSheet
        /// </summary>
        Task<bool> TestConnectionAsync();
    }
}

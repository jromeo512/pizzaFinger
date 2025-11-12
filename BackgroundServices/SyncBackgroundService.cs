using FingerprintAttendanceSystem.Services;

namespace FingerprintAttendanceSystem.BackgroundServices
{
    /// <summary>
    /// Servicio en background para sincronización automática con AppSheet
    /// </summary>
    public class SyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SyncBackgroundService> _logger;
        private readonly IConfiguration _configuration;
        private readonly int _intervalMinutos;

        public SyncBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<SyncBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
            _intervalMinutos = configuration.GetValue<int>("Attendance:SincronizacionIntervalMinutos", 5);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Servicio de sincronización iniciado. Intervalo: {_intervalMinutos} minutos");

            // Esperar 1 minuto antes de la primera sincronización
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncDataAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el servicio de sincronización");
                }

                // Esperar el intervalo configurado
                await Task.Delay(TimeSpan.FromMinutes(_intervalMinutos), stoppingToken);
            }

            _logger.LogInformation("Servicio de sincronización detenido");
        }

        private async Task SyncDataAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var appSheetService = scope.ServiceProvider.GetRequiredService<IAppSheetService>();

            _logger.LogInformation("Iniciando sincronización automática...");

            try
            {
                // 1. Sincronizar asistencias pendientes
                var syncCount = await appSheetService.SyncPendingAsistenciasAsync();
                _logger.LogInformation($"Sincronizadas {syncCount} asistencias pendientes");

                // 2. Sincronizar empleados (cada hora)
                var lastSyncTime = DateTime.UtcNow.Hour;
                if (lastSyncTime % 1 == 0) // Cada hora en punto
                {
                    var success = await appSheetService.SyncEmpleadosFromAppSheetAsync();
                    if (success)
                    {
                        _logger.LogInformation("Empleados sincronizados desde AppSheet");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la sincronización automática");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Deteniendo servicio de sincronización...");
            await base.StopAsync(cancellationToken);
        }
    }
}

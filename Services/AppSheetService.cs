using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FingerprintAttendanceSystem.Data;
using FingerprintAttendanceSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FingerprintAttendanceSystem.Services
{
    /// <summary>
    /// Servicio de integración con AppSheet API
    /// </summary>
    public class AppSheetService : IAppSheetService
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AppSheetService> _logger;
        private readonly string _applicationId;
        private readonly string _apiKey;
        private readonly int _sucursalId;              // id numérico local (opcional)
        private readonly string? _sucursalKey;         // clave/string de sucursal (opcional)

        // DTO opcional si AppSheet envía { "Rows": [...] }
        private class AppSheetRowsResponse
        {
            [JsonProperty("Rows")]
            public List<Dictionary<string, object>> Rows { get; set; } = new();
        }

        public AppSheetService(
            HttpClient httpClient,
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<AppSheetService> logger)
        {
            _httpClient = httpClient;
            _context = context;
            _configuration = configuration;
            _logger = logger;

            _applicationId = configuration["AppSheet:ApplicationId"] ?? throw new ArgumentException("AppSheet:ApplicationId no configurado");
            _apiKey = configuration["AppSheet:ApiKey"] ?? throw new ArgumentException("AppSheet:ApiKey no configurado");
            _sucursalId = configuration.GetValue<int>("Sucursal:SucursalId");
            _sucursalKey = configuration["Sucursal:SucursalKey"]; // p.ej. "1CENTRO" (opcional)

            // Header de access key
            _httpClient.DefaultRequestHeaders.Remove("ApplicationAccessKey");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("ApplicationAccessKey", _apiKey);
        }

        // URL correcta con /Action (singular)
        private string TableActionUrl(string table) =>
            $"https://www.appsheet.com/api/v2/apps/{_applicationId}/tables/{Uri.EscapeDataString(table)}/Action";

        // Parser que acepta ambos formatos de respuesta: array en raíz o { "Rows": [...] }
        private static List<Dictionary<string, object>> ParseRowsFlexible(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();

            var first = json.SkipWhile(char.IsWhiteSpace).FirstOrDefault();

            if (first == '[')
            {
                var rows = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                return rows ?? new();
            }

            if (first == '{')
            {
                var obj = JsonConvert.DeserializeObject<AppSheetRowsResponse>(json);
                if (obj?.Rows != null) return obj.Rows;

                var jo = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (jo != null && jo.TryGetValue("rows", out var rowsObj) && rowsObj is Newtonsoft.Json.Linq.JArray ja)
                {
                    return ja.ToObject<List<Dictionary<string, object>>>() ?? new();
                }
            }

            return new();
        }

        // Helpers de sucursal
        private static bool TryGetInt(object? value, out int result)
        {
            result = default;
            if (value == null) return false;

            // si viene como número (long/double/etc)
            try
            {
                switch (value)
                {
                    case int i: result = i; return true;
                    case long l when l >= int.MinValue && l <= int.MaxValue: result = (int)l; return true;
                    case double d when d >= int.MinValue && d <= int.MaxValue && Math.Abs(d % 1) < double.Epsilon:
                        result = (int)d; return true;
                }
            }
            catch { /* ignore */ }

            // si viene como string (p.ej. "12" o "1CENTRO")
            var s = value.ToString()?.Trim();
            if (string.IsNullOrEmpty(s)) return false;

            // primero intenta parseo directo
            if (int.TryParse(s, out result)) return true;

            // si trae dígitos al inicio ("1CENTRO"), extrae prefijo numérico
            var digits = new string(s.TakeWhile(char.IsDigit).ToArray());
            if (!string.IsNullOrEmpty(digits) && int.TryParse(digits, out result)) return true;

            return false;
        }

        private bool MatchesSucursal(object? sucursalValue)
        {
            // 1) si tenemos clave string configurada, compara string con tolerancia
            var s = sucursalValue?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(_sucursalKey) && !string.IsNullOrEmpty(s))
            {
                if (string.Equals(s, _sucursalKey, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // 2) si podemos obtener número, compáralo con _sucursalId
            if (_sucursalId != 0 && TryGetInt(sucursalValue, out var n))
                return n == _sucursalId;

            // 3) último intento: si no tenemos _sucursalKey pero el valor es igual a _sucursalId ToString
            if (!string.IsNullOrEmpty(s) && _sucursalId != 0 && s.Equals(_sucursalId.ToString(), StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        public async Task<bool> SyncEmpleadosFromAppSheetAsync()
        {
            try
            {
                _logger.LogInformation("Sincronizando empleados desde AppSheet...");

                var url = TableActionUrl("EMPLEADOS");

                // Traer todos y filtrar aquí (o usa Selector si quieres filtrar en el servidor)
                var requestBody = new
                {
                    Action = "Find",
                    Properties = new
                    {
                        Locale = "es-GT",
                        Timezone = "Central Standard Time"
                        // Ejemplo para filtrar en servidor si tienes SucursalKey:
                        // Selector = $"Filter(EMPLEADOS, TEXT([SUCURSAL]) = \"{_sucursalKey}\")"
                        // o si es número: Selector = $"Filter(EMPLEADOS, [SUCURSAL] = {_sucursalId})"
                    },
                    Rows = Array.Empty<object>()
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Error al sincronizar empleados: {response.StatusCode} - {errorContent}");
                    return false;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var empleadosData = ParseRowsFlexible(responseContent);

                if (empleadosData.Count == 0)
                {
                    _logger.LogInformation("AppSheet devolvió 0 filas.");
                    return true;
                }

                int count = 0;

                foreach (var empData in empleadosData)
                {
                    // Filtra por sucursal de forma tolerante
                    if (empData.TryGetValue("SUCURSAL", out var sucursalObj) && MatchesSucursal(sucursalObj))
                    {
                        var codigo = empData.TryGetValue("CODIGO", out var codObj) ? codObj?.ToString() ?? "" : "";
                        if (string.IsNullOrWhiteSpace(codigo)) continue;

                        var empleado = await _context.Empleados.FirstOrDefaultAsync(e => e.Codigo == codigo);
                        if (empleado == null)
                        {
                            empleado = new Empleado { Codigo = codigo };
                            _context.Empleados.Add(empleado);
                        }

                        empleado.IdEmpleado = empData.TryGetValue("ID EMPLEADO", out var id) ? id?.ToString() : null;
                        empleado.Nombre = empData.TryGetValue("NOMBRE", out var nombre) ? nombre?.ToString() ?? "" : "";
                        empleado.Puesto = empData.TryGetValue("PUESTO", out var puesto) ? puesto?.ToString() : null;

                        // Asigna SucursalId de forma segura
                        if (TryGetInt(sucursalObj, out var sucIdParsed))
                            empleado.SucursalId = sucIdParsed;
                        else
                            empleado.SucursalId = _sucursalId != 0 ? _sucursalId : empleado.SucursalId; // fallback

                        empleado.FotoUrl = empData.TryGetValue("FOTO", out var foto) ? foto?.ToString() : null;

                        if (empData.TryGetValue("FECHA DE INGRESO", out var fechaIngreso) &&
                            DateTime.TryParse(fechaIngreso?.ToString(), out var fecha))
                        {
                            empleado.FechaIngreso = fecha;
                        }

                        empleado.LastSync = DateTime.UtcNow;
                        count++;
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Sincronizados {count} empleados desde AppSheet");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al sincronizar empleados desde AppSheet");
                return false;
            }
        }

        public async Task<bool> SendAsistenciaToAppSheetAsync(Asistencia asistencia)
        {
            try
            {
                _logger.LogInformation($"Enviando asistencia a AppSheet: {asistencia.Codigo} - {asistencia.FechaMovimiento}");

                var url = TableActionUrl("ASISTENCIA");

                var row = new Dictionary<string, object?>
                {
                    { "NOMBRE", asistencia.Nombre ?? "" },
                    { "SUCURSAL", asistencia.Sucursal },
                    { "PUESTO", asistencia.Puesto ?? "" },
                    { "CODIGO", asistencia.Codigo },
                    { "FECHA", asistencia.FechaMovimiento.ToString("yyyy-MM-dd") },
                    // MOVIMIENTO lo asigna AppSheet con automatización
                    { "ENTRADA", asistencia.HoraEntrada?.ToString("HH:mm:ss") ?? "" },
                    { "SALIDA", asistencia.Salida?.ToString("HH:mm:ss") ?? "" }
                };

                var requestBody = new
                {
                    Action = "Add",
                    Properties = new { Locale = "es-GT", Timezone = "Central Standard Time" },
                    Rows = new[] { row }
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                _logger.LogInformation($"Body enviado a AppSheet: {JsonConvert.SerializeObject(requestBody)}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Asistencia enviada exitosamente: {responseContent}");

                    asistencia.Sincronizado = true;
                    asistencia.ErrorSync = null;
                    await _context.SaveChangesAsync();

                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Error al enviar asistencia: {response.StatusCode} - {errorContent}");

                    asistencia.IntentosSincronizacion++;
                    asistencia.UltimoIntentoSync = DateTime.UtcNow;
                    asistencia.ErrorSync = $"{response.StatusCode}: {errorContent}";
                    await _context.SaveChangesAsync();

                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar asistencia a AppSheet");

                asistencia.IntentosSincronizacion++;
                asistencia.UltimoIntentoSync = DateTime.UtcNow;
                asistencia.ErrorSync = ex.Message;
                await _context.SaveChangesAsync();

                return false;
            }
        }

        public async Task<int> SyncPendingAsistenciasAsync()
        {
            try
            {
                _logger.LogInformation("Sincronizando asistencias pendientes...");

                var pendientes = await _context.Asistencias
                    .Where(a => !a.Sincronizado && a.IntentosSincronizacion < 5)
                    .OrderBy(a => a.FechaCreacion)
                    .ToListAsync();

                int syncCount = 0;

                foreach (var asistencia in pendientes)
                {
                    if (await SendAsistenciaToAppSheetAsync(asistencia))
                    {
                        syncCount++;
                    }

                    // Pausa breve para no saturar la API
                    await Task.Delay(500);
                }

                _logger.LogInformation($"Sincronizadas {syncCount} de {pendientes.Count} asistencias pendientes");
                return syncCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al sincronizar asistencias pendientes");
                return 0;
            }
        }

        public async Task<Empleado?> GetEmpleadoByCodigoAsync(string codigo)
        {
            try
            {
                _logger.LogInformation($"Obteniendo empleado por código: {codigo}");

                var url = TableActionUrl("EMPLEADOS");

                // Busca por CODIGO usando Selector; Rows debe ir vacío
                var selector = $"Top(Filter(EMPLEADOS, [CODIGO] = \"{codigo.Replace("\"", "\\\"")}\"), 1)";

                var requestBody = new
                {
                    Action = "Find",
                    Properties = new
                    {
                        Locale = "es-GT",
                        Timezone = "Central Standard Time",
                        Selector = selector
                    },
                    Rows = Array.Empty<object>()
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode) return null;

                var responseContent = await response.Content.ReadAsStringAsync();
                var rows = ParseRowsFlexible(responseContent);
                var empData = rows.FirstOrDefault();
                if (empData == null) return null;

                // Resolver sucursal id de forma segura
                int resolvedSucursalId = _sucursalId;
                if (empData.TryGetValue("SUCURSAL", out var sucVal) && TryGetInt(sucVal, out var parsed))
                    resolvedSucursalId = parsed;

                var empleado = new Empleado
                {
                    IdEmpleado = empData.TryGetValue("ID EMPLEADO", out var id) ? id?.ToString() : null,
                    Codigo = codigo,
                    Nombre = empData.TryGetValue("NOMBRE", out var nombre) ? nombre?.ToString() ?? "" : "",
                    Puesto = empData.TryGetValue("PUESTO", out var puesto) ? puesto?.ToString() : null,
                    SucursalId = resolvedSucursalId,
                    FotoUrl = empData.TryGetValue("FOTO", out var foto) ? foto?.ToString() : null,
                    LastSync = DateTime.UtcNow
                };

                if (empData.TryGetValue("FECHA DE INGRESO", out var fechaIngreso) &&
                    DateTime.TryParse(fechaIngreso?.ToString(), out var fecha))
                {
                    empleado.FechaIngreso = fecha;
                }

                return empleado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener empleado por código: {codigo}");
                return null;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Probando conexión con AppSheet...");

                var url = TableActionUrl("EMPLEADOS");

                var requestBody = new
                {
                    Action = "Find",
                    Properties = new { Locale = "es-GT", Timezone = "Central Standard Time" },
                    Rows = Array.Empty<object>()
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.PostAsync(url, content, cts.Token);

                _logger.LogInformation($"Conexión AppSheet: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al probar conexión con AppSheet");
                return false;
            }
        }
    }
}

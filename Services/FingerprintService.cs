using FingerprintAttendanceSystem.Models;
using FingerprintAttendanceSystem.Data;
using Microsoft.EntityFrameworkCore;
using libzkfpcsharp;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.DependencyInjection;

namespace FingerprintAttendanceSystem.Services
{
    public class FingerprintService : IFingerprintService, IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<FingerprintService> _logger;
        private IntPtr _devHandle = IntPtr.Zero;
        private IntPtr _dbHandle = IntPtr.Zero;
        private bool _sdkInitialized = false;
        private byte[]? _lastTemplate;
        private byte[]? _lastImage;
        private readonly List<byte[]> _enrollTemplates = new();
        private int _fpWidth = 0;
        private int _fpHeight = 0;
        private byte[] _fpBuffer = Array.Empty<byte>();
        private byte[] _capTmp = new byte[2048];
        private int _cbCapTmp = 2048;
        private readonly object _syncRoot = new();

        /*
        public FingerprintService(ApplicationDbContext context, ILogger<FingerprintService> logger)
        {
            _context = context;
            _logger = logger;
        }
        */
        public FingerprintService(IServiceScopeFactory serviceScopeFactory, ILogger<FingerprintService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _logger.LogInformation("FingerprintService creado como Singleton");
        }

        public FingerprintResponse Initialize()
        {
            lock (_syncRoot)
            {
                if (IsDeviceReady())
                {
                    _logger.LogInformation("El lector ya se encuentra inicializado, reutilizando manejadores existentes");
                    return new FingerprintResponse { Success = true, Message = "Lector ya inicializado", Data = GetDeviceInfo() };
                }

                TerminateInternal();

                try
                {
                    _logger.LogInformation("Inicializando SDK ZK...");

                    int ret = zkfp2.Init();
                    if (ret != zkfperrdef.ZKFP_ERR_OK)
                    {
                        return new FingerprintResponse { Success = false, Message = $"Error al inicializar SDK: {ret}" };
                    }

                    _sdkInitialized = true;

                    bool initialized = false;
                    try
                    {
                        int deviceCount = zkfp2.GetDeviceCount();
                        if (deviceCount <= 0)
                        {
                            return new FingerprintResponse { Success = false, Message = "No hay dispositivos conectados" };
                        }

                        _devHandle = zkfp2.OpenDevice(0);
                        if (_devHandle == IntPtr.Zero)
                        {
                            return new FingerprintResponse { Success = false, Message = "No se pudo abrir el dispositivo" };
                        }

                        _dbHandle = zkfp2.DBInit();
                        if (_dbHandle == IntPtr.Zero)
                        {
                            return new FingerprintResponse { Success = false, Message = "Error al inicializar BD" };
                        }

                        byte[] paramValue = new byte[4];
                        int size = 4;
                        zkfp2.GetParameters(_devHandle, 1, paramValue, ref size);
                        zkfp2.ByteArray2Int(paramValue, ref _fpWidth);

                        size = 4;
                        zkfp2.GetParameters(_devHandle, 2, paramValue, ref size);
                        zkfp2.ByteArray2Int(paramValue, ref _fpHeight);

                        _fpBuffer = new byte[_fpWidth * _fpHeight];
                        ResetCaptureBuffer();
                        _lastTemplate = null;
                        _lastImage = null;
                        _enrollTemplates.Clear();

                        initialized = true;
                        _logger.LogInformation("Dispositivo inicializado correctamente");
                        return new FingerprintResponse { Success = true, Message = "Lector inicializado", Data = GetDeviceInfo() };
                    }
                    finally
                    {
                        if (!initialized)
                        {
                            TerminateInternal();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al inicializar");
                    TerminateInternal();
                    return new FingerprintResponse { Success = false, Message = ex.Message };
                }
            }
        }

        public FingerprintResponse Terminate()
        {
            lock (_syncRoot)
            {
                try
                {
                    TerminateInternal();
                    _logger.LogInformation("Dispositivo cerrado correctamente");
                    return new FingerprintResponse { Success = true, Message = "Dispositivo cerrado" };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al cerrar");
                    return new FingerprintResponse { Success = false, Message = ex.Message };
                }
            }
        }

        public FingerprintResponse CaptureForEnrollment(int captureNumber)
        {
            _logger.LogInformation($"CaptureForEnrollment llamado. DevHandle: {_devHandle}, DbHandle: {_dbHandle}");
            lock (_syncRoot)
            {
                if (!IsDeviceReady())
                    return new FingerprintResponse { Success = false, Message = "Dispositivo no listo" };

                try
                {
                    _logger.LogInformation($"Capturando huella {captureNumber}...");

                    ResetCaptureBuffer();
                    int ret = zkfp2.AcquireFingerprint(_devHandle, _fpBuffer, _capTmp, ref _cbCapTmp);
                    if (ret != zkfp.ZKFP_ERR_OK)
                    {
                        return HandleAcquisitionError(ret, "captura para enrolamiento");
                    }

                    _lastTemplate = new byte[_cbCapTmp];
                    Array.Copy(_capTmp, _lastTemplate, _cbCapTmp);
                    _lastImage = ConvertToImage();

                    if (_enrollTemplates.Count >= captureNumber)
                        _enrollTemplates[captureNumber - 1] = _lastTemplate;
                    else
                        _enrollTemplates.Add(_lastTemplate);

                    return new FingerprintResponse
                    {
                        Success = true,
                        Message = $"Captura {captureNumber} de 3 exitosa",
                        ImageBase64 = Convert.ToBase64String(_lastImage),
                        Template = Convert.ToBase64String(_lastTemplate)
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al capturar");
                    return new FingerprintResponse { Success = false, Message = ex.Message };
                }
            }
        }

        public FingerprintResponse GenerateEnrollmentTemplate()
        {
            lock (_syncRoot)
            {
                if (_enrollTemplates.Count < 3)
                    return new FingerprintResponse { Success = false, Message = "Se requieren 3 capturas" };

                try
                {
                    _logger.LogInformation("Generando template final...");

                    byte[] regTmp = new byte[2048];
                    int cbRegTmp = 0;

                    int ret = zkfp2.DBMerge(_dbHandle, _enrollTemplates[0], _enrollTemplates[1], _enrollTemplates[2], regTmp, ref cbRegTmp);
                    if (ret != zkfp.ZKFP_ERR_OK)
                    {
                        return new FingerprintResponse { Success = false, Message = "Error al generar template" };
                    }

                    _lastTemplate = new byte[cbRegTmp];
                    Array.Copy(regTmp, _lastTemplate, cbRegTmp);
                    _enrollTemplates.Clear();

                    return new FingerprintResponse
                    {
                        Success = true,
                        Message = "Template generado exitosamente",
                        Template = Convert.ToBase64String(_lastTemplate)
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al generar template");
                    return new FingerprintResponse { Success = false, Message = ex.Message };
                }
            }
        }

        public async Task<FingerprintResponse> IdentifyAsync()
        {
            byte[]? capturedTemplate = null;
            byte[]? capturedImage = null;

            lock (_syncRoot)
            {
                if (!IsDeviceReady())
                    return new FingerprintResponse { Success = false, Message = "Dispositivo no listo" };

                try
                {
                    _logger.LogInformation("Identificando huella...");

                    ResetCaptureBuffer();
                    int ret = zkfp2.AcquireFingerprint(_devHandle, _fpBuffer, _capTmp, ref _cbCapTmp);
                    if (ret != zkfp.ZKFP_ERR_OK)
                    {
                        return HandleAcquisitionError(ret, "identificación");
                    }

                    _lastImage = ConvertToImage();
                    capturedImage = _lastImage;
                    capturedTemplate = new byte[_cbCapTmp];
                    Array.Copy(_capTmp, capturedTemplate, _cbCapTmp);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al identificar");
                    return new FingerprintResponse { Success = false, Message = ex.Message };
                }
            }

            if (capturedTemplate == null || capturedImage == null)
            {
                return new FingerprintResponse { Success = false, Message = "No se pudo capturar la huella" };
            }

            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var huellas = await context.Huellas.Include(h => h.Empleado).Where(h => h.Activa).ToListAsync();

                if (!huellas.Any())
                {
                    return new FingerprintResponse { Success = false, Message = "No hay huellas registradas" };
                }

                int bestScore = 0;
                Huella? bestMatch = null;

                lock (_syncRoot)
                {
                    if (!IsDeviceReady())
                    {
                        return new FingerprintResponse { Success = false, Message = "Dispositivo no listo" };
                    }

                    foreach (var huella in huellas)
                    {
                        int score = zkfp2.DBMatch(_dbHandle, capturedTemplate, huella.Template);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestMatch = huella;
                        }
                    }
                }

                const int MATCH_THRESHOLD = 55;
                if (bestScore >= MATCH_THRESHOLD && bestMatch != null)
                {
                    _logger.LogInformation($"Identificada: {bestMatch.Codigo} - Score: {bestScore}");
                    return new FingerprintResponse
                    {
                        Success = true,
                        Message = "Huella identificada",
                        Codigo = bestMatch.Codigo,
                        NombreEmpleado = bestMatch.Empleado?.Nombre,
                        Score = bestScore,
                        ImageBase64 = Convert.ToBase64String(capturedImage)
                    };
                }

                return new FingerprintResponse { Success = false, Message = "Huella no reconocida", Score = bestScore };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al identificar en base de datos");
                return new FingerprintResponse { Success = false, Message = ex.Message };
            }
        }

        public FingerprintResponse Verify(byte[] template)
        {
            lock (_syncRoot)
            {
                if (!IsDeviceReady())
                    return new FingerprintResponse { Success = false, Message = "Dispositivo no listo" };

                try
                {
                    int score = zkfp2.DBMatch(_dbHandle, _capTmp, template);
                    const int MATCH_THRESHOLD = 55;
                    return new FingerprintResponse
                    {
                        Success = score >= MATCH_THRESHOLD,
                        Message = score >= MATCH_THRESHOLD ? "Verificada" : "No coincide",
                        Score = score
                    };
                }
                catch (Exception ex)
                {
                    return new FingerprintResponse { Success = false, Message = ex.Message };
                }
            }
        }

        public byte[]? GetFingerprintImage() => _lastImage;
        public byte[]? GetLastTemplate() => _lastTemplate;
        public bool IsDeviceReady() => _devHandle != IntPtr.Zero && _dbHandle != IntPtr.Zero;

        public string GetDeviceInfo()
        {
            return IsDeviceReady() ? $"ZK9500 - {_fpWidth}x{_fpHeight}" : "No inicializado";
        }

        private byte[] ConvertToImage()
        {
            using var bmp = new Bitmap(_fpWidth, _fpHeight, PixelFormat.Format32bppArgb);
            for (int y = 0; y < _fpHeight; y++)
                for (int x = 0; x < _fpWidth; x++)
                {
                    byte grayValue = _fpBuffer[y * _fpWidth + x];
                    bmp.SetPixel(x, y, Color.FromArgb(255, grayValue, grayValue, grayValue));
                }
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Bmp);
            return ms.ToArray();
        }

        private void ResetCaptureBuffer()
        {
            _cbCapTmp = _capTmp.Length;
        }

        private void TerminateInternal()
        {
            if (_dbHandle != IntPtr.Zero)
            {
                zkfp2.DBFree(_dbHandle);
                _dbHandle = IntPtr.Zero;
            }

            if (_devHandle != IntPtr.Zero)
            {
                zkfp2.CloseDevice(_devHandle);
                _devHandle = IntPtr.Zero;
            }

            if (_sdkInitialized)
            {
                zkfp2.Terminate();
                _sdkInitialized = false;
            }

            _fpBuffer = Array.Empty<byte>();
            _capTmp = new byte[2048];
            _cbCapTmp = _capTmp.Length;
            _lastTemplate = null;
            _lastImage = null;
            _enrollTemplates.Clear();
            _fpWidth = 0;
            _fpHeight = 0;
        }

        private FingerprintResponse HandleAcquisitionError(int errorCode, string operation)
        {
            string message;
            bool needsReinitialization = false;

            switch (errorCode)
            {
                case zkfp.ZKFP_ERR_NO_DEVICE:
                case zkfp.ZKFP_ERR_INVALID_HANDLE:
                case zkfp.ZKFP_ERR_CAPTURE:
                case zkfp.ZKFP_ERR_INIT:
                case zkfp.ZKFP_ERR_INITLIB:
                case zkfp.ZKFP_ERR_OPEN:
                    needsReinitialization = true;
                    message = "El lector se desconectó o está ocupado";
                    break;
                default:
                    message = "No se detectó huella";
                    break;
            }

            if (needsReinitialization)
            {
                _logger.LogWarning("Error {ErrorCode} durante {Operation}, reiniciando manejadores", errorCode, operation);
                TerminateInternal();
            }
            else
            {
                _logger.LogWarning("Error {ErrorCode} durante {Operation}", errorCode, operation);
            }

            return new FingerprintResponse
            {
                Success = false,
                Message = $"{message}. Código: 0x{errorCode:X} ({errorCode})"
            };
        }

        public void Dispose() => Terminate();
    }
}

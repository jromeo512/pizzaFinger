using FingerprintAttendanceSystem.Models;
using FingerprintAttendanceSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace FingerprintAttendanceSystem.Services
{
    /// <summary>
    /// Servicio Mock para desarrollo sin el lector físico ZK9500
    /// Usar este servicio cuando no tengas el hardware disponible
    /// </summary>
    public class MockFingerprintService : IFingerprintService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MockFingerprintService> _logger;
        private byte[]? _lastTemplate;
        private byte[]? _lastImage;
        private readonly List<byte[]> _enrollTemplates = new();
        private int _captureCount = 0;

        public MockFingerprintService(ApplicationDbContext context, ILogger<MockFingerprintService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public FingerprintResponse Initialize()
        {
            _logger.LogInformation("MOCK: Inicializando dispositivo virtual...");
            
            return new FingerprintResponse
            {
                Success = true,
                Message = "Dispositivo virtual inicializado correctamente (MODO DESARROLLO)",
                Data = "Mock ZK9500 - Resolución: 256x288 - DPI: 500"
            };
        }

        public FingerprintResponse Terminate()
        {
            _logger.LogInformation("MOCK: Cerrando dispositivo virtual...");
            
            return new FingerprintResponse
            {
                Success = true,
                Message = "Dispositivo virtual cerrado"
            };
        }

        public FingerprintResponse CaptureForEnrollment(int captureNumber)
        {
            _logger.LogInformation($"MOCK: Capturando huella {captureNumber} para enrolamiento...");

            // Simular captura con delay
            Thread.Sleep(500);

            // Generar template mock (datos aleatorios)
            _lastTemplate = GenerateMockTemplate();
            _lastImage = GenerateMockImage();

            if (_enrollTemplates.Count >= captureNumber)
            {
                _enrollTemplates[captureNumber - 1] = _lastTemplate;
            }
            else
            {
                _enrollTemplates.Add(_lastTemplate);
            }

            _captureCount++;

            return new FingerprintResponse
            {
                Success = true,
                Message = $"Captura {captureNumber} de 3 completada exitosamente (MOCK)",
                ImageBase64 = Convert.ToBase64String(_lastImage),
                Template = Convert.ToBase64String(_lastTemplate)
            };
        }

        public FingerprintResponse GenerateEnrollmentTemplate()
        {
            _logger.LogInformation("MOCK: Generando template de enrolamiento final...");

            if (_enrollTemplates.Count < 3)
            {
                return new FingerprintResponse
                {
                    Success = false,
                    Message = $"Se requieren 3 capturas. Solo hay {_enrollTemplates.Count} capturas."
                };
            }

            // Simular procesamiento
            Thread.Sleep(300);

            // Generar template final (combinación mock de los 3)
            _lastTemplate = GenerateMockTemplate();

            _enrollTemplates.Clear();
            _captureCount = 0;

            return new FingerprintResponse
            {
                Success = true,
                Message = "Template de enrolamiento generado exitosamente (MOCK)",
                Template = Convert.ToBase64String(_lastTemplate)
            };
        }

        public async Task<FingerprintResponse> IdentifyAsync()
        {
            _logger.LogInformation("MOCK: Identificando huella...");

            // Simular captura
            await Task.Delay(800);

            // Generar template mock
            _lastTemplate = GenerateMockTemplate();
            _lastImage = GenerateMockImage();

            // Obtener todas las huellas registradas
            var huellas = await _context.Huellas
                .Include(h => h.Empleado)
                .Where(h => h.Activa)
                .ToListAsync();

            if (!huellas.Any())
            {
                return new FingerprintResponse
                {
                    Success = false,
                    Message = "No hay huellas registradas en el sistema"
                };
            }

            // En modo mock, siempre identificamos al primer empleado registrado
            var primeraHuella = huellas.First();

            _logger.LogInformation($"MOCK: Huella identificada - {primeraHuella.Codigo} - Score: 100");

            return new FingerprintResponse
            {
                Success = true,
                Message = "Huella identificada exitosamente (MOCK)",
                Codigo = primeraHuella.Codigo,
                NombreEmpleado = primeraHuella.Empleado?.Nombre ?? "Desconocido",
                Score = 100, // Mock siempre da score perfecto
                ImageBase64 = Convert.ToBase64String(_lastImage)
            };
        }

        public FingerprintResponse Verify(byte[] template)
        {
            _logger.LogInformation("MOCK: Verificando huella...");

            Thread.Sleep(300);

            // Mock siempre verifica exitosamente
            return new FingerprintResponse
            {
                Success = true,
                Message = "Huella verificada (MOCK)",
                Score = 95
            };
        }

        public byte[]? GetFingerprintImage()
        {
            return _lastImage;
        }

        public byte[]? GetLastTemplate()
        {
            return _lastTemplate;
        }

        public bool IsDeviceReady()
        {
            return true; // Mock siempre está listo
        }

        public string GetDeviceInfo()
        {
            return "Mock ZK9500 - Resolución: 256x288 - DPI: 500 (MODO DESARROLLO)";
        }

        // Métodos auxiliares para generar datos mock

        private byte[] GenerateMockTemplate()
        {
            // Generar template mock de tamaño realista (aproximadamente 512 bytes)
            var random = new Random();
            var template = new byte[512];
            random.NextBytes(template);
            return template;
        }

        private byte[] GenerateMockImage()
        {
            // Generar imagen mock muy simple (256x288 pixels en escala de grises)
            // En producción real, esto sería la imagen BMP de la huella
            var width = 256;
            var height = 288;
            var imageSize = width * height;
            
            var random = new Random();
            var image = new byte[imageSize];
            
            // Llenar con patrón que simula una huella
            for (int i = 0; i < imageSize; i++)
            {
                // Patrón de líneas para simular huella
                if ((i / width) % 4 < 2)
                {
                    image[i] = (byte)random.Next(50, 100);
                }
                else
                {
                    image[i] = (byte)random.Next(150, 200);
                }
            }
            
            return image;
        }

        public void Dispose()
        {
            _logger.LogInformation("MOCK: Liberando recursos virtuales...");
        }
    }
}

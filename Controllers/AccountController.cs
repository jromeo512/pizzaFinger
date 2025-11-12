using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FingerprintAttendanceSystem.Data;
using FingerprintAttendanceSystem.Models;

namespace FingerprintAttendanceSystem.Controllers
{
    /// <summary>
    /// Controlador de autenticación y gestión de usuarios
    /// </summary>
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ApplicationDbContext context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            try
            {
                var usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Username == username && u.Activo);

                if (usuario != null && VerifyPassword(password, usuario.PasswordHash))
                {
                    // Actualizar último login
                    usuario.UltimoLogin = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Crear claims
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, usuario.Username),
                        new Claim(ClaimTypes.Role, usuario.Rol),
                        new Claim("SucursalId", usuario.SucursalId.ToString()),
                        new Claim("UsuarioId", usuario.UsuarioId.ToString())
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    _logger.LogInformation($"Usuario {username} inició sesión correctamente");

                    // Redirigir según rol
                    if (usuario.Rol == Roles.Administrador)
                    {
                        return RedirectToAction("Index", "Admin");
                    }
                    else
                    {
                        return RedirectToAction("Index", "Operator");
                    }
                }

                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar sesión");
                ModelState.AddModelError(string.Empty, "Error al iniciar sesión");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("Usuario cerró sesión");
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        /// <summary>
        /// Verifica una contraseña contra su hash
        /// En producción, usar BCrypt: BCrypt.Net.BCrypt.Verify(password, hash)
        /// </summary>
        private bool VerifyPassword(string password, string hash)
        {
            // TEMPORAL: Comparación simple para desarrollo
            // TODO: Implementar BCrypt en producción
            // Instalar: dotnet add package BCrypt.Net-Next
            // Uso: return BCrypt.Net.BCrypt.Verify(password, hash);
            
            // Por ahora, password simple para demo (admin / Admin123!)
            if (password == "Admin123!" && hash.StartsWith("$2a$"))
            {
                return true;
            }

            return false;
        }
    }
}

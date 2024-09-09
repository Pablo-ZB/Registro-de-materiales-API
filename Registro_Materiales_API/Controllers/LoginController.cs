using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;

namespace Registro_Materiales_API.Controllers


{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly string _connectionString;

        public LoginController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("HeadcountConnection"); // Usar la cadena de conexión correcta
        }

        [HttpPost("Authenticate")]
        public IActionResult Login([FromBody] LoginRequest loginRequest)
        {
            if (string.IsNullOrEmpty(loginRequest.NoEmpleado))
            {
                return BadRequest("El número de empleado es requerido.");
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Comprobar si el usuario existe
                    string query = @"SELECT COUNT(*) FROM HEADCOUNT WHERE Numreloj = @NoEmpleado";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@NoEmpleado", loginRequest.NoEmpleado);

                        int count = (int)cmd.ExecuteScalar();
                        if (count > 0)
                        {
                            // Inicio de sesión exitoso, puedes devolver un token o simplemente un éxito
                            return Ok(new { message = "Inicio de sesión exitoso" });
                        }
                        else
                        {
                            return Unauthorized("Número de empleado no válido");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error en el servidor: {ex.Message}");
            }
        }
    }
    public class LoginRequest
    {
        public string NoEmpleado { get; set; }  // Número de empleado
    }

}
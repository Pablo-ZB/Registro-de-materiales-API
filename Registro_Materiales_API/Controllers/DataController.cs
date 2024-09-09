using Microsoft.AspNetCore.Mvc;
using Registro_Materiales_API.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlClient;

namespace Registro_Materiales_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataController : ControllerBase
    {
        private readonly string _connectionStringDAWS;
        private readonly string _connectionStringExternal;

        public DataController(IConfiguration configuration)
        {
            _connectionStringDAWS = configuration.GetConnectionString("DAWS");
            _connectionStringExternal = configuration.GetConnectionString("DAWS_Materiales");
        }

        [HttpPost]
        public IActionResult SaveData([FromBody] DataItem dataItems)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionStringDAWS))
                {
                    conn.Open();

                    foreach (var item in dataItems.Items)
                    {
                        // Extraer la cantidad de la tabla TMES_MATERIALINVENTORY
                        decimal qty = GetQtyFromInventory(item.ScannedCode);

                        // Verificar en la base de datos externa si el KicPNo está como ROLL
                        bool isRoll = CheckIfRoll(item.ScannedCode);

                        // Definir el valor de cant_convertida y el sufijo
                        decimal cantConvertida;
                        string sufijo;
                        if (isRoll)
                        {
                            // Obtener el valor de BomRatio
                            decimal bomRatio = GetBomRatio(item.ScannedCode);

                            // Calcular cant_convertida
                            cantConvertida = (item.Quantity * bomRatio) / 1000;
                            sufijo = "m"; // Sufijo para ROLL
                        }
                        else
                        {
                            // Si no es ROLL, la cantidad convertida es la misma que cant_capturada
                            cantConvertida = item.Quantity;
                            sufijo = "EA"; // Sufijo para no ROLL
                        }

                        // Insertar en la tabla de registros incluyendo cant_sistema y cant_convertida
                        string query = @"INSERT INTO Registros (insert_by, planta, kicpno, cant_capturada, cant_sistema, cant_convertida, tipo)
                                 VALUES (@NoEmpleado, @Planta, @Code, @Quantity, @Qty, @CantConvertida, @Sufijo)";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@NoEmpleado", dataItems.noEmpleado);
                            cmd.Parameters.AddWithValue("@Planta", dataItems.planta);
                            cmd.Parameters.AddWithValue("@Code", item.ScannedCode);
                            cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                            cmd.Parameters.AddWithValue("@Qty", qty);
                            cmd.Parameters.AddWithValue("@CantConvertida", cantConvertida);
                            cmd.Parameters.AddWithValue("@Sufijo", sufijo);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                return Ok("Datos insertados con éxito");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error en el servidor: {ex.Message}");
            }
        }

        private decimal GetBomRatio(string scannedCode)
        {
            decimal bomRatio = 0m;
            string query = @"SELECT BomRatio FROM TMES_MATERIALMASTER WHERE KicPNo = @Code";

            using (SqlConnection conn = new SqlConnection(_connectionStringExternal))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Code", scannedCode);
                    object result = cmd.ExecuteScalar();
                    if (result != null && decimal.TryParse(result.ToString(), out decimal ratio))
                    {
                        bomRatio = ratio;
                    }
                }
            }
            return bomRatio;
        }

        private bool CheckIfRoll(string scannedCode)
        {
            using (SqlConnection conn = new SqlConnection(_connectionStringExternal))
            {
                conn.Open();

                string query = @"SELECT COUNT(*) FROM TMES_MATERIALMASTER WHERE KicPNo = @Code AND UOMBasis = 'ROLL'";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Code", scannedCode);
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        private decimal GetQtyFromInventory(string scannedCode)
        {
            using (SqlConnection conn = new SqlConnection(_connectionStringExternal))
            {
                conn.Open();

                string query = @"SELECT Qty FROM TMES_MATERIALINVENTORY WHERE KicPno = @Code";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Code", scannedCode);
                    object result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToDecimal(result) : 0;
                }
            }
        }
    }
}

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
            Response res = new();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionStringDAWS))
                {
                    conn.Open();

                    foreach (var item in dataItems.items)
                    {
                        decimal qty = GetQtyFromInventory(item.scannedCode, dataItems.planta);

                        bool isRoll = CheckIfRoll(item.scannedCode);
                        var materialData = GetMaterialMasterData(item.scannedCode);

                        decimal cantConvertida;
                        string sufijo;
                        if (isRoll)
                        {
                            qty = (qty * materialData.BomRatio) / 1000;
                            cantConvertida = (item.quantity * materialData.BomRatio) / 1000;
                            sufijo = "M";
                        }
                        else if (item.scannedCode == "AKL03-01192")
                        {
                            qty = (qty * materialData.Qtypack * materialData.BomRatio) / 1000;
                            cantConvertida = (item.quantity * materialData.QtyBox * materialData.BomRatio) / 1000;
                            sufijo = "M";
                        }
                        else if (item.scannedCode == "ESL87-19500")
                        {
                            qty = (qty * materialData.BomRatio) / 1000;
                            cantConvertida = (item.quantity * materialData.BomRatio) / 1000;
                            sufijo = "EA";
                        }
                        else if (item.scannedCode == "EKLKL-00036" || item.scannedCode == "EKLKL-00037" || item.scannedCode == "EKLKL-00056" ||
                            item.scannedCode == "EKLKL-00060" || item.scannedCode == "EKLKL-00070" || item.scannedCode == "EKLKL-00073" || 
                            item.scannedCode == "EKLKL-00094" || item.scannedCode == "EKLKL-00096")
                        {
                            qty = qty * materialData.BomRatio;
                            cantConvertida = item.quantity * materialData.BomRatio;
                            sufijo = "EA";
                        }
                        else if (materialData.CompKind.ToLower().Trim() == "v/sheet" && materialData.Uombasis.ToLower().Trim() == "m")
                        {
                            cantConvertida = item.quantity * materialData.Qtypack;
                            sufijo = "M";
                        }
                        else if(materialData.Uombasis.ToLower().Trim() == "kg" && materialData.Uombasiskic.ToLower().Trim() == "ea")
                        {
                            qty = qty * materialData.BomRatio;
                            cantConvertida = item.quantity * materialData.BomRatio;
                            sufijo = "EA";
                        }
                        else if(materialData.Uombasis.ToLower().Trim() == "ft" && materialData.CompKind.ToLower().Trim() == "tube")
                        {
                            qty = (qty * materialData.BomRatio) / 1000;
                            cantConvertida = item.quantity * materialData.QtyBox * materialData.BomRatio / 1000;
                            sufijo = "M";
                        }
                        else
                        {
                            cantConvertida = item.quantity;
                            sufijo = "EA";
                        }

                        string query = @"INSERT INTO Materials.Registros (insert_by, planta, kicpno, cant_capturada, cant_sistema, cant_convertida, tipo, QtyBox, CompKind)
                         VALUES (@NoEmpleado, @Planta, @Code, @Quantity, @Qty, @CantConvertida, @Sufijo, @QtyBox, @CompKind)";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@NoEmpleado", dataItems.noEmpleado);
                            cmd.Parameters.AddWithValue("@Planta", dataItems.planta);
                            cmd.Parameters.AddWithValue("@Code", item.scannedCode);
                            cmd.Parameters.AddWithValue("@Quantity", item.quantity);
                            cmd.Parameters.AddWithValue("@Qty", qty);
                            cmd.Parameters.AddWithValue("@CantConvertida", cantConvertida);
                            cmd.Parameters.AddWithValue("@Sufijo", sufijo);
                            cmd.Parameters.AddWithValue("@QtyBox", materialData.QtyBox);
                            cmd.Parameters.AddWithValue("@CompKind", materialData.CompKind);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                res.Title = "Datos insertados";
                res.Message = "Datos insertados con éxito";
                res.StatusCode = 200;
                return Ok(res);
            }
            catch (Exception ex)
            {
                res.Title = "Error";
                res.Message = $"Error en el servidor: {ex.Message}";
                res.StatusCode=500;
                return BadRequest(res);
            }
        }

        private (decimal BomRatio, decimal QtyBox, string CompKind, string Uombasis, string Uombasiskic, int Qtypack) GetMaterialMasterData(string scannedCode)
        {
            decimal bomRatio = 0m;
            decimal qtyBox = 0m;
            string compKind = string.Empty;
            string uombasis = string.Empty;
            string uombasiskic = string.Empty;
            int qtyPack = 0;

            string query = @"SELECT BomRatio, QtyBox, CompKind, UOMBasis, UOMBasisKic, QtyPack FROM TMES_MATERIALMASTER WHERE KicPNo = @Code";

            using (SqlConnection conn = new SqlConnection(_connectionStringExternal))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Code", scannedCode);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            if (!reader.IsDBNull(0))
                                bomRatio = reader.GetDecimal(0);
                            if (!reader.IsDBNull(1))
                                qtyBox = reader.GetInt32(1);
                            if (!reader.IsDBNull(2))
                                compKind = reader.GetString(2);
                            if (!reader.IsDBNull(3))
                                uombasis = reader.GetString(3);
                            if (!reader.IsDBNull(4))
                                uombasiskic = reader.GetString(4);
                            if (!reader.IsDBNull(5))
                                qtyPack = reader.GetInt32(5);
                        }
                    }
                }
            }

            return (bomRatio, qtyBox, compKind, uombasis, uombasiskic, qtyPack);
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

        private decimal GetQtyFromInventory(string scannedCode, string planta)
        {
            using (SqlConnection conn = new SqlConnection(_connectionStringExternal))
            {
                conn.Open();

                string query = @"SELECT SUM(Qty) FROM TMES_MATERIALINVENTORY WHERE KicPno = @Code AND WareHouseCode = @Planta";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Planta", planta);
                    cmd.Parameters.AddWithValue("@Code", scannedCode);
                    object result = cmd.ExecuteScalar();
                    return result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
                }
            }
        }
    }
}

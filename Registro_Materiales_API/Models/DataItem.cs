namespace Registro_Materiales_API.Models
{
    public class DataItem
    {
        public string noEmpleado { get; set; }
        public string planta { get; set; }
        public List<Item> Items { get; set; } 
        
    }

    public class Item
    {
        public string ScannedCode { get; set; }
        public int Quantity { get; set; }




    }
}

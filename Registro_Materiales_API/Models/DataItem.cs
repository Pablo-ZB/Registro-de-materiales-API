namespace Registro_Materiales_API.Models
{
    public class DataItem
    {
        public string noEmpleado { get; set; }
        public string planta { get; set; }
        public List<Item> items { get; set; } 
        
    }

    public class Item
    {
        public string scannedCode { get; set; }
        public int quantity { get; set; }
    }
}

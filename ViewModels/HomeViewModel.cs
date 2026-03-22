using WebLinhKienPc.Models;

namespace WebLinhKienPc.ViewModels
{
    public class HomeViewModel
    {
        public List<Product> HotProducts { get; set; }
        public List<Product> NewProducts { get; set; }
        public List<Product> LapsProducts { get; set; }
        public List<Product> Accsessories { get; set; }
        public List<Product> Phones { get; set; }
    }
}

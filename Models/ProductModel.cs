using System.Collections.Generic;

namespace N11ProductIntegration.Models
{
    public class ProductModel
    {
        public ProductModel()
        {
            Options = new List<ProductOption>();
            ProductImages = new List<string>();
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ShortDescription { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPrice { get; set; }
        public string Stock { get; set; }
        public string SKU { get; set; }
        public string Barcode { get; set; }
        public IList<ProductOption> Options { get; set; }
        public IList<string> ProductImages { get; set; }
    }

    public class ProductOption
    {
        public string Barcode { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
    }
}
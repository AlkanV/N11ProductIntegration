using N11ProductIntegration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml;

namespace N11ProductIntegration.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult SendN11()
        {
            #region Read Xml
            var productXml = Server.MapPath("~/App_Data/products.xml");
            XmlDocument doc = new XmlDocument();
            doc.Load(productXml);
            var productsNodes = doc.SelectSingleNode("Products").SelectNodes("Product");

            var n11Products = new List<ProductModel>();
            for (int i = 0; i < productsNodes.Count; i++)
            {
                var productNode = productsNodes[i];
                var product = new ProductModel
                {
                    Id = productNode.SelectSingleNode("ProductId").InnerText,
                    Name = productNode.SelectSingleNode("Name").InnerText,
                    SKU = productNode.SelectSingleNode("SKU").InnerText,
                    Description = productNode.SelectSingleNode("FullDescription").InnerText,
                    ShortDescription = productNode.SelectSingleNode("ShortDescription").InnerText,
                    Price = Convert.ToDecimal(productNode.SelectSingleNode("Price").InnerText.Replace(",", ".")),
                    Stock = productNode.SelectSingleNode("StockQuantity").InnerText,
                };

                if (productNode.SelectSingleNode("OldPrice").InnerText != "0.0000")//sıfır değilse indirimli fiyatı mevcut
                {
                    product.Price = Convert.ToDecimal(productNode.SelectSingleNode("OldPrice").InnerText.Replace(",", "."));//Orjinal fiyat
                    product.DiscountPrice = Convert.ToDecimal(productNode.SelectSingleNode("Price").InnerText.Replace(",", "."));//İndirimli fiyat
                }

                if (productNode.SelectSingleNode("ProductAttributes").SelectNodes("ProductAttributeMapping").Count > 0)//ürünün opsiyonları Beden,Renk, Ayakkabı numarası gibi
                {
                    var productMappings = productNode.SelectSingleNode("ProductAttributes").SelectNodes("ProductAttributeMapping");
                    for (int j = 0; j < productMappings.Count; j++)
                    {
                        var productOptionValues = productMappings[j].SelectSingleNode("ProductAttributeValues").SelectNodes("ProductAttributeValue");
                        for (int k = 0; k < productOptionValues.Count; k++)
                        {
                            product.Options.Add(new ProductOption
                            {
                                Name = productMappings[j].SelectSingleNode("ProductAttributeName").InnerText,
                                Value = productOptionValues[k].SelectSingleNode("Name").InnerText,
                                Price = Convert.ToDecimal(productOptionValues[k].SelectSingleNode("PriceAdjustment").InnerText.Replace(",", ".")),
                                StockQuantity = Convert.ToInt32(productOptionValues[k].SelectSingleNode("Quantity").InnerText)
                            });
                        }
                    }
                }
                n11Products.Add(product);
            }
            #endregion

            #region Send to N11

            var authentication = new N11ProductService.Authentication()
            {
                appKey = "", //api anahtarınız
                appSecret = ""//api şifeniz
            };

            int succesProductCount = 0;
            int errorProductCount = 0;
            string errorMessages = string.Empty;

            foreach (var product in n11Products)
            {
                #region Pictures
                var productImages = new List<N11ProductService.ProductImage>();
                foreach (var imageUrl in product.ProductImages)
                {
                    productImages.Add(new N11ProductService.ProductImage { url = "https:" + imageUrl, order = "0" });
                }
                #endregion

                #region Options & Properties
                //ayakkabı/t-shirt
                //Beden va ayakkabı numaralarında her kırılıma bir stockitem oluşturup içerisine kırılımı ve kırılımın stoğunu ekliyoruz ve sellerCode barkod oluyor
                var stockItems = new List<N11ProductService.ProductSkuRequest>();
                foreach (var item in product.Options)
                {
                    stockItems.Add(new N11ProductService.ProductSkuRequest
                    {
                        attributes = new N11ProductService.ProductAttributeRequest[] { new N11ProductService.ProductAttributeRequest { name = item.Name, value = item.Value } },
                        optionPrice = product.Price,
                        quantity = item.StockQuantity.ToString(),
                        sellerStockCode = item.Barcode
                    });
                }

                //çanta
                //eğer stokitem listesi boş ise bu çanta/kalemlik vs. oluyor, tek bir stockitem oluşturup fiyat, adet ve stockCode veriyoruz.
                //barkod boş gelebiliyor bu yüzden kontrol edip boş ise SKU veriyoruz
                if (stockItems.Count <= 0)
                {
                    stockItems.Add(new N11ProductService.ProductSkuRequest
                    {
                        optionPrice = product.Price,
                        quantity = product.Stock,
                        sellerStockCode = string.IsNullOrEmpty(product.Barcode) ? product.SKU : product.Barcode
                    });
                }
                #endregion

                #region Discont & Category
                //1 = İndirim Tutarı Cinsinden
                //2 = İndirim Oranı Cinsinden
                //3 = İndirimli Fiyat Cinsinden
                var discountRequest = new N11ProductService.ProductDiscountRequest
                {
                    type = "3",
                    value = product.DiscountPrice.ToString().Replace(",", ".")
                };

                #endregion

                //Ürün kısa açıklaması boş gelebiliyor
                if (string.IsNullOrEmpty(product.ShortDescription))
                    product.ShortDescription = product.Name;

                #region Product Request
                var productRequest = new N11ProductService.ProductRequest
                {
                    productSellerCode = product.Id,
                    title = product.Name,
                    subtitle = product.ShortDescription,
                    description = product.Description,
                    category = new N11ProductService.CategoryRequest
                    {
                        id = 5456//N11 kategorilerinden ürününüze uygun olan kategorinin idsini buraya ekliyoruz
                    },
                    price = product.Price,
                    currencyType = "1",//TL 1
                    images = productImages.Take(8).ToArray(),//n11 en fazla 8 ürün görseli kabul ediyor
                    approvalStatus = "1",//Aktif 1 | Pasif 0
                    preparingDay = "3",//Hazırlanma süresi
                    stockItems = stockItems.ToArray(),
                    discount = product.DiscountPrice > 0 ? discountRequest : null,
                    productCondition = "1",//Yeni ürün 1 | İkinci el 2
                    shipmentTemplate = "Kargo Şablon Adı",//Bunun n11 panelinizden teslimat şablonları bölümünden oluşturabilirsiniz.
                };

                var request = new N11ProductService.SaveProductRequest
                {
                    product = productRequest,
                    auth = authentication
                };

                var servicePort = new N11ProductService.ProductServicePortClient();
                #endregion

                var response = servicePort.SaveProduct(request);
                if (response.result != null)
                {
                    if (response.result.status.Contains("success"))
                        succesProductCount++;

                    if (!string.IsNullOrEmpty(response.result.errorMessage))
                    {
                        errorProductCount++;
                        errorMessages += string.Format("SKU : {0}, Hata Detayı : {1}<br />", product.SKU, response.result.errorMessage);
                    }
                }
            }

            string result = "Gönderilen Ürün : {0}, Hatalı Ürün : {1}<br /><br />Hata Mesajları : {2}";
            return Content(string.Format(result, succesProductCount, errorProductCount, errorMessages), "text/html");
            #endregion
        }
    }
}
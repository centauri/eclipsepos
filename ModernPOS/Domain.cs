using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EclipsePOS.Modern
{
 public class Product
 {
  public string Code { get; set; } public string Name { get; set; } public string Category { get; set; }
  public string Icon { get; set; } public decimal Price { get; set; } public decimal Vat { get; set; }
  public bool Weighted { get; set; }
  public string PriceLabel => Money(Price) + (Weighted ? " / kg" : "");
  public static string Money(decimal amount) => amount.ToString("C", CultureInfo.GetCultureInfo("nl-NL"));
 }
 public class SaleLine
 {
  public Product Product { get; set; } public decimal Quantity { get; set; }
  public string Name => Product.Name;
  public string Detail => Quantity.ToString("0.###") + (Product.Weighted ? " kg × " : " × ") + Product.Money(Product.Price);
  public decimal Total => Math.Round(Product.Price * Quantity, 2, MidpointRounding.AwayFromZero);
  public string TotalLabel => Product.Money(Total);
 }
 public class Cart
 {
  public List<SaleLine> Lines { get; } = new List<SaleLine>();
  public decimal Total => Lines.Sum(l => l.Total);
  public decimal Tax => Lines.Sum(l => Math.Round(l.Total - l.Total / (1 + l.Product.Vat), 2, MidpointRounding.AwayFromZero));
  public void Add(Product p, decimal quantity = 1)
  {
   if (quantity <= 0 || quantity > 999) throw new ArgumentOutOfRangeException(nameof(quantity));
   var existing = Lines.FirstOrDefault(l => l.Product.Code == p.Code);
   if (existing == null) Lines.Add(new SaleLine { Product = p, Quantity = quantity });
   else { if (existing.Quantity + quantity > 999) throw new InvalidOperationException("Maximaal 999 per artikel."); existing.Quantity += quantity; }
  }
  public string Receipt(string id, string method) => "ECLIPSE / MARKET\nDEMO • GEEN FISCALE BON\n" + id + "\n" + DateTime.Now.ToString("dd-MM-yyyy HH:mm") + "\n\n" +
   string.Join("\n", Lines.Select(l => l.Name + "\n" + l.Detail + "   " + l.TotalLabel)) +
   "\n\nTOTAAL  " + Product.Money(Total) + "\nInclusief btw " + Product.Money(Tax) + "\n" + method + "\nBedankt en tot ziens!\n\n\n";
 }
 public static class Catalog
 {
  public static List<Product> Create() => new List<Product> {
   P("8710000000011", "Flat white", "Koffie", "☕", 3.60m, .09m),
   P("8710000000028", "Espresso", "Koffie", "☕", 2.80m, .09m),
   P("8710000000035", "Croissant", "Bakkerij", "◒", 2.25m, .09m),
   P("8710000000042", "Zuurdesembrood", "Bakkerij", "◓", 4.95m, .09m),
   P("8710000000059", "Verse jus", "Dranken", "◉", 4.20m, .09m),
   P("8710000000066", "Bruiswater", "Dranken", "♒", 2.50m, .09m),
   P("8710000000073", "Notenmix", "Voorraadkast", "✦", 5.75m, .09m),
   P("8710000000080", "Olijfolie", "Voorraadkast", "❧", 12.90m, .09m),
   P("8710000000097", "Katoenen tas", "Lifestyle", "▱", 6.50m, .21m),
   P("8710000000103", "Keramieken mok", "Lifestyle", "◡", 14.95m, .21m),
   new Product { Code="2001", Name="Elstar appels", Category="Vers", Icon="●", Price=3.49m, Vat=.09m, Weighted=true },
   new Product { Code="2002", Name="Conference peren", Category="Vers", Icon="♠", Price=3.95m, Vat=.09m, Weighted=true }
  };
  static Product P(string code,string name,string category,string icon,decimal price,decimal vat) => new Product {Code=code,Name=name,Category=category,Icon=icon,Price=price,Vat=vat};
 }
}

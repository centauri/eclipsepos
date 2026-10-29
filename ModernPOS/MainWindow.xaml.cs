using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EclipsePOS.Modern
{
 public partial class MainWindow : Window
 {
  readonly System.Collections.Generic.List<Product> catalog=Catalog.Create();
  readonly Cart cart=new Cart();
  readonly Hardware hardware=new Hardware();
  string category="Alles",lastReceipt; bool dark, busy;
  public MainWindow()
  {
   InitializeComponent();
   foreach(var name in new[]{"Alles"}.Concat(catalog.Select(p=>p.Category).Distinct()))
   {
    var button=new Button { Content=name, Tag=name };
    button.Click+=(s,e)=> { category=(string)((Button)s).Tag; RefreshProducts(); };
    Categories.Children.Add(button);
   }
   hardware.Barcode+=code=>Dispatcher.BeginInvoke(new Action(()=> { if(!busy) AddCode(code); else Status.Text="Scan uitgesteld: scan opnieuw na het afrekenen."; }));
   hardware.Fault+=message=>Dispatcher.BeginInvoke(new Action(()=>Status.Text="APPARAATFOUT · "+message));
   Closed+=(s,e)=>hardware.Dispose();
   Refresh();
  }
  void RefreshProducts()
  {
   if(Products==null || Search==null) return;
   string q=Search.Text.Trim();
   Products.ItemsSource=catalog.Where(p=>(category=="Alles" || p.Category==category) && (p.Name.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0 || p.Code==q || p.Category.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
   foreach(Button b in Categories.Children)
   {
    bool selected=(string)b.Tag==category;
    b.SetResourceReference(BackgroundProperty,selected?"Accent":"Panel");
    if(selected) b.Foreground=Brushes.White; else b.SetResourceReference(ForegroundProperty,"Ink");
   }
  }
  void Refresh()
  {
   CartItems.ItemsSource=null; CartItems.ItemsSource=cart.Lines;
   ItemCount.Text=cart.Lines.Count==0?"Je mandje is nog leeg":cart.Lines.Count+" artikelregels · prijzen inclusief btw";
   Total.Text=Product.Money(cart.Total); Vat.Text="Inclusief btw  "+Product.Money(cart.Tax);
   Checkout.IsEnabled=cart.Lines.Count>0; RefreshProducts();
  }
  void Guard(Action action)
  {
   try { action(); } catch(Exception ex) { MessageBox.Show(this,ex.GetBaseException().Message,"Eclipse / Market",MessageBoxButton.OK,MessageBoxImage.Warning); }
  }
  void AddProduct(Product p)
  {
   Guard(()=> {
    decimal quantity=1;
    if(p.Weighted)
    {
     busy=true;
     try {
      if(hardware.IsReal) quantity=hardware.ReadWeight();
      else { var input=new AmountDialog("Demo gewicht in kg",p.Name+" · "+p.PriceLabel,"Toevoegen",true) {Owner=this}; if(input.ShowDialog()!=true) return; quantity=input.Amount; }
     } finally { busy=false; }
    }
    cart.Add(p,quantity); Status.Text=p.Name+" toegevoegd"; Refresh();
   });
  }
  void AddCode(string code)
  {
   var p=catalog.FirstOrDefault(x=>x.Code==code.Trim());
   if(p==null) { Status.Text="Barcode niet gevonden: "+code; return; }
   AddProduct(p); Search.Clear(); Search.Focus();
  }
  void Product_Click(object s,RoutedEventArgs e)=>AddProduct((Product)((Button)s).Tag);
  void Search_Changed(object s,TextChangedEventArgs e)=>RefreshProducts();
  void Search_KeyDown(object s,KeyEventArgs e) { if(e.Key==Key.Enter) { AddCode(Search.Text); e.Handled=true; } }
  void Scan_Click(object s,RoutedEventArgs e)=>AddCode(Search.Text);
  void Plus_Click(object s,RoutedEventArgs e)
  {
   var line=(SaleLine)((Button)s).Tag;
   if(line.Product.Weighted) AddProduct(line.Product); else Guard(()=> {cart.Add(line.Product);Refresh();});
  }
  void Minus_Click(object s,RoutedEventArgs e)
  {
   var line=(SaleLine)((Button)s).Tag;
   if(line.Product.Weighted || line.Quantity<=1) cart.Lines.Remove(line); else line.Quantity--;
   Refresh();
  }
  void Remove_Click(object s,RoutedEventArgs e) {cart.Lines.Remove((SaleLine)((Button)s).Tag);Refresh();}
  void Clear_Click(object s,RoutedEventArgs e)
  {
   if(cart.Lines.Count>0 && MessageBox.Show(this,"Huidige verkoop wissen?","Verkoop",MessageBoxButton.YesNo)==MessageBoxResult.Yes) {cart.Lines.Clear();Refresh();}
  }
  void Checkout_Click(object s,RoutedEventArgs e)
  {
   if(cart.Lines.Count==0) return;
   busy=true;
   try
   {
    var payment=new PaymentDialog(cart.Total) {Owner=this};
    if(payment.ShowDialog()!=true) return;
    Guard(()=> {
     string id="DEMO-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
     string receipt=cart.Receipt(id,payment.Method);
     if(payment.Cash) receipt+="Ontvangen "+Product.Money(payment.Received)+"\nWisselgeld "+Product.Money(payment.Received-cart.Total)+"\n";
     // Commit the local sale before any irreversible printer/drawer operation.
     string path=SaleStore.Save(id,receipt,cart,payment.Method,payment.Received);
     lastReceipt=receipt; cart.Lines.Clear();Refresh();
     Status.Text="Demo verkoop opgeslagen · "+Path.GetFileName(path);
     string problems="";
     if(hardware.IsReal && hardware.HasPrinter) try {hardware.Print(receipt);} catch(Exception ex) {problems+="\nBon niet geprint: "+ex.GetBaseException().Message;}
     if(hardware.IsReal && payment.Cash && hardware.HasDrawer) try {hardware.OpenDrawer();} catch(Exception ex) {problems+="\nKassalade niet geopend: "+ex.GetBaseException().Message;}
     MessageBox.Show(this,"Demo verkoop voltooid."+(payment.Cash?"\nWisselgeld: "+Product.Money(payment.Received-payment.Total):"\nPinbetaling is gesimuleerd.")+problems,"Verkoop opgeslagen",MessageBoxButton.OK,problems.Length>0?MessageBoxImage.Warning:MessageBoxImage.Information);
    });
   }
   finally {busy=false;Search.Focus();}
  }
  void Receipt_Click(object s,RoutedEventArgs e)
  {
   if(lastReceipt==null) {Status.Text="Rond eerst een demo verkoop af.";return;}
   var dialog=new Window {Title="Laatste bon",Owner=this,Width=470,Height=680,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=(Brush)FindResource("Panel")};
   var panel=new DockPanel {Margin=new Thickness(20)};
   var print=new Button {Content=hardware.HasPrinter?"Print kopie via POS for .NET":"Geen bonprinter verbonden",IsEnabled=hardware.HasPrinter};
   DockPanel.SetDock(print,Dock.Bottom); print.Click+=(a,b)=>Guard(()=>hardware.Print("KOPIE\n"+lastReceipt));panel.Children.Add(print);
   panel.Children.Add(new TextBox {Text=lastReceipt,IsReadOnly=true,FontFamily=new FontFamily("Consolas"),VerticalScrollBarVisibility=ScrollBarVisibility.Auto});dialog.Content=panel;dialog.ShowDialog();
  }
  void Devices_Click(object s,RoutedEventArgs e)=>Guard(()=> {
   string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"hardware.json");
   var config=new JavaScriptSerializer().Deserialize<HardwareConfig>(File.ReadAllText(path));
   Status.Text="Verbinden…";
   try { Status.Text=hardware.Connect(config); }
   catch { Status.Text="NIET VERBONDEN · controleer hardware.json en vendor drivers";throw; }
  });
  void Theme_Click(object s,RoutedEventArgs e)=>SetTheme(!dark);
  public void SetTheme(bool value)
  {
   dark=value;
   string[] keys={"Bg","Panel","Ink","Muted","Line","Accent"};
   string[] colors=dark?new[]{"#111C1B","#1C2C29","#F0F5F2","#AAC1B8","#354B43","#13876C"}:new[]{"#F1F4F3","#FFFFFF","#172B2A","#667D78","#DFE7E3","#006E58"};
   for(int i=0;i<keys.Length;i++) Application.Current.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
   RefreshProducts();
  }
  public void SeedPreview() {cart.Add(catalog[0],2);cart.Add(catalog[2]);cart.Add(catalog[8]);Refresh();}
 }
 public static class SaleStore
 {
  public static string Save(string id,string receipt,Cart cart,string method,decimal received,string root=null)
  {
   root=root??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EclipsePOS.Modern","sales");
   Directory.CreateDirectory(root);
   string final=Path.Combine(root,id+".json"),temp=final+".tmp";
   var json=new JavaScriptSerializer().Serialize(new {Id=id,CreatedUtc=DateTime.UtcNow.ToString("o"),Demo=true,Method=method,Received=received,Total=cart.Total,Tax=cart.Tax,Lines=cart.Lines,Receipt=receipt});
   try {File.WriteAllText(temp,json,System.Text.Encoding.UTF8);File.Move(temp,final);} finally {if(File.Exists(temp))File.Delete(temp);}
   return final;
  }
 }
}

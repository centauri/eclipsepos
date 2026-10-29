using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EclipsePOS.Modern
{
 public partial class App : Application
 {
  protected override void OnStartup(StartupEventArgs e)
  {
   base.OnStartup(e);
   if(e.Args.Contains("--self-test"))
   {
    try {SelfTest.Run();Shutdown(0);} catch(Exception ex) {File.WriteAllText("self-test-failed.txt",ex.ToString());Shutdown(1);}return;
   }
   var window=new MainWindow();MainWindow=window;
   if(e.Args.Contains("--smoke"))
   {
    window.SeedPreview();window.Show();
    window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=> {
     try {
      window.UpdateLayout();Capture(window,"preview-light.png");
      window.SetTheme(true);window.UpdateLayout();Capture(window,"preview-dark.png");
      var payment=new PaymentDialog(15.95m) {Owner=window};payment.Show();payment.UpdateLayout();Capture(payment,"preview-checkout.png");payment.Close();
      var cash=new AmountDialog("Contant ontvangen","Te betalen: € 15,95","Verkoop voltooien",false,15.95m) {Owner=window};cash.Show();cash.UpdateLayout();Capture(cash,"preview-cash.png");cash.Close();
      File.WriteAllText("smoke-test-passed.txt","WPF windows rendered successfully.");Shutdown(0);
     }catch(Exception ex) {File.WriteAllText("smoke-test-failed.txt",ex.ToString());Shutdown(1);}
    }));
   }
   else window.Show();
  }
  static void Capture(Window window,string name)
  {
   var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(name))encoder.Save(stream);
  }
 }
 public static class SelfTest
 {
  static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
  public static void Run()
  {
   var products=Catalog.Create();var cart=new Cart();
   Check(products.Select(p=>p.Code).Distinct().Count()==products.Count,"Duplicate barcode");
   cart.Add(products[0],2);cart.Add(products[2]);cart.Add(products[8]);
   Check(cart.Total==15.95m,"Mixed cart total");Check(cart.Tax==1.91m,"Mixed VAT inclusive rounding");
   cart.Add(products[0]);Check(cart.Lines.Count==3 && cart.Lines[0].Quantity==3,"Merge barcode");
   bool invalid=false;try {cart.Add(products[0],-1);}catch(ArgumentOutOfRangeException){invalid=true;}Check(invalid,"Reject negative quantity");
   invalid=false;try {cart.Add(products[0],999);}catch(InvalidOperationException){invalid=true;}Check(invalid,"Quantity cap");
   cart.Lines.Clear();cart.Add(products.First(p=>p.Code=="2001"),.345m);Check(cart.Total==1.20m,"Weighted rounding");
   string receipt=cart.Receipt("TEST","PIN · GESIMULEERD");Check(receipt.Contains("Elstar") && receipt.Contains("DEMO"),"Receipt contents");
   string root=Path.Combine(Path.GetTempPath(),"EclipsePOS-tests-"+Guid.NewGuid().ToString("N"));
   try {
    var path=SaleStore.Save("TEST",receipt,cart,"DEMO",cart.Total,root);
    Check(File.ReadAllText(path).Contains("Elstar"),"Persist sale");Check(Directory.GetFiles(root,"*.tmp").Length==0,"Atomic save cleanup");
    invalid=false;try {SaleStore.Save("TEST",receipt,cart,"DEMO",cart.Total,root);}catch(IOException){invalid=true;}Check(invalid,"Prevent duplicate sale overwrite");
    using(var hw=new Hardware()) {Check(hw.Connect(new HardwareConfig()) .Contains("DEMO"),"Demo hardware");invalid=false;try{hw.Print("test");}catch(InvalidOperationException){invalid=true;}Check(invalid,"No silent fake print");}
    File.WriteAllText("self-test-passed.txt","10 checks passed: catalog, totals, VAT, merge, invalid quantity, cap, weight, receipt, persistence/duplicate protection, demo hardware.");
   }finally {if(Directory.Exists(root))Directory.Delete(root,true);}
  }
 }
}

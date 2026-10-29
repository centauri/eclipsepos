using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EclipsePOS.Modern
{
 public class AmountDialog : Window
 {
  readonly TextBox input=new TextBox {Margin=new Thickness(4,16,4,8),FontSize=28};
  readonly TextBlock error=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,8,4,8)};
  public decimal Amount {get;private set;}
  public AmountDialog(string title,string subtitle,string accept,bool weight=false,decimal minimum=0)
  {
   Title=title;Width=440;Height=680;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;SetResourceReference(BackgroundProperty,"Bg");SetResourceReference(ForegroundProperty,"Ink");
   var panel=new StackPanel {Margin=new Thickness(24)};
   panel.Children.Add(new TextBlock {Text=title,FontSize=26,FontWeight=FontWeights.SemiBold});
   panel.Children.Add(new TextBlock {Text=subtitle,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,12,4,0)});panel.Children.Add(input);
   var keys=new System.Windows.Controls.Primitives.UniformGrid {Columns=3};
   foreach(var key in new[]{"7","8","9","4","5","6","1","2","3",",","0","⌫"})
   {
    var button=new Button {Content=key,FontSize=24,Height=64};
    button.Click+=(s,e)=> { if(key=="⌫") input.Text=input.Text.Length>0?input.Text.Substring(0,input.Text.Length-1):"";else input.Text+=key; };keys.Children.Add(button);
   }
   panel.Children.Add(keys);panel.Children.Add(error);
   var done=new Button {Content=accept,Height=60,IsDefault=true,Foreground=Brushes.White};done.SetResourceReference(BackgroundProperty,"Accent");
   done.Click+=(s,e)=> {
    if(!decimal.TryParse(input.Text.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out decimal amount) || amount<=0 || amount<minimum || amount>(weight?999m:999999m) || decimal.Round(amount,weight?3:2)!=amount)
    {error.Text=weight?"Voer 0,001 t/m 999 kg in (max. 3 decimalen).":"Voer een geldig bedrag in, minimaal "+Product.Money(minimum)+" (max. 2 decimalen).";return;}
    Amount=amount;DialogResult=true;
   };panel.Children.Add(done);Content=panel;Loaded+=(s,e)=>input.Focus();
  }
 }
 public class PaymentDialog : Window
 {
  public bool Cash {get;private set;} public decimal Received {get;private set;} public decimal Total {get;}
  public string Method=>Cash?"CONTANT · DEMO":"PIN · GESIMULEERD";
  public PaymentDialog(decimal total)
  {
   Total=total;Title="Afrekenen";Width=460;Height=420;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;SetResourceReference(BackgroundProperty,"Bg");SetResourceReference(ForegroundProperty,"Ink");
   var panel=new StackPanel {Margin=new Thickness(30)};
   panel.Children.Add(new TextBlock {Text="Afrekenen",FontSize=26});panel.Children.Add(new TextBlock {Text=Product.Money(total),FontSize=42,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,10,0,20)});
   var pin=new Button {Content="Pin simuleren",Height=64};pin.Click+=(s,e)=> {Received=total;DialogResult=true;};panel.Children.Add(pin);
   var cash=new Button {Content="Contant · ontvangen bedrag",Height=64};
   cash.Click+=(s,e)=> { var amount=new AmountDialog("Contant ontvangen","Te betalen: "+Product.Money(total),"Verkoop voltooien",false,total) {Owner=this};if(amount.ShowDialog()==true) {Cash=true;Received=amount.Amount;DialogResult=true;} };panel.Children.Add(cash);
   panel.Children.Add(new TextBlock {Text="Demo: er wordt geen betaalterminal aangestuurd.",FontSize=12,Margin=new Thickness(4,12,0,0)});Content=panel;
  }
 }
}

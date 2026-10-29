using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace EclipsePOS.Modern
{
 public class HardwareConfig
 {
  public string Mode { get; set; } = "Demo";
  public string AssemblyPath { get; set; }
  public string Scanner { get; set; } public string Printer { get; set; }
  public string Drawer { get; set; } public string Scale { get; set; }
 }
 // Runtime binding keeps the vendor SDK optional for demo builds; real calls use the installed SDK.
 public sealed class Hardware : IDisposable
 {
  readonly List<object> opened = new List<object>();
  readonly List<Tuple<object, EventInfo, Delegate>> handlers = new List<Tuple<object, EventInfo, Delegate>>();
  dynamic scanner, printer, drawer, scale;
  Assembly sdk;
  public bool IsReal { get; private set; }
  public bool HasScale => scale != null;
  public bool HasPrinter => printer != null;
  public bool HasDrawer => drawer != null;
  public event Action<string> Barcode;
  public event Action<string> Fault;
  public string Connect(HardwareConfig config)
  {
   Dispose();
   if (config.Mode == "Demo") return "DEMO · geen apparaten aangesloten";
   if (config.Mode != "PosNet") throw new InvalidOperationException("Mode moet Demo of PosNet zijn.");
   try
   {
    sdk = Assembly.LoadFrom(config.AssemblyPath);
    dynamic explorer = Activator.CreateInstance(sdk.GetType("Microsoft.PointOfService.PosExplorer", true));
    scanner = Open(explorer,"Scanner",config.Scanner);
    printer = Open(explorer,"PosPrinter",config.Printer);
    drawer = Open(explorer,"CashDrawer",config.Drawer);
    scale = Open(explorer,"Scale",config.Scale);
    if (opened.Count == 0) throw new InvalidOperationException("Vul minimaal één logische apparaatnaam in hardware.json in.");
    if (scanner != null)
    {
     scanner.DecodeData = true;
     Subscribe((object)scanner,"DataEvent", () => {
      try { Barcode?.Invoke(Encoding.ASCII.GetString((byte[])scanner.ScanDataLabel).TrimEnd('\0','\r','\n')); }
      catch(Exception e) { Fault?.Invoke(e.Message); }
      finally { try { scanner.DataEventEnabled=true; } catch(Exception e) { Fault?.Invoke(e.Message); } }
     });
     scanner.DataEventEnabled=true;
    }
    IsReal=true;
    return "POS for .NET · " + opened.Count + " apparaat/apparaten verbonden";
   }
   catch { Dispose(); throw; }
  }
  object Open(dynamic explorer, string category, string logicalName)
  {
   if (string.IsNullOrWhiteSpace(logicalName)) return null;
   // Explicit logical names avoid accidentally claiming the first device on the lane.
   string deviceType=(string)sdk.GetType("Microsoft.PointOfService.DeviceType",true).GetField(category).GetValue(null);
   dynamic info = explorer.GetDevice(deviceType, logicalName);
   if (info == null) throw new InvalidOperationException(category + " niet gevonden: " + logicalName);
   dynamic device = explorer.CreateInstance(info);
   device.Open(); opened.Add((object)device);
   device.Claim(3000); device.DeviceEnabled=true;
   Subscribe((object)device,"ErrorEvent", () => Fault?.Invoke(category + ": apparaatfout; controleer het apparaat en verbind opnieuw."));
   return (object)device;
  }
  void Subscribe(object device,string name,Action callback)
  {
   var evt=device.GetType().GetEvent(name); if(evt==null) return;
   var signature=evt.EventHandlerType.GetMethod("Invoke").GetParameters();
   var parameters=Array.ConvertAll(signature,p=>Expression.Parameter(p.ParameterType,p.Name));
   var handler=Expression.Lambda(evt.EventHandlerType,Expression.Call(Expression.Constant(callback),typeof(Action).GetMethod("Invoke")),parameters).Compile();
   evt.AddEventHandler(device,handler); handlers.Add(Tuple.Create(device,evt,handler));
  }
  public void Print(string receipt)
  {
   if(printer==null) throw new InvalidOperationException("Geen bonprinter aangesloten.");
   object station=Enum.Parse(sdk.GetType("Microsoft.PointOfService.PrinterStation",true),"Receipt");
   // Reflection selects the SDK enum overload without a compile-time SDK dependency.
   ((object)printer).GetType().GetMethod("PrintNormal").Invoke(printer,new object[]{station,receipt.Replace("\n","\r\n")});
  }
  public void OpenDrawer() { if(drawer==null) throw new InvalidOperationException("Geen kassalade aangesloten."); drawer.OpenDrawer(); }
  public decimal ReadWeight()
  {
   if(scale==null) throw new InvalidOperationException("Geen weegschaal aangesloten.");
   decimal weight=(decimal)scale.ReadWeight(3000);
   if(weight<=0 || weight>999) throw new InvalidOperationException("Ongeldig gewicht. Controleer eenheid en stabiliteit.");
   return weight; // Configure the service object to report kilograms.
  }
  public void Dispose()
  {
   foreach(var h in handlers) try { h.Item2.RemoveEventHandler(h.Item1,h.Item3); } catch { }
   handlers.Clear();
   for(int i=opened.Count-1;i>=0;i--)
   {
    dynamic d=opened[i];
    try { d.DeviceEnabled=false; } catch { }
    try { if(d.Claimed) d.Release(); } catch { }
    try { d.Close(); } catch { }
    try { ((IDisposable)d).Dispose(); } catch { }
   }
   opened.Clear(); scanner=printer=drawer=scale=null; IsReal=false;
  }
 }
}

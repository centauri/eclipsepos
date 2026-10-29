using System;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading;
using EclipsePOS.Platform;
using EclipsePos.Data;
using EclipsePos.Data.Util;

class Program
{
    static int count;
    static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); count++; }
    static void Main()
    {
        var password = "Example café 123!";
        var hash = PasswordHash.Create(password);
        Check(PasswordHash.Verify(password, hash), "PBKDF2 Unicode password roundtrip");
        Check(!PasswordHash.Verify("wrong", hash), "Incorrect password rejected");
        Check(hash != PasswordHash.Create(password), "Independent salts");
        Check(PasswordHash.Verify("admin", "21232F297A57A5A743894A0E4A801FC3"), "Legacy MD5 read compatibility");
        Check(!PasswordHash.Verify("wrong", "21232F297A57A5A743894A0E4A801FC3"), "Legacy incorrect password rejected");
        Check(!PasswordHash.Verify(password, "pbkdf2-sha256$2147483647$bad$bad"), "Unbounded work factor rejected");
        Check(!PasswordHash.Verify(password, "pbkdf2-sha256$600000$bad$bad"), "Malformed hash rejected");

        var table = new DataTable();
        string[] columns = {"trans_no","seq_no","line_no","organization_no","store_no","sku","sku_link","quantity","amount","ext_amount","weight","item_desc","state","reason_code","tax_exempt","tax_incl","var_amount","pos_no"};
        foreach (var name in columns) table.Columns.Add(name, typeof(object));
        var row = table.NewRow();
        foreach (var name in columns) row[name] = 1;
        row["quantity"] = 1.375m;
        table.Rows.Add(row);
        foreach (var culture in new[] { "en-US", "nl-NL" })
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Check(new TransItem(row).Quantity == 1.375, "Fractional quantity survives reload: " + culture);
        }

        string directory = Path.Combine(Path.GetTempPath(), "EclipsePOS-test-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("ECLIPSEPOS_DATA_DIR", directory);
        var provider = new EclipseSettingsProvider(); provider.Initialize(null, null);
        var property = new SettingsProperty("DataSource") { PropertyType = typeof(string), SerializeAs = SettingsSerializeAs.String, DefaultValue = "default" };
        var props = new SettingsPropertyCollection { property };
        var context = new SettingsContext();
        Check((string)provider.GetPropertyValues(context, props)["DataSource"].SerializedValue == "default", "Missing settings use defaults");
        var values = new SettingsPropertyValueCollection { new SettingsPropertyValue(property) { SerializedValue = "test", IsDirty = true } };
        provider.SetPropertyValues(context, values);
        Check((string)new EclipseSettingsProvider().GetPropertyValues(context, props)["DataSource"].SerializedValue == "test", "Settings persisted and reloaded");
        var other = new SettingsProperty("Store") { PropertyType = typeof(string), SerializeAs = SettingsSerializeAs.String };
        provider.SetPropertyValues(context, new SettingsPropertyValueCollection { new SettingsPropertyValue(other) { SerializedValue = "2", IsDirty = true } });
        Check((string)provider.GetPropertyValues(context, props)["DataSource"].SerializedValue == "test", "Saving another provider field preserves existing settings");
        Check(File.Exists(Path.Combine(directory, "EclipsePOS.settings.bak")), "Atomic replacement retains backup");
        PosSettings.Default.DataSource = "Server=test;Database=demo;Integrated Security=True";
        PosSettings.Default.Save();
        PosSettings.Default.Reload();
        Check(PosSettings.Default.DataSource.Contains("Database=demo"), "Original ApplicationSettingsBase provider roundtrip");
        File.WriteAllText(Path.Combine(directory, "EclipsePOS.settings"), "<broken");
        bool corrupt = false;
        try { provider.GetPropertyValues(context, props); } catch (System.Xml.XmlException) { corrupt = true; }
        Check(corrupt, "Corrupt settings surface an error instead of resetting");
        Console.WriteLine(count + " checks passed. SQL/hardware integration not included.");
        // Keep isolated test artifacts for inspection; never touch user settings.
    }
}

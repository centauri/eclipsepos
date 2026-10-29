using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;

namespace EclipsePOS.Platform
{
    // All four legacy providers share a file. Reload under a cross-process lock before
    // saving so one application's cached document cannot erase another's settings.
    public class PortableSettingsProvider : SettingsProvider
    {
        public override string ApplicationName { get { return "EclipsePOS"; } set { } }
        public override void Initialize(string name, NameValueCollection config) { base.Initialize(ApplicationName, config); }
        public virtual string GetAppSettingsPath()
        {
            var configured = Environment.GetEnvironmentVariable("ECLIPSEPOS_DATA_DIR");
            var directory = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EclipsePOS", "Data")
                : Path.GetFullPath(configured);
            Directory.CreateDirectory(directory);
            return directory;
        }
        public virtual string GetAppSettingsFilename() { return "EclipsePOS.settings"; }
        private string FilePath { get { return Path.Combine(GetAppSettingsPath(), GetAppSettingsFilename()); } }
        private XmlDocument Read()
        {
            var document = new XmlDocument { XmlResolver = null };
            var path = FilePath;
            // Read legacy settings on first use; leave the original untouched.
            if (!File.Exists(path) && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ECLIPSEPOS_DATA_DIR")))
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EclipseSoftware", GetAppSettingsFilename());
            if (File.Exists(path)) document.Load(path);
            else document.LoadXml("<Settings />");
            if (document.DocumentElement == null || document.DocumentElement.Name != "Settings")
                throw new ConfigurationErrorsException("Invalid EclipsePOS settings file: " + path);
            return document;
        }
        private static XmlElement Parent(XmlDocument doc, SettingsProperty property)
        {
            var root = doc.DocumentElement;
            if (property.Attributes[typeof(SettingsManageabilityAttribute)] != null) return root;
            var name = XmlConvert.EncodeLocalName(Environment.MachineName);
            var node = root[name];
            if (node == null) { node = doc.CreateElement(name); root.AppendChild(node); }
            return node;
        }
        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection properties)
        {
            var doc = Read();
            var values = new SettingsPropertyValueCollection();
            foreach (SettingsProperty property in properties)
            {
                var node = Parent(doc, property)[property.Name];
                values.Add(new SettingsPropertyValue(property) { SerializedValue = node == null ? property.DefaultValue : node.InnerText, IsDirty = false });
            }
            return values;
        }
        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection values)
        {
            string name;
            using (var sha = SHA256.Create()) name = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(FilePath.ToUpperInvariant()))).Replace("-", "");
            using (var mutex = new Mutex(false, @"Local\EclipsePOS-settings-" + name))
            {
                bool acquired = false;
                try
                {
                    try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(10)); } catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) throw new IOException("Settings are busy in another EclipsePOS window.");
                    var doc = Read();
                    foreach (SettingsPropertyValue value in values)
                    {
                        if (!value.IsDirty) continue;
                        var parent = Parent(doc, value.Property);
                        var node = parent[value.Name];
                        if (node == null) { node = doc.CreateElement(value.Name); parent.AppendChild(node); }
                        node.InnerText = Convert.ToString(value.SerializedValue);
                    }
                    var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        doc.Save(temporary);
                        if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
                        else File.Move(temporary, FilePath);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
                finally { if (acquired) mutex.ReleaseMutex(); }
            }
        }
    }
}

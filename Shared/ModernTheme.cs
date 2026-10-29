using System;
using System.Windows;
using System.Windows.Media;

namespace EclipsePOS.Platform
{
    internal static class ModernTheme
    {
        internal static bool Dark;
        internal static void Apply(bool dark)
        {
            Dark = dark;
            var resources = Application.Current.Resources;
            Set(resources, "Surface", dark ? "#192335" : "#FFFFFF");
            Set(resources, "Canvas", dark ? "#101827" : "#F0F3F8");
            Set(resources, "Ink", dark ? "#F1F5FA" : "#182235");
            Set(resources, "Muted", dark ? "#ACBBD0" : "#56657A");
            Set(resources, "Line", dark ? "#344259" : "#D9E1EC");
            Set(resources, "Accent", dark ? "#6EE7CF" : "#0B7566");
        }
        private static void Set(ResourceDictionary dictionary, string name, string color)
        {
            var value = (Color)ColorConverter.ConvertFromString(color);
            dictionary[name + "Color"] = value;
            dictionary[name + "Brush"] = new SolidColorBrush(value);
        }
    }
}

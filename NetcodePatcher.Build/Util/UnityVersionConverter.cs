using System;
using System.ComponentModel;
using System.Globalization;
using NetcodePatcher.Build.SymbolResolution;

namespace NetcodePatcher.Build.Util;

public class UnityVersionConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string casted) return UnityVersion.Parse(casted);
        return base.ConvertFrom(context, culture, value);
    }
    public override object ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is  UnityVersion unityVersion && destinationType == typeof(string)) return unityVersion.ToString();
        return base.ConvertTo(context, culture, value, destinationType);
    }
}

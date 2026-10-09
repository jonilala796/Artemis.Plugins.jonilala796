using System;
using System.Reflection;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Attributes;
using Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Enum;
using RGB.NET.Core;

namespace Artemis.Plugins.Devices.Nanoleaf.RGB.NET.Helper;

/// <summary>
/// Offers some extensions and helper-methods for enum related things.
/// </summary>
internal static class EnumExtension
{
    /// <summary>
    /// Gets the value of the <see cref="SideLengthAttribute"/>.
    /// </summary>
    /// <param name="source">The enum value to get the attribute from</param>
    /// <returns>The value of the <see cref="SideLengthAttribute"/> of the source.</returns>
    internal static float? GetSideLength(this System.Enum source) => source.GetAttribute<SideLengthAttribute>()?.SideLength;
    
    internal static Shape? GetShape(this System.Enum source) => source.GetAttribute<ShapeAttribute>()?.Shape;
    
    internal static ExtControlVersion? GetExtControlVersion(this System.Enum source) => source.GetAttribute<ExtControlVersionAttribute>()?.ExtControlVersion;

    /// <summary>
    /// Gets the attribute of type T.
    /// </summary>
    /// <param name="source">The enum value to get the attribute from</param>
    /// <typeparam name="T">The generic attribute type</typeparam>
    /// <returns>The <see cref="Attribute"/>.</returns>
    private static T? GetAttribute<T>(this System.Enum source)
        where T : Attribute
    {
        FieldInfo? fi = source.GetType().GetField(source.ToString());
        if (fi == null) return null;
        T[] attributes = (T[])fi.GetCustomAttributes(typeof(T), false);
        return attributes.Length > 0 ? attributes[0] : null;
    }
}
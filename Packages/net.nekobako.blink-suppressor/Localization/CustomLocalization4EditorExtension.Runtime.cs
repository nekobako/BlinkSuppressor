#if UNITY_2020_2_OR_NEWER
#define CSHARP_NULLABLE_SUPPORTED
#endif

using System;
using System.Reflection;
#if CSHARP_NULLABLE_SUPPORTED
using System.Diagnostics.CodeAnalysis;
#else
using AllowNullAttribute = JetBrains.Annotations.CanBeNullAttribute;
using DisallowNullAttribute = JetBrains.Annotations.NotNullAttribute;
using MaybeNullAttribute = JetBrains.Annotations.CanBeNullAttribute;
using NotNullAttribute = JetBrains.Annotations.NotNullAttribute;
#endif
using UnityEngine;

namespace CustomLocalization4EditorExtension
{
    /// <summary>
    /// Use CL4EE Localization instance of other Assembly for this assembly.
    /// You can share CL4EE Localization instance between multiple assembly using this attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
#if COM_ANATAWA12_CUSTOM_LOCALIZATION_FOR_EDITOR_EXTENSION_AS_PACKAGE
    public
#else
    internal
#endif
        // ReSharper disable once InconsistentNaming
        sealed class RedirectCL4EEInstanceAttribute : Attribute
    {
        public string RedirectToName { get; }

        public RedirectCL4EEInstanceAttribute(string redirectToName)
        {
            RedirectToName = redirectToName;
        }
    }

    /// <summary>
    /// Put LocalePicker on the top of the field.
    /// Due to technical reasons, You need specify target Assembly
    /// </summary>
#if COM_ANATAWA12_CUSTOM_LOCALIZATION_FOR_EDITOR_EXTENSION_AS_PACKAGE
    public
#else
    internal
#endif
        // ReSharper disable once InconsistentNaming
        sealed class CL4EELocalePickerAttribute : PropertyAttribute
    {
        public Assembly TargetAssembly { get; }

        public CL4EELocalePickerAttribute(Assembly targetAssembly) => TargetAssembly = targetAssembly;

        public CL4EELocalePickerAttribute(Type typeBelongsToTargetAssembly) :
            this(typeBelongsToTargetAssembly.Assembly)
        {
        }

#if !COM_ANATAWA12_CUSTOM_LOCALIZATION_FOR_EDITOR_EXTENSION_AS_PACKAGE
        // as a embed library, assembly of localization library & tool are same.
        public CL4EELocalePickerAttribute() : this(typeof(CL4EELocalePickerAttribute).Assembly)
        {

        }
#endif
    }

    /// <summary>
    /// Use localized name for the property name in the Inspector.
    /// If you want to combine with other PropertyDrawer (e.g. <see cref="RangeAttribute"/>),
    /// Please put this attribute at the top of PropertyDrawer.
    /// </summary>
#if COM_ANATAWA12_CUSTOM_LOCALIZATION_FOR_EDITOR_EXTENSION_AS_PACKAGE
    public
#else
    internal
#endif
        // ReSharper disable once InconsistentNaming
        sealed class CL4EELocalizedAttribute : PropertyAttribute
    {
        [NotNull] public string LocalizationKey { get; }
        [MaybeNull] public string TooltipKey { get; }

        public CL4EELocalizedAttribute([DisallowNull] string localizationKey) : this(localizationKey, null) {}

        public CL4EELocalizedAttribute([DisallowNull] string localizationKey, [AllowNull] string tooltipKey)
        {
            LocalizationKey = localizationKey ?? throw new ArgumentNullException(nameof(localizationKey));
            TooltipKey = tooltipKey;
        }
    } 
}

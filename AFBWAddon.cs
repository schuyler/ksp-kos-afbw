using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using kOS.Safe.Encapsulation;
using kOS.Safe.Encapsulation.Suffixes;
using kOS.Suffixed;

namespace kOS.AddOns.AFBW
{
    [kOSAddon("AFBW")]
    [kOS.Safe.Utilities.KOSNomenclature("AFBWAddon")]
    public class AFBWAddon : Suffixed.Addon
    {
        // Cached reflection handles
        private static bool _reflected;
        private static Type _afbwType;
        private static FieldInfo _disabledField;
        private static FieldInfo _toolbarField;
        private static PropertyInfo _instanceProp;

        public AFBWAddon(SharedObjects shared) : base(shared)
        {
            InitializeSuffixes();
        }

        private void InitializeSuffixes()
        {
            AddSuffix("ENABLED", new SetSuffix<BooleanValue>(
                () => GetEnabled(),
                value => SetEnabled(value),
                "Global AFBW enable/disable toggle"));
        }

        public override BooleanValue Available()
        {
            EnsureReflected();
            return _afbwType != null;
        }

        private static void EnsureReflected()
        {
            if (_reflected) return;
            _reflected = true;

            _afbwType = AssemblyLoader.loadedAssemblies
                .SelectMany(a => { try { return a.assembly.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.FullName == "KSPAdvancedFlyByWire.AdvancedFlyByWire");

            if (_afbwType == null)
            {
                Debug.Log("[kOS-AFBW] AdvancedFlyByWire type not found; addon unavailable");
                return;
            }

            _disabledField = _afbwType.GetField("rightClickDisabled",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            _instanceProp = _afbwType.GetProperty("Instance",
                BindingFlags.Static | BindingFlags.Public);
            _toolbarField = _afbwType.GetField("toolbarControl",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        private BooleanValue GetEnabled()
        {
            EnsureReflected();
            if (_disabledField == null) return false;
            return !(bool)_disabledField.GetValue(null);
        }

        private void SetEnabled(BooleanValue value)
        {
            EnsureReflected();
            if (_disabledField == null || _instanceProp == null) return;

            bool wantDisabled = !value;
            _disabledField.SetValue(null, wantDisabled);

            // Sync toolbar icon
            var instance = _instanceProp.GetValue(null);
            if (instance == null || _toolbarField == null) return;

            var toolbar = _toolbarField.GetValue(instance);
            if (toolbar == null) return;

            var setTexture = toolbar.GetType().GetMethod("SetTexture",
                new[] { typeof(string), typeof(string) });
            if (setTexture == null) return;

            if (wantDisabled)
                setTexture.Invoke(toolbar, new object[] {
                    "ksp-advanced-flybywire/Textures/toolbar_btn_disabled_38",
                    "ksp-advanced-flybywire/Textures/toolbar_btn_disabled" });
            else
                setTexture.Invoke(toolbar, new object[] {
                    "ksp-advanced-flybywire/Textures/toolbar_btn_38",
                    "ksp-advanced-flybywire/Textures/toolbar_btn" });
        }
    }
}

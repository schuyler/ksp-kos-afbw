using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using kOS.Safe.Encapsulation;
using kOS.Safe.Encapsulation.Suffixes;
using kOS.Suffixed;

// kOS's Bootstrapper filters the assemblies it walks for [kOSAddon]-decorated
// types to those whose dllName equals "kOS", starts with "kOS.", or that
// declare a KSPAssemblyDependency on kOS. Our DLL is "kOS-AFBW" (hyphen, not
// period), so without this attribute kOS never scans us and the addon is
// invisible to scripts.
[assembly: KSPAssembly("kOS-AFBW", 0, 1)]
[assembly: KSPAssemblyDependency("kOS", 1, 4)]

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

        // Reflection handles for the throttle-latch workaround. AFBW's FlightManager
        // never clears m_Throttle/m_WheelThrottle's *value* on disable (see SetEnabled),
        // only kOS's own polling loop; we reach in and zero it ourselves.
        private static FieldInfo _flightManagerField;
        private static FieldInfo _throttleField;
        private static FieldInfo _wheelThrottleField;
        private static MethodInfo _propSetValue;

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
            AddSuffix("THROTTLE_RELEASE_BOUND", new Suffix<BooleanValue>(
                () => ThrottleReleaseBound(),
                "TRUE if the reflection handles needed to release the throttle/wheel " +
                "throttle axes on ENABLED:FALSE were found. FALSE means disabling AFBW " +
                "will stop pitch/roll/yaw input but leave a stuck throttle."));
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

            _flightManagerField = _afbwType.GetField("m_FlightManager",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (_flightManagerField == null)
            {
                Debug.Log("[kOS-AFBW] AdvancedFlyByWire.m_FlightManager not found; " +
                          "ENABLED:FALSE will not release the throttle axis");
                return;
            }

            Type flightManagerType = _flightManagerField.FieldType;
            _throttleField = flightManagerType.GetField("m_Throttle",
                BindingFlags.Instance | BindingFlags.Public);
            _wheelThrottleField = flightManagerType.GetField("m_WheelThrottle",
                BindingFlags.Instance | BindingFlags.Public);
            if (_throttleField == null || _wheelThrottleField == null)
            {
                Debug.Log("[kOS-AFBW] FlightManager.m_Throttle/m_WheelThrottle not found; " +
                          "ENABLED:FALSE will not release the throttle axis");
                return;
            }

            Type flightPropertyType = _throttleField.FieldType;
            _propSetValue = flightPropertyType.GetMethod("SetValue", new[] { typeof(float) });
            if (_propSetValue == null)
                Debug.Log("[kOS-AFBW] FlightProperty.SetValue not found; " +
                          "ENABLED:FALSE will not release the throttle axis");
        }

        private static BooleanValue ThrottleReleaseBound()
        {
            EnsureReflected();
            return _flightManagerField != null && _throttleField != null &&
                   _wheelThrottleField != null && _propSetValue != null;
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
            if (_disabledField == null)
            {
                Debug.Log("[kOS-AFBW] rightClickDisabled field not found; ENABLED write ignored");
                return;
            }

            bool wantDisabled = !value;
            _disabledField.SetValue(null, wantDisabled);

            // rightClickDisabled only stops AFBW's per-controller polling; it does not
            // touch FlightManager's throttle FlightProperty, whose *value* (as opposed
            // to velocity/acceleration) survives being disabled and gets re-applied to
            // the throttle every physics tick. Clear it so disabling AFBW actually lets
            // go of the throttle axis. This does not depend on the toolbar icon handle
            // below, and must not be skipped just because that handle is missing.
            if (wantDisabled) ReleaseThrottleLatch();

            // Sync toolbar icon (best-effort, independent of the write above)
            if (_instanceProp == null) return;
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

        // Zeroes FlightManager.m_Throttle/m_WheelThrottle once, at the moment ENABLED
        // goes to FALSE. Nothing repopulates FlightProperty::m_Value while AFBW's
        // polling loop is stopped -- EvaluateContinuousAction is the only other writer,
        // and it is reached exclusively through the same polling loop rightClickDisabled
        // just stopped -- so a one-shot clear here holds for as long as AFBW stays
        // disabled. Not restored on re-enable: m_Value is an offset added to whatever
        // the throttle already is, not an absolute lever position, so zero is the
        // correct idle value; restoring a stale offset would just re-inject it and
        // reproduce the bug this works around.
        private static void ReleaseThrottleLatch()
        {
            if (_instanceProp == null || _flightManagerField == null ||
                _throttleField == null || _wheelThrottleField == null || _propSetValue == null)
            {
                Debug.Log("[kOS-AFBW] throttle-release reflection handles missing; " +
                          "ENABLED:FALSE will not release the throttle axis");
                return;
            }

            var instance = _instanceProp.GetValue(null);
            if (instance == null) return; // outside a flight scene; nothing to release

            var flightManager = _flightManagerField.GetValue(instance);
            if (flightManager == null) return;

            ZeroProperty(flightManager, _throttleField);
            ZeroProperty(flightManager, _wheelThrottleField);
        }

        private static void ZeroProperty(object flightManager, FieldInfo propertyField)
        {
            var property = propertyField.GetValue(flightManager);
            if (property == null) return;
            _propSetValue.Invoke(property, new object[] { 0f });
        }
    }
}

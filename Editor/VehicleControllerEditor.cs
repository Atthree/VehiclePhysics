using UnityEditor;
using UnityEngine;

namespace Atthree.Vehicles.Editor
{
    /// <summary>VehicleController Inspector'ının üstüne "profil uygula" satırı ekler.</summary>
    [CustomEditor(typeof(VehicleController))]
    public class VehicleControllerEditor : UnityEditor.Editor
    {
        static int presetIndex;
        static VehiclePreset customPreset;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Profil Uygula", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                presetIndex = EditorGUILayout.Popup(presetIndex, VehiclePreset.BuiltInNames);
                if (GUILayout.Button("Uygula", GUILayout.Width(70))) Apply(VehiclePreset.BuiltIn(presetIndex));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                customPreset = (VehiclePreset)EditorGUILayout.ObjectField(customPreset, typeof(VehiclePreset), false);
                using (new EditorGUI.DisabledScope(customPreset == null))
                    if (GUILayout.Button("Uygula", GUILayout.Width(70))) Apply(customPreset);
            }
            EditorGUILayout.HelpBox("Kütle, motor, fren, direksiyon ve TÜM tekerleklerin süspansiyon/lastik ayarlarını değiştirir. " +
                                    "Süspansiyon boyu değişirse tekerlekler dururken biraz farklı yükseklikte durabilir.", MessageType.None);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        void Apply(VehiclePreset preset)
        {
            var vehicle = (VehicleController)target;
            var objects = new System.Collections.Generic.List<Object> { vehicle };
            var body = vehicle.GetComponent<Rigidbody>();
            if (body != null) objects.Add(body);
            objects.AddRange(vehicle.GetComponentsInChildren<VehicleWheel>(true));
            Undo.RecordObjects(objects.ToArray(), "Apply Vehicle Preset");
            preset.ApplyTo(vehicle);
            foreach (var o in objects) EditorUtility.SetDirty(o);
        }
    }
}

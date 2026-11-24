using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Atthree.Vehicles.Editor
{
    /// <summary>
    /// Kendi araba modelini sürülebilir yap: Tools > Vehicle Physics > Builder.
    /// Aracın kökünü ve dört tekerlek mesh'ini sürükle, bir profil seç, Build'e bas.
    /// </summary>
    public class VehicleBuilderWindow : EditorWindow
    {
        GameObject root;
        Transform frontLeft, frontRight, rearLeft, rearRight;
        int presetIndex;
        VehiclePreset customPreset;
        bool addPlayerInput = true, addHud = true, setupCamera = true;
        Vector2 scroll;

        [MenuItem("Tools/Vehicle Physics/Builder (Your Own Model)", priority = 20)]
        static void Open()
        {
            var w = GetWindow<VehicleBuilderWindow>("Vehicle Builder");
            w.minSize = new Vector2(360, 460);
            if (Selection.activeGameObject != null) w.SetRoot(Selection.activeGameObject);
        }

        void SetRoot(GameObject value)
        {
            root = value;
            frontLeft = frontRight = rearLeft = rearRight = null;
            if (root != null) VehicleSetup.FindWheels(root, out frontLeft, out frontRight, out rearLeft, out rearRight);
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(
                "1) Arabayı sahneye koy: önü mavi oka (+Z) baksın, tekerlekler yerde.\n" +
                "2) Kökü sürükle; tekerlekler isimden bulunamazsa elle ata.\n" +
                "3) Profil seç, Build. Play: WASD sür, Space el freni, R düzelt.",
                MessageType.Info);

            var newRoot = (GameObject)EditorGUILayout.ObjectField("Araç Kökü", root, typeof(GameObject), true);
            if (newRoot != root) SetRoot(newRoot);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tekerlek Mesh'leri", EditorStyles.boldLabel);
            frontLeft = (Transform)EditorGUILayout.ObjectField("Ön Sol", frontLeft, typeof(Transform), true);
            frontRight = (Transform)EditorGUILayout.ObjectField("Ön Sağ", frontRight, typeof(Transform), true);
            rearLeft = (Transform)EditorGUILayout.ObjectField("Arka Sol", rearLeft, typeof(Transform), true);
            rearRight = (Transform)EditorGUILayout.ObjectField("Arka Sağ", rearRight, typeof(Transform), true);
            if (root != null && GUILayout.Button("Tekerlekleri isimden tekrar bul")) SetRoot(root);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Profil", EditorStyles.boldLabel);
            presetIndex = EditorGUILayout.Popup("Hazır Profil", presetIndex, VehiclePreset.BuiltInNames);
            customPreset = (VehiclePreset)EditorGUILayout.ObjectField(
                new GUIContent("Kendi Profilin", "Doluysa hazır profil yerine bu kullanılır.\nCreate > Vehicle Physics > Vehicle Preset"),
                customPreset, typeof(VehiclePreset), false);

            EditorGUILayout.Space();
            addPlayerInput = EditorGUILayout.Toggle("Oyuncu Girdisi Ekle", addPlayerInput);
            addHud = EditorGUILayout.Toggle("Gösterge (HUD) Ekle", addHud);
            setupCamera = EditorGUILayout.Toggle("Kamerayı Kur", setupCamera);

            string error = Validate();
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            string warning = ForwardWarning();
            if (warning != null) EditorGUILayout.HelpBox(warning, MessageType.Warning);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(error != null))
            {
                if (GUILayout.Button("Build", GUILayout.Height(32))) Build();
            }
            EditorGUILayout.EndScrollView();
        }

        void Build()
        {
            Undo.SetCurrentGroupName("Build Vehicle");
            int group = Undo.GetCurrentGroup();
            Undo.RegisterFullObjectHierarchyUndo(root, "Build Vehicle");

            var created = new List<GameObject>();
            var options = new VehicleSetup.Options
            {
                preset = customPreset != null ? customPreset : VehiclePreset.BuiltIn(presetIndex),
                addPlayerInput = addPlayerInput,
                addHud = addHud,
            };
            var vehicle = VehicleSetup.Setup(root, frontLeft, frontRight, rearLeft, rearRight, options, created);
            foreach (var go in created) Undo.RegisterCreatedObjectUndo(go, "Build Vehicle");

            if (setupCamera)
            {
                if (Camera.main != null) Undo.RegisterFullObjectHierarchyUndo(Camera.main.gameObject, "Setup Camera");
                VehicleSetup.SetupCamera(vehicle);
            }

            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(root);
            Selection.activeGameObject = root;
            Debug.Log($"[Vehicle Builder] '{root.name}' sürülebilir hale getirildi ({options.preset.name} profili).", root);
        }

        string Validate()
        {
            if (root == null) return "Araç kökünü seç.";
            if (EditorUtility.IsPersistent(root)) return "Prefab asset'i değil, SAHNEDEKİ aracı seç.";
            if (frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
                return "Dört tekerleği de ata.";
            foreach (var t in new[] { frontLeft, frontRight, rearLeft, rearRight })
                if (!t.IsChildOf(root.transform) || t == root.transform) return $"'{t.name}' aracın altında bir obje olmalı.";
            return null;
        }

        /// <summary>Ön tekerlekler kökün +Z yönünde değilse model yanlış yöne bakıyordur.</summary>
        string ForwardWarning()
        {
            if (root == null || frontLeft == null || frontRight == null || rearLeft == null || rearRight == null) return null;
            Vector3 front = (frontLeft.GetComponentInChildren<Renderer>()?.bounds.center ?? frontLeft.position)
                          + (frontRight.GetComponentInChildren<Renderer>()?.bounds.center ?? frontRight.position);
            Vector3 rear = (rearLeft.GetComponentInChildren<Renderer>()?.bounds.center ?? rearLeft.position)
                         + (rearRight.GetComponentInChildren<Renderer>()?.bounds.center ?? rearRight.position);
            Vector3 dir = root.transform.InverseTransformDirection((front - rear).normalized);
            if (dir.z > 0.9f) return null;
            return "Ön tekerlekler aracın ileri yönünde (+Z, mavi ok) değil: araç yanlış yöne bakıyor.\n" +
                   "Çözüm: boş bir GameObject oluştur, modeli onun altına koy ve modeli önü mavi oka bakacak şekilde " +
                   "döndür. Sonra boş objeyi 'Araç Kökü' olarak kullan.";
        }
    }
}

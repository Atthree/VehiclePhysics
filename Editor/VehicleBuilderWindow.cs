using UnityEditor;
using UnityEngine;

namespace Atthree.Vehicles.Editor
{
    /// <summary>
    /// Kendi araba modelini sürülebilir yap: Tools > Vehicle Physics > Builder.
    /// Aracın kökünü ve dört tekerlek mesh'ini sürükle, Build'e bas.
    /// </summary>
    public class VehicleBuilderWindow : EditorWindow
    {
        GameObject root;
        Transform frontLeft, frontRight, rearLeft, rearRight;
        readonly VehicleSetup.Options options = new VehicleSetup.Options();
        Vector2 scroll;

        [MenuItem("Tools/Vehicle Physics/Builder (Your Own Model)", priority = 20)]
        static void Open()
        {
            var w = GetWindow<VehicleBuilderWindow>("Vehicle Builder");
            w.minSize = new Vector2(340, 420);
            if (Selection.activeGameObject != null) w.root = Selection.activeGameObject;
            w.TryAutoFindWheels();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(
                "1) Arabayı sahneye koy: ileri yönü mavi ok (+Z), tekerlekler yerde.\n" +
                "2) Kökü ve dört tekerlek mesh'ini aşağıya sürükle.\n" +
                "3) Build. Play: WASD sür, Space el freni, R düzelt.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            root = (GameObject)EditorGUILayout.ObjectField("Araç Kökü", root, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                frontLeft = frontRight = rearLeft = rearRight = null; // yeni araç: eski atamaları unut
                TryAutoFindWheels();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tekerlek Mesh'leri", EditorStyles.boldLabel);
            frontLeft = (Transform)EditorGUILayout.ObjectField("Ön Sol", frontLeft, typeof(Transform), true);
            frontRight = (Transform)EditorGUILayout.ObjectField("Ön Sağ", frontRight, typeof(Transform), true);
            rearLeft = (Transform)EditorGUILayout.ObjectField("Arka Sol", rearLeft, typeof(Transform), true);
            rearRight = (Transform)EditorGUILayout.ObjectField("Arka Sağ", rearRight, typeof(Transform), true);
            if (root != null && GUILayout.Button("Tekerlekleri isimden bul")) TryAutoFindWheels();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ayarlar", EditorStyles.boldLabel);
            options.mass = EditorGUILayout.FloatField("Kütle (kg)", options.mass);
            options.suspensionLength = EditorGUILayout.FloatField("Süspansiyon Boyu (m)", options.suspensionLength);
            options.springStiffness = EditorGUILayout.FloatField("Yay Sertliği (N/m)", options.springStiffness);
            options.bumpDamper = EditorGUILayout.FloatField("Amortisör: Sıkışma", options.bumpDamper);
            options.reboundDamper = EditorGUILayout.FloatField("Amortisör: Açılma", options.reboundDamper);
            options.frontWheelDrive = EditorGUILayout.Toggle("Önden Çekiş", options.frontWheelDrive);
            options.rearWheelDrive = EditorGUILayout.Toggle("Arkadan İtiş", options.rearWheelDrive);
            options.addPlayerInput = EditorGUILayout.Toggle("Oyuncu Girdisi Ekle", options.addPlayerInput);
            options.addHud = EditorGUILayout.Toggle("Gösterge (HUD) Ekle", options.addHud);
            bool setupCamera = EditorPrefs.GetBool("Atthree.Vehicles.SetupCamera", true);
            setupCamera = EditorGUILayout.Toggle("Kamerayı Kur", setupCamera);
            EditorPrefs.SetBool("Atthree.Vehicles.SetupCamera", setupCamera);

            string error = Validate();
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(error != null))
            {
                if (GUILayout.Button("Build", GUILayout.Height(32)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(root, "Build Vehicle");
                    var vehicle = VehicleSetup.Setup(root, frontLeft, frontRight, rearLeft, rearRight, options);
                    // Yeni eklenen bileşen ve objeler için:
                    foreach (var w in root.GetComponentsInChildren<VehicleWheel>())
                        Undo.RegisterCreatedObjectUndo(w.gameObject, "Build Vehicle");
                    if (setupCamera) VehicleSetup.SetupCamera(vehicle);
                    EditorUtility.SetDirty(root);
                    Selection.activeGameObject = root;
                    Debug.Log($"[Vehicle Builder] '{root.name}' sürülebilir hale getirildi.", root);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        string Validate()
        {
            if (root == null) return "Araç kökünü seç.";
            if (EditorUtility.IsPersistent(root)) return "Prefab asset'i değil, SAHNEDEKİ aracı seç.";
            if (frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
                return "Dört tekerleği de ata.";
            foreach (var t in new[] { frontLeft, frontRight, rearLeft, rearRight })
                if (!t.IsChildOf(root.transform)) return $"'{t.name}' aracın altında değil.";
            return null;
        }

        /// <summary>Yaygın isimlerden tekerlekleri bulmaya çalışır (FL, Wheel_FL, FrontLeft...).</summary>
        void TryAutoFindWheels()
        {
            if (root == null) return;
            var all = root.GetComponentsInChildren<Transform>(true);
            frontLeft ??= Find(all, "fl", "frontleft", "front_left", "wheel_fl", "wheelfl", "lf", "wheel_front_left");
            frontRight ??= Find(all, "fr", "frontright", "front_right", "wheel_fr", "wheelfr", "rf", "wheel_front_right");
            rearLeft ??= Find(all, "rl", "rearleft", "rear_left", "wheel_rl", "wheelrl", "lr", "backleft", "wheel_rear_left");
            rearRight ??= Find(all, "rr", "rearright", "rear_right", "wheel_rr", "wheelrr", "backright", "wheel_rear_right");
        }

        static Transform Find(Transform[] all, params string[] keys)
        {
            foreach (var t in all)
            {
                string n = t.name.ToLowerInvariant().Replace(" ", "").Replace("-", "_");
                foreach (var k in keys)
                    if (n == k || n.EndsWith("_" + k) || n.EndsWith(k) && n.Contains("wheel"))
                        return t;
            }
            return null;
        }
    }
}

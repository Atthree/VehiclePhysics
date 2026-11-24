using System.Collections.Generic;
using UnityEngine;

namespace Atthree.Vehicles
{
    /// <summary>
    /// Herhangi bir araba modelini sürülebilir hale getirir (oyun içinden de, editörden de çağrılabilir).
    /// Editördeki "Vehicle Builder" penceresi bunu Undo kaydıyla çağırır.
    ///
    /// Yaptıkları:
    ///  - Köke Rigidbody + VehicleController (+ girdi, HUD) ekler ve seçilen profili (preset) uygular.
    ///  - Tekerlek mesh'lerindeki collider'ları kaldırır (tekerleği süspansiyon taşır, collider değil).
    ///  - Gövdede collider yoksa mesh sınırlarından BoxCollider ekler; içbükey (non-convex)
    ///    MeshCollider'ları dışbükey yapar (hareketli Rigidbody içbükey collider kullanamaz).
    ///  - Tekerlek mesh'inin pivotu tekerleğin merkezinde değilse araya merkezde bir pivot objesi koyar
    ///    (yoksa tekerlek kendi ekseni yerine başka bir noktanın etrafında dönerek "yalpalar").
    ///  - Her tekerlek için "Wheel FL" gibi bir süspansiyon noktası oluşturur. Bu noktanın yüksekliği,
    ///    araç dururken tekerleğin tam modeldeki yerinde olacağı şekilde hesaplanır.
    /// </summary>
    public static class VehicleSetup
    {
        public class Options
        {
            [Tooltip("Boşsa VehiclePreset.Default.")]
            public VehiclePreset preset;
            public bool addPlayerInput = true;
            public bool addHud = true;
        }

        /// <param name="root">Aracın kökü (ileri yönü +Z, yukarısı +Y olmalı).</param>
        /// <param name="frontLeft">Görsel tekerlek mesh'leri.</param>
        /// <param name="createdObjects">Oluşturulan yeni objeler eklenir (editörde Undo için).</param>
        public static VehicleController Setup(GameObject root, Transform frontLeft, Transform frontRight,
            Transform rearLeft, Transform rearRight, Options options, List<GameObject> createdObjects = null)
        {
            var preset = options.preset != null ? options.preset : VehiclePreset.Default;
            var wheelVisuals = new[] { frontLeft, frontRight, rearLeft, rearRight };

            // --- Rigidbody
            var body = root.GetComponent<Rigidbody>();
            if (body == null) body = root.AddComponent<Rigidbody>();
            body.mass = preset.mass;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // --- Tekerlek mesh'lerindeki collider'lar çarpışmayı bozar: kaldır.
            foreach (var visual in wheelVisuals)
                foreach (var c in visual.GetComponentsInChildren<Collider>())
                    Object.DestroyImmediate(c); // Destroy bekletir; kurulumun devamı collider'ı görmesin

            // --- Gövde collider'ı
            foreach (var mc in root.GetComponentsInChildren<MeshCollider>())
                if (!mc.convex) mc.convex = true;
            if (root.GetComponentInChildren<Collider>() == null)
                AddBodyCollider(root, wheelVisuals);

            // --- Tekerlekler
            CreateWheel(root, frontLeft, Axle.Front, Side.Left, preset, createdObjects);
            CreateWheel(root, frontRight, Axle.Front, Side.Right, preset, createdObjects);
            CreateWheel(root, rearLeft, Axle.Rear, Side.Left, preset, createdObjects);
            CreateWheel(root, rearRight, Axle.Rear, Side.Right, preset, createdObjects);

            var controller = root.GetComponent<VehicleController>();
            if (controller == null) controller = root.AddComponent<VehicleController>();
            preset.ApplyTo(controller);
            if (options.addPlayerInput && root.GetComponent<PlayerVehicleInput>() == null) root.AddComponent<PlayerVehicleInput>();
            if (options.addHud && root.GetComponent<VehicleHud>() == null) root.AddComponent<VehicleHud>();
            return controller;
        }

        static void CreateWheel(GameObject root, Transform visual, Axle axle, Side side, VehiclePreset preset,
            List<GameObject> createdObjects)
        {
            float radius = MeasureWheelRadius(visual, root.transform);
            Vector3 up = root.transform.up;
            Vector3 center = RendererCenter(visual);
            visual = EnsureCenteredPivot(visual, center, root.transform, createdObjects);

            // Dinlenmede: anchor ile tekerlek merkezi arası = suspensionLength - statik sıkışma.
            Vector3 anchor = center + up * (preset.suspensionLength - preset.StaticCompression);

            string name = $"Wheel {(axle == Axle.Front ? "F" : "R")}{(side == Side.Left ? "L" : "R")}";
            var existing = root.transform.Find(name);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject(name);
                createdObjects?.Add(go);
            }
            go.transform.SetParent(root.transform, false);
            go.transform.position = anchor;
            go.transform.rotation = root.transform.rotation;

            var wheel = go.GetComponent<VehicleWheel>();
            if (wheel == null) wheel = go.AddComponent<VehicleWheel>();
            wheel.axle = axle;
            wheel.side = side;
            wheel.visual = visual;
            wheel.radius = radius;
            wheel.steer = axle == Axle.Front;
            wheel.handbrake = axle == Axle.Rear;
            preset.ApplyTo(wheel);
        }

        /// <summary>
        /// Tekerlek döndürülürken pivotunun etrafında döner. Pivot tekerleğin merkezinde değilse
        /// (dışarıdan alınan modellerde çok sık), merkezde boş bir obje oluşturup mesh'i onun altına taşır.
        /// </summary>
        static Transform EnsureCenteredPivot(Transform visual, Vector3 center, Transform root, List<GameObject> createdObjects)
        {
            if (Vector3.Distance(visual.position, center) < 0.01f) return visual;

            var pivot = new GameObject(visual.name + " Pivot").transform;
            createdObjects?.Add(pivot.gameObject);
            pivot.SetParent(visual.parent, false);
            pivot.SetPositionAndRotation(center, root.rotation);
            visual.SetParent(pivot, true);
            return pivot;
        }

        /// <summary>Tekerlek yarıçapı: mesh sınırlarının araca göre dikey yarısı.</summary>
        static float MeasureWheelRadius(Transform visual, Transform root)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0.35f;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var r in renderers)
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    float y = root.InverseTransformPoint(corner).y;
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            // Bounds dünya eksenli olduğu için döndürülmüş mesh'te biraz büyük çıkabilir; makul aralıkta tut.
            return Mathf.Clamp((maxY - minY) * 0.5f * root.lossyScale.y, 0.05f, 2f);
        }

        static Vector3 RendererCenter(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return t.position;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b.center;
        }

        static void AddBodyCollider(GameObject root, Transform[] wheelVisuals)
        {
            Bounds? bounds = null;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                bool isWheel = false;
                foreach (var w in wheelVisuals) if (r.transform.IsChildOf(w)) { isWheel = true; break; }
                if (isWheel) continue;
                if (bounds == null) bounds = r.bounds; else { var b = bounds.Value; b.Encapsulate(r.bounds); bounds = b; }
            }
            if (bounds == null) return;

            var box = root.AddComponent<BoxCollider>();
            Vector3 scale = root.transform.lossyScale;
            box.center = root.transform.InverseTransformPoint(bounds.Value.center);
            box.size = new Vector3(bounds.Value.size.x / scale.x, bounds.Value.size.y / scale.y, bounds.Value.size.z / scale.z);
        }

        /// <summary>Kameraya VehicleCamera ekler (yoksa kamera oluşturur) ve aracı hedef yapar.</summary>
        public static VehicleCamera SetupCamera(VehicleController vehicle)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                cam = go.GetComponent<Camera>();
            }
            var follow = cam.GetComponent<VehicleCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<VehicleCamera>();
            follow.target = vehicle;
            cam.transform.position = vehicle.transform.position - vehicle.transform.forward * 6f + Vector3.up * 2.5f;
            cam.transform.LookAt(vehicle.transform.position + Vector3.up);
            return follow;
        }

        // ------------------------------------------------------------------ tekerlek bulma
        /// <summary>
        /// Yaygın isimlendirmelerden tekerlekleri bulur: FL, Wheel_FL, wheel.fl, FrontLeftWheel,
        /// Wheel_Front_Left, LF ... Bulamadığı için null döner.
        /// </summary>
        public static void FindWheels(GameObject root, out Transform frontLeft, out Transform frontRight,
            out Transform rearLeft, out Transform rearRight)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            frontLeft = Find(all, "fl", "lf", "frontleft", "leftfront");
            frontRight = Find(all, "fr", "rf", "frontright", "rightfront");
            rearLeft = Find(all, "rl", "lr", "rearleft", "leftrear", "backleft", "leftback", "bl");
            rearRight = Find(all, "rr", "rearright", "rightrear", "backright", "rightback", "br");
        }

        static Transform Find(Transform[] all, params string[] keys)
        {
            foreach (var t in all)
            {
                // "Wheel_Front-Left.001" -> parçalar: wheel, front, left, 001 ; birleşik: wheelfrontleft001
                string lower = t.name.ToLowerInvariant();
                var parts = lower.Split(new[] { '_', '.', '-', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                string joined = string.Concat(parts);
                bool looksLikeWheel = lower.Contains("wheel") || lower.Contains("tire") || lower.Contains("tyre") || parts.Length == 1;
                if (!looksLikeWheel) continue;

                foreach (var key in keys)
                {
                    foreach (var p in parts) if (p == key) return t;              // Wheel_FL, wheel.fl
                    if (key.Length >= 6 && joined.Contains(key)) return t;          // FrontLeftWheel, Wheel_Front_Left
                }
            }
            return null;
        }
    }
}

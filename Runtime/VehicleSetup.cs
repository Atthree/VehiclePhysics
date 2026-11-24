using UnityEngine;

namespace Atthree.Vehicles
{
    /// <summary>
    /// Herhangi bir araba modelini sürülebilir hale getirir (oyun içinden de, editörden de çağrılabilir).
    /// Editördeki "Vehicle Builder" penceresi bunu Undo kaydıyla çağırır.
    ///
    /// Yaptıkları:
    ///  - Köke Rigidbody + VehicleController (+ girdi, HUD) ekler.
    ///  - Gövdede collider yoksa, tekerlekler hariç mesh sınırlarından bir BoxCollider ekler.
    ///  - Her tekerlek mesh'i için "Wheel FL" gibi bir süspansiyon noktası oluşturur. Bu noktanın
    ///    yüksekliği, araç dururken tekerleğin tam modeldeki yerinde olacağı şekilde hesaplanır.
    /// </summary>
    public static class VehicleSetup
    {
        public class Options
        {
            public float mass = 1200f;
            public float suspensionLength = 0.3f;
            public float springStiffness = 35000f;
            public float bumpDamper = 2500f;
            public float reboundDamper = 4000f;
            public bool frontWheelDrive = false;
            public bool rearWheelDrive = true;
            public bool addPlayerInput = true;
            public bool addHud = true;
        }

        /// <param name="root">Aracın kökü (ileri yönü +Z, yukarısı +Y olmalı).</param>
        /// <param name="frontLeft">Görsel tekerlek mesh'leri.</param>
        public static VehicleController Setup(GameObject root, Transform frontLeft, Transform frontRight,
            Transform rearLeft, Transform rearRight, Options options)
        {
            var wheelVisuals = new[] { frontLeft, frontRight, rearLeft, rearRight };

            // --- Rigidbody
            var body = root.GetComponent<Rigidbody>();
            if (body == null) body = root.AddComponent<Rigidbody>();
            body.mass = options.mass;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // --- Tekerlek mesh'lerindeki collider'lar çarpışmayı bozar: kaldır.
            foreach (var visual in wheelVisuals)
                foreach (var c in visual.GetComponentsInChildren<Collider>())
                    Object.DestroyImmediate(c); // Destroy bekletir; kurulumun devamı collider'ı görmesin

            // --- Gövde collider'ı
            if (root.GetComponentInChildren<Collider>() == null)
                AddBodyCollider(root, wheelVisuals);

            // --- Tekerlekler
            // Dururken her tekerlek ~ ağırlık/4 taşır; yay bu kadar sıkışmış olur.
            float staticCompression = Mathf.Clamp(options.mass * Mathf.Abs(Physics.gravity.y) / 4f / options.springStiffness,
                0f, options.suspensionLength * 0.8f);

            CreateWheel(root, frontLeft, Axle.Front, Side.Left, options, staticCompression);
            CreateWheel(root, frontRight, Axle.Front, Side.Right, options, staticCompression);
            CreateWheel(root, rearLeft, Axle.Rear, Side.Left, options, staticCompression);
            CreateWheel(root, rearRight, Axle.Rear, Side.Right, options, staticCompression);

            var controller = root.GetComponent<VehicleController>();
            if (controller == null) controller = root.AddComponent<VehicleController>();
            if (options.addPlayerInput && root.GetComponent<PlayerVehicleInput>() == null) root.AddComponent<PlayerVehicleInput>();
            if (options.addHud && root.GetComponent<VehicleHud>() == null) root.AddComponent<VehicleHud>();
            return controller;
        }

        static void CreateWheel(GameObject root, Transform visual, Axle axle, Side side, Options options, float staticCompression)
        {
            float radius = MeasureWheelRadius(visual, root.transform);
            Vector3 up = root.transform.up;

            // Dinlenmede: anchor ile tekerlek merkezi arası = suspensionLength - staticCompression.
            Vector3 center = RendererCenter(visual);
            Vector3 anchor = center + up * (options.suspensionLength - staticCompression);

            string name = $"Wheel {(axle == Axle.Front ? "F" : "R")}{(side == Side.Left ? "L" : "R")}";
            var existing = root.transform.Find(name);
            var go = existing != null ? existing.gameObject : new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = anchor;
            go.transform.rotation = root.transform.rotation;

            var wheel = go.GetComponent<VehicleWheel>();
            if (wheel == null) wheel = go.AddComponent<VehicleWheel>();
            wheel.axle = axle;
            wheel.side = side;
            wheel.visual = visual;
            wheel.radius = radius;
            wheel.suspensionLength = options.suspensionLength;
            wheel.springStiffness = options.springStiffness;
            wheel.bumpDamper = options.bumpDamper;
            wheel.reboundDamper = options.reboundDamper;
            wheel.steer = axle == Axle.Front;
            wheel.drive = axle == Axle.Front ? options.frontWheelDrive : options.rearWheelDrive;
            wheel.handbrake = axle == Axle.Rear;
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
    }
}

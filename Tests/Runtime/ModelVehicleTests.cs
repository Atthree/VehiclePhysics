#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Atthree.Vehicles.Tests
{
    /// <summary>
    /// Paketteki örnek modelleri (Vehicles/*.fbx), "dışarıdan alınmış model" gibi Builder'ın kullandığı
    /// VehicleSetup ile kurar ve sürer. Her model farklı bir zorluk içerir:
    ///   SportsCar : temiz model
    ///   Truck     : tekerlek pivotları merkezde değil
    ///   Buggy     : model +X yönüne bakıyor (README'deki gibi boş bir kök altında döndürülür)
    /// </summary>
    public class ModelVehicleTests
    {
        public class Case
        {
            public string model; public int preset; public float modelYaw; public float wheelRadius; public float bumpSpeed;
            public override string ToString() => model;
        }

        static readonly Case[] Cases =
        {
            new Case { model = "SportsCar", preset = 1, modelYaw = 0f,   wheelRadius = 0.33f, bumpSpeed = 12f },
            new Case { model = "Truck",     preset = 2, modelYaw = 0f,   wheelRadius = 0.50f, bumpSpeed = 9f },
            new Case { model = "Buggy",     preset = 3, modelYaw = -90f, wheelRadius = 0.46f, bumpSpeed = 12f },
        };

        GameObject track, root;
        VehicleController car;

        [SetUp] public void SetUp() => track = TestTrack.Create(Vector3.zero);

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(track);
            if (root != null) Object.Destroy(root);
        }

        /// <summary>Modeli sahneye koyar, README'deki adımlarla kurar.</summary>
        void Spawn(Case c, Vector3 position, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"Packages/com.atthree.vehicle-physics/Vehicles/{c.model}.fbx");
            Assert.NotNull(asset, "model bulunamadı: " + c.model);

            // README adım 2: yanlış eksenli modeli boş bir kök altında döndür.
            root = new GameObject(c.model + " Root");
            var model = Object.Instantiate(asset, root.transform);
            model.transform.localRotation = Quaternion.Euler(0f, c.modelYaw, 0f);

            VehicleSetup.FindWheels(root, out var fl, out var fr, out var rl, out var rr);
            Assert.NotNull(fl, "FL bulunamadı"); Assert.NotNull(fr, "FR bulunamadı");
            Assert.NotNull(rl, "RL bulunamadı"); Assert.NotNull(rr, "RR bulunamadı");
            Debug.Log($"[Model] {c.model}: wheels {fl.name}, {fr.name}, {rl.name}, {rr.name}");

            // Ön tekerlekler kökün +Z'sinde olmalı (Builder penceresinin uyarısıyla aynı kontrol).
            Vector3 dir = ((Center(fl) + Center(fr)) - (Center(rl) + Center(rr))).normalized;
            Assert.Greater(root.transform.InverseTransformDirection(dir).z, 0.9f, "araç +Z'ye bakmıyor");

            car = VehicleSetup.Setup(root, fl, fr, rl, rr, new VehicleSetup.Options
            {
                preset = VehiclePreset.BuiltIn(c.preset), addPlayerInput = false, addHud = false,
            });
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            car.Body.position = root.transform.position;
            car.Body.rotation = root.transform.rotation;
        }

        static Vector3 Center(Transform t)
        {
            var r = t.GetComponentsInChildren<Renderer>();
            if (r.Length == 0) return t.position;
            var b = r[0].bounds; foreach (var x in r) b.Encapsulate(x.bounds); return b.center;
        }

        static IEnumerator Simulate(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
        }

        float Tilt => Vector3.Angle(car.transform.up, Vector3.up);

        /// <summary>Görsel tekerleğin, süspansiyonun söylediği yerde olup olmadığı (yalpalama kontrolü).</summary>
        float MaxVisualOffset()
        {
            float max = 0f;
            foreach (var w in car.Wheels)
            {
                Vector3 expected = w.transform.position - car.transform.up * w.Distance;
                max = Mathf.Max(max, Vector3.Distance(Center(w.visual), expected));
            }
            return max;
        }

        // ------------------------------------------------------------------ düz zeminde
        [UnityTest]
        public IEnumerator FlatGround([ValueSource(nameof(Cases))] Case c)
        {
            // Pistin arkasındaki düz bölgeye doğru (-Z) bak.
            Spawn(c, new Vector3(0f, 0.05f, 20f), 180f);
            yield return Simulate(2.5f);

            // 1) Oturma
            Assert.AreEqual(4, car.GroundedWheelCount, "4 teker yerde");
            foreach (var w in car.Wheels)
                Assert.AreEqual(c.wheelRadius, w.radius, 0.02f, $"{w.name} yarıçapı mesh'ten doğru ölçülmeli");
            float centerY = car.Wheels.Average(w => w.transform.position.y - w.Distance);
            float offsetRest = MaxVisualOffset();
            Debug.Log($"[Model] {c.model} rest: wheelCenterY={centerY:F3} (radius {c.wheelRadius}) compression={car.Wheels[0].Compression01:F2} visualOffset={offsetRest:F3} pivots={root.GetComponentsInChildren<Transform>().Count(t => t.name.EndsWith(" Pivot"))}");
            Assert.AreEqual(c.wheelRadius, centerY, 0.04f, "tekerlekler yarıçap yüksekliğinde durmalı");
            Assert.Less(Mathf.Abs(car.Body.linearVelocity.y), 0.05f, "yerinde salınmamalı");

            // 2) Düz git: aracın burnu yönünde ilerlemeli.
            Vector3 start = car.transform.position;
            Vector3 forward = car.transform.forward;
            car.commands.throttle = 1f;
            float maxOffset = 0f;
            for (float t = 0; t < 3f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                maxOffset = Mathf.Max(maxOffset, MaxVisualOffset());
            }
            Vector3 moved = car.transform.position - start;
            float along = Vector3.Dot(moved, forward);
            float side = Vector3.Cross(forward, moved).magnitude;
            Debug.Log($"[Model] {c.model} straight: along={along:F1}m side={side:F2}m speed={car.ForwardSpeed:F1} maxVisualOffset={maxOffset:F3}");
            Assert.Greater(along, 10f, "ileri gitmeli");
            Assert.Less(side, 0.6f, "düz gitmeli");
            Assert.Less(maxOffset, 0.03f, "tekerlekler yalpalamadan dönmeli (pivot düzeltmesi)");

            // 3) Viraj
            car.commands.throttle = 0.6f;
            car.commands.steer = 1f;
            float yaw0 = car.transform.eulerAngles.y, maxTilt = 0f;
            for (float t = 0; t < 2.5f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                maxTilt = Mathf.Max(maxTilt, Tilt);
            }
            float turned = Mathf.DeltaAngle(yaw0, car.transform.eulerAngles.y);
            Debug.Log($"[Model] {c.model} turn: turned={turned:F0}° maxTilt={maxTilt:F1}° speed={car.Body.linearVelocity.magnitude:F1}");
            Assert.Greater(turned, 30f, "sağa dönmeli");
            Assert.Less(maxTilt, 20f, "virajda devrilmemeli");
        }

        // ------------------------------------------------------------------ bozuk yol
        [UnityTest]
        public IEnumerator BumpyTrack([ValueSource(nameof(Cases))] Case c)
        {
            Spawn(c, new Vector3(0f, 0.05f, 10f), 0f);
            yield return Simulate(1.5f);

            float maxTilt = 0f, maxRoll = 0f, maxPitch = 0f, airborne = 0f, minComp = 1f, maxComp = 0f, tiltZ = 0f;
            float timeout = Time.time + 40f;
            while (car.transform.position.z < 155f)
            {
                float heading = Mathf.DeltaAngle(0f, car.transform.eulerAngles.y);
                car.commands.steer = Mathf.Clamp(-car.transform.position.x * 0.15f - heading * 0.05f, -1f, 1f);
                car.commands.throttle = car.Body.linearVelocity.magnitude > c.bumpSpeed ? 0f : 0.8f;
                yield return new WaitForFixedUpdate();

                if (car.transform.position.z > 30f)
                {
                    if (Tilt > maxTilt) { maxTilt = Tilt; tiltZ = car.transform.position.z; }
                    maxRoll = Mathf.Max(maxRoll, Mathf.Abs(Mathf.DeltaAngle(0f, car.transform.eulerAngles.z)));
                    maxPitch = Mathf.Max(maxPitch, Mathf.Abs(Mathf.DeltaAngle(0f, car.transform.eulerAngles.x)));
                    if (car.GroundedWheelCount == 0) airborne += Time.fixedDeltaTime;
                    foreach (var w in car.Wheels) { minComp = Mathf.Min(minComp, w.Compression01); maxComp = Mathf.Max(maxComp, w.Compression01); }
                }
                if (Time.time > timeout) Assert.Fail($"pisti geçemedi, z={car.transform.position.z:F0}");
            }
            Debug.Log($"[Model] {c.model} bumps @{c.bumpSpeed} m/s: maxTilt={maxTilt:F1}° at z={tiltZ:F0} (roll {maxRoll:F1}°, pitch {maxPitch:F1}°) airborne={airborne:F2}s compression {minComp:F2}..{maxComp:F2}");
            // Takla yana yatmayla olur; öne-arka yatma zeminin eğimini de içerir (dalgalar ~20°).
            Assert.Less(maxRoll, 25f, "yana fazla yatmamalı (takla)");
            Assert.Less(maxPitch, 45f, "öne/arkaya devrilmemeli");
            Assert.Less(airborne, 0.8f, "tümseklerden fırlamamalı");
        }
    }
}
#endif

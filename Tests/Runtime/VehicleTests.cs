using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Atthree.Vehicles.Tests
{
    /// <summary>
    /// Aracın gerçekten sürüldüğünü fizik simülasyonu ile ölçen PlayMode testleri.
    /// Her test kendi pistini ve arabasını kodla oluşturur; sahne gerekmez.
    /// </summary>
    public class VehicleTests
    {
        GameObject track;
        VehicleController car;

        [SetUp]
        public void SetUp()
        {
            track = TestTrack.Create(Vector3.zero);
            car = VehiclePrimitives.CreateTestCar(new Vector3(0f, 0.2f, 5f), Quaternion.identity,
                new VehicleSetup.Options { addPlayerInput = false, addHud = false });
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(track);
            Object.Destroy(car.gameObject);
        }

        static IEnumerator Simulate(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
        }

        float Upright => Vector3.Dot(car.transform.up, Vector3.up);

        [UnityTest]
        public IEnumerator SettlesOnSuspension()
        {
            yield return Simulate(2.5f);

            Assert.AreEqual(4, car.GroundedWheelCount, "4 teker yere basmalı");
            Assert.Less(Mathf.Abs(car.Body.linearVelocity.y), 0.05f, "yerinde salınmamalı");
            foreach (var w in car.Wheels)
                Assert.That(w.Compression01, Is.InRange(0.1f, 0.6f), $"{w.name} süspansiyon makul sıkışmalı");
            float wheelCenterY = car.Wheels[0].transform.position.y - car.Wheels[0].Distance;
            Debug.Log($"[Vehicle] rest: compression={car.Wheels[0].Compression01:F2} wheelCenterY={wheelCenterY:F3} radius={car.Wheels[0].radius:F2}");
            Assert.AreEqual(car.Wheels[0].radius, wheelCenterY, 0.03f, "teker yarıçapı kadar yukarıda durmalı");
        }

        [UnityTest]
        public IEnumerator AcceleratesStraight_ThenBrakes()
        {
            // Pistin arkasındaki düz bölgede (z < 30 değil, -Z yönünde) gaz.
            car.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            car.Body.rotation = car.transform.rotation;
            yield return Simulate(1f);
            Vector3 start = car.transform.position;

            car.commands.throttle = 1f;
            yield return Simulate(4f);
            float speed = car.Body.linearVelocity.magnitude;
            float lateral = Mathf.Abs(car.transform.position.x - start.x);
            Debug.Log($"[Vehicle] accel: speed={speed:F1} m/s ({speed * 3.6f:F0} km/h) lateralDrift={lateral:F2}");
            Assert.Greater(speed, 14f, "4 sn'de 50 km/s'i geçmeli");
            Assert.Less(lateral, 0.5f, "düz gitmeli");

            car.commands.throttle = -1f; // ileri giderken geri = fren
            Vector3 brakeStart = car.transform.position;
            float stopDistance = -1f;
            for (float t = 0; t < 4f && stopDistance < 0f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                if (car.ForwardSpeed < 0.3f) stopDistance = Vector3.Distance(brakeStart, car.transform.position);
            }
            Debug.Log($"[Vehicle] brake: from {speed:F1} m/s stopped in {stopDistance:F1} m, forwardSpeed now {car.ForwardSpeed:F2}");
            Assert.Greater(stopDistance, 0f, "4 sn içinde durmalı");
            // 15 m/s'den ~1 g ile fren ≈ 11.5 m. Fizik sınırından kısa ya da çok uzun olmamalı.
            Assert.That(stopDistance, Is.InRange(8f, 25f), "fren mesafesi gerçekçi olmalı");
            Assert.Greater(Upright, 0.95f);
        }

        [UnityTest]
        public IEnumerator Reverses()
        {
            yield return Simulate(1f);
            float z0 = car.transform.position.z;
            car.commands.throttle = -1f;
            yield return Simulate(2f);
            Debug.Log($"[Vehicle] reverse: dz={car.transform.position.z - z0:F2} speed={car.ForwardSpeed:F2}");
            Assert.Less(car.ForwardSpeed, -2f, "geri gitmeli");
        }

        [UnityTest]
        public IEnumerator TurnsWithoutRollingOver()
        {
            car.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            car.Body.rotation = car.transform.rotation;
            yield return Simulate(1f);

            car.commands.throttle = 1f;
            yield return Simulate(2.5f);
            car.commands.throttle = 0.5f;
            car.commands.steer = 1f; // sağa
            float yawStart = car.transform.eulerAngles.y;
            float minUpright = 1f;
            for (float t = 0; t < 3f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                minUpright = Mathf.Min(minUpright, Upright);
            }
            float turned = Mathf.DeltaAngle(yawStart, car.transform.eulerAngles.y);
            float roll = Mathf.Acos(Mathf.Clamp01(minUpright)) * Mathf.Rad2Deg;
            Debug.Log($"[Vehicle] turn: turned={turned:F0}° maxTilt={roll:F1}° speed={car.Body.linearVelocity.magnitude:F1}");
            Assert.Greater(turned, 45f, "sağa dönmeli (yaw artmalı)");
            Assert.Less(roll, 15f, "virajda fazla yatmamalı");
        }

        [UnityTest]
        public IEnumerator WheelsSpinWithSpeed_AndSteerVisually()
        {
            car.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            car.Body.rotation = car.transform.rotation;
            yield return Simulate(1f);
            car.commands.throttle = 0.6f;
            yield return Simulate(2f);

            var wheel = car.Wheels.First(w => w.axle == Axle.Rear);
            float expected = car.ForwardSpeed / wheel.radius;
            Debug.Log($"[Vehicle] spin: angularVelocity={wheel.AngularVelocity:F2} rad/s expected≈{expected:F2}");
            Assert.AreEqual(expected, wheel.AngularVelocity, Mathf.Abs(expected) * 0.15f + 0.5f, "teker hızla orantılı dönmeli");

            car.commands.steer = -1f;
            yield return Simulate(0.6f);
            var front = car.Wheels.Where(w => w.axle == Axle.Front).ToArray();
            Debug.Log($"[Vehicle] steer: {string.Join(", ", front.Select(w => $"{w.side}={w.SteerAngle:F1}"))}");
            Assert.Less(front[0].SteerAngle, -5f, "ön teker sola dönmeli");
            // Ackermann: sola dönüşte sol (iç) teker daha çok döner.
            var left = front.First(w => w.side == Side.Left);
            var right = front.First(w => w.side == Side.Right);
            Assert.Greater(Mathf.Abs(left.SteerAngle), Mathf.Abs(right.SteerAngle), "iç teker daha çok dönmeli");
        }

        [UnityTest]
        public IEnumerator DrivesOverBumpsAndPotholes()
        {
            yield return Simulate(1f);
            float minUpright = 1f, maxComp = 0f, minComp = 1f, airborneTime = 0f;
            float bodyMinY = float.MaxValue, bodyMaxY = float.MinValue;

            // Tümsek, hız kesici ve çukur bölgelerinin hepsinden geç (z 30 -> 150).
            car.commands.throttle = 0.7f;
            while (car.transform.position.z < 155f)
            {
                // Pistin ortasında kal: basit direksiyon düzeltmesi.
                float heading = Mathf.DeltaAngle(0f, car.transform.eulerAngles.y);
                car.commands.steer = Mathf.Clamp(-car.transform.position.x * 0.15f - heading * 0.05f, -1f, 1f);
                car.commands.throttle = car.Body.linearVelocity.magnitude > 12f ? 0f : 0.7f;

                yield return new WaitForFixedUpdate();
                float z = car.transform.position.z;
                if (z > 30f)
                {
                    minUpright = Mathf.Min(minUpright, Upright);
                    foreach (var w in car.Wheels) { maxComp = Mathf.Max(maxComp, w.Compression01); minComp = Mathf.Min(minComp, w.Compression01); }
                    if (car.GroundedWheelCount == 0) { airborneTime += Time.fixedDeltaTime; Debug.Log($"[Vehicle]   airborne at z={z:F1} speed={car.Body.linearVelocity.magnitude:F1}"); }
                    bodyMinY = Mathf.Min(bodyMinY, car.transform.position.y);
                    bodyMaxY = Mathf.Max(bodyMaxY, car.transform.position.y);
                }
                if (Time.time > 60f) Assert.Fail("pisti 60 sn'de geçemedi, z=" + z);
            }
            float tilt = Mathf.Acos(Mathf.Clamp01(minUpright)) * Mathf.Rad2Deg;
            Debug.Log($"[Vehicle] bumps: compression range {minComp:F2}..{maxComp:F2} maxTilt={tilt:F1}° airborne={airborneTime:F2}s bodyY {bodyMinY:F2}..{bodyMaxY:F2} x={car.transform.position.x:F1}");
            Assert.Greater(maxComp - minComp, 0.3f, "süspansiyon çalışmalı (sıkışıp açılmalı)");
            Assert.Less(tilt, 25f, "takla atmamalı");
            Assert.Less(airborneTime, 0.5f, "tümseklerde uçmamalı");
        }

        [UnityTest]
        public IEnumerator HandbrakeMakesRearSlide()
        {
            car.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            car.Body.rotation = car.transform.rotation;
            yield return Simulate(1f);
            car.commands.throttle = 1f;
            yield return Simulate(3f);

            car.commands.throttle = 0f;
            car.commands.steer = 0.6f;
            car.commands.handbrake = true;
            float maxRearSlip = 0f;
            for (float t = 0; t < 1.2f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                foreach (var w in car.Wheels.Where(w => w.axle == Axle.Rear)) maxRearSlip = Mathf.Max(maxRearSlip, Mathf.Abs(w.LateralSpeed));
            }
            Debug.Log($"[Vehicle] handbrake: max rear lateral slip={maxRearSlip:F2} m/s upright={Upright:F2}");
            Assert.Greater(maxRearSlip, 1.5f, "arka kaymalı (drift)");
            Assert.Greater(Upright, 0.8f, "devrilmemeli");
        }
    }
}

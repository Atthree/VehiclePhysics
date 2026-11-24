using System.Collections.Generic;
using UnityEngine;

namespace Atthree.Vehicles
{
    /// <summary>
    /// Aracın beyni. Aracın kökünde (Rigidbody ile aynı yerde) durur.
    ///
    /// Her fizik adımında sırayla:
    ///  1) Girdiyi (VehicleCommands) gaz / fren / geri vitese çevirir.
    ///  2) Direksiyon açılarını hesaplar (Ackermann: iç teker dış tekerden fazla döner).
    ///  3) Tüm tekerleklerin süspansiyonunu çalıştırır (önce hepsi, çünkü yükler lastik için lazım).
    ///  4) Viraj demiri (anti-roll bar): sol-sağ sıkışma farkını azaltarak yatmayı önler.
    ///  5) Tüm tekerleklerin lastik kuvvetlerini uygular.
    ///  6) Hıza bağlı yere basma kuvveti (downforce).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(-50)]
    public class VehicleController : MonoBehaviour
    {
        [Header("Motor")]
        [Tooltip("Toplam itiş kuvveti (N). Kabaca: kütle * istenen ivme.")]
        public float motorForce = 7000f;
        [Tooltip("İleri azami hız (m/sn). 30 m/sn = 108 km/s.")]
        public float maxSpeed = 30f;
        [Tooltip("Geri azami hız (m/sn).")]
        public float maxReverseSpeed = 8f;

        [Header("Fren")]
        [Tooltip("Tekerlek başına fren kuvveti (N).")]
        public float brakeForce = 3000f;
        [Tooltip("El freninin kilitlediği tekerleğe ek fren kuvveti (N).")]
        public float handbrakeForce = 4000f;
        [Tooltip("El freni çekiliyken arka tekerleklerin tutuş oranı (drift için düşük).")]
        [Range(0f, 1f)] public float handbrakeGrip = 0.35f;

        [Header("Direksiyon")]
        public float maxSteerAngle = 32f;
        [Tooltip("Direksiyonun dönme hızı (derece/sn).")]
        public float steerSpeed = 150f;
        [Tooltip("Azami hızdayken direksiyon açısının kalan oranı (yüksek hızda savrulmayı önler).")]
        [Range(0.1f, 1f)] public float highSpeedSteerFactor = 0.35f;

        [Header("Denge")]
        [Tooltip("Ağırlık merkezinin otomatik hesaplanan yerine göre kaydırması (yerel). Aşağı = devrilmez.")]
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.35f, 0f);
        [Tooltip("Viraj demiri sertliği (N/m). Virajda yana yatmayı azaltır.")]
        public float antiRollStiffness = 9000f;
        [Tooltip("Lastik kuvvetinin uygulandığı yükseklik: 0 = temas noktası, 1 = ağırlık merkezi.")]
        [Range(0f, 1f)] public float tireForceHeight = 0.5f;
        [Tooltip("Hızın karesiyle artan, aracı yere bastıran kuvvet katsayısı.")]
        public float downforce = 2f;
        public LayerMask groundMask = ~0;

        [Header("Komutlar (girdi scripti doldurur)")]
        public VehicleCommands commands = new VehicleCommands();

        // ---- Dışarıya açık durum ----
        public Rigidbody Body { get; private set; }
        public IReadOnlyList<VehicleWheel> Wheels => wheels;
        /// <summary>Aracın ileri yöndeki hızı (m/sn), geri giderken negatif.</summary>
        public float ForwardSpeed { get; private set; }
        public float SpeedKmh => Body.linearVelocity.magnitude * 3.6f;
        public int GroundedWheelCount { get; private set; }
        public float Throttle { get; private set; }
        public float Brake { get; private set; }

        readonly List<VehicleWheel> wheels = new List<VehicleWheel>();
        readonly HashSet<Collider> ownColliders = new HashSet<Collider>();
        float steerInput;
        float wheelBase, trackWidth;
        int drivenCount;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.ResetCenterOfMass();
            Body.centerOfMass += centerOfMassOffset;

            foreach (var c in GetComponentsInChildren<Collider>(true)) ownColliders.Add(c);

            GetComponentsInChildren(wheels);
            foreach (var w in wheels)
            {
                w.Initialize(this);
                if (w.drive) drivenCount++;
            }
            MeasureAxles();
        }

        /// <summary>Dingil mesafesi ve iz genişliği (Ackermann için), tekerlek konumlarından.</summary>
        void MeasureAxles()
        {
            float frontZ = 0, rearZ = 0, frontWidth = 0;
            int f = 0, r = 0;
            foreach (var w in wheels)
            {
                Vector3 local = transform.InverseTransformPoint(w.transform.position);
                if (w.axle == Axle.Front) { frontZ += local.z; f++; frontWidth = Mathf.Max(frontWidth, Mathf.Abs(local.x) * 2f); }
                else { rearZ += local.z; r++; }
            }
            wheelBase = (f > 0 && r > 0) ? Mathf.Abs(frontZ / f - rearZ / r) : 2.5f;
            trackWidth = frontWidth > 0f ? frontWidth : 1.6f;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            ForwardSpeed = Vector3.Dot(Body.linearVelocity, transform.forward);

            ReadPedals(out float throttle, out float brake);
            Throttle = throttle;
            Brake = brake;
            UpdateSteering(dt);

            // 3) Süspansiyon
            GroundedWheelCount = 0;
            foreach (var w in wheels)
            {
                w.UpdateSuspension(dt);
                if (w.IsGrounded) GroundedWheelCount++;
            }

            // 4) Viraj demiri
            ApplyAntiRoll(Axle.Front);
            ApplyAntiRoll(Axle.Rear);

            // 5) Lastikler
            float speedLimit = throttle >= 0f ? maxSpeed : maxReverseSpeed;
            float speedRatio = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / Mathf.Max(0.1f, speedLimit));
            // Azami hıza yaklaştıkça itiş azalır (basit motor eğrisi).
            float drive = throttle * motorForce * (1f - speedRatio * speedRatio);
            float drivePerWheel = drivenCount > 0 ? drive / drivenCount : 0f;

            foreach (var w in wheels)
                w.ApplyTireForces(w.drive ? drivePerWheel : 0f, brake * brakeForce, commands.handbrake, dt);

            // 6) Downforce
            if (GroundedWheelCount > 0)
                Body.AddForce(-transform.up * downforce * Body.linearVelocity.sqrMagnitude);
        }

        // ------------------------------------------------------------------ 1) Pedallar
        /// <summary>
        /// Tek bir "ileri/geri" girdisini gaz ve frene çevirir:
        /// ileri giderken geri tuşu = fren, durunca = geri vites. (Çoğu araba oyunu böyle.)
        /// </summary>
        void ReadPedals(out float throttle, out float brake)
        {
            float input = Mathf.Clamp(commands.throttle, -1f, 1f);
            throttle = 0f;
            brake = 0f;

            if (input > 0.05f)
            {
                if (ForwardSpeed < -1f) brake = input; else throttle = input;
            }
            else if (input < -0.05f)
            {
                if (ForwardSpeed > 1f) brake = -input; else throttle = input;
            }
            else if (Mathf.Abs(ForwardSpeed) < 0.5f)
            {
                brake = 1f; // dururken kendiliğinden kaymasın (yokuşta park freni)
            }
        }

        // ------------------------------------------------------------------ 2) Direksiyon
        void UpdateSteering(float dt)
        {
            steerInput = Mathf.MoveTowards(steerInput, Mathf.Clamp(commands.steer, -1f, 1f), steerSpeed / maxSteerAngle * dt);

            float speedRatio = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / maxSpeed);
            float angle = steerInput * maxSteerAngle * Mathf.Lerp(1f, highSpeedSteerFactor, speedRatio);

            // Ackermann: viraj içindeki teker daha dar bir çember çizer, daha çok döner.
            float innerAngle = angle, outerAngle = angle;
            if (Mathf.Abs(angle) > 0.01f)
            {
                float turnRadius = wheelBase / Mathf.Tan(Mathf.Abs(angle) * Mathf.Deg2Rad);
                innerAngle = Mathf.Atan(wheelBase / (turnRadius - trackWidth * 0.5f)) * Mathf.Rad2Deg * Mathf.Sign(angle);
                outerAngle = Mathf.Atan(wheelBase / (turnRadius + trackWidth * 0.5f)) * Mathf.Rad2Deg * Mathf.Sign(angle);
            }

            foreach (var w in wheels)
            {
                if (!w.steer) { w.SteerAngle = 0f; continue; }
                // Sağa dönerken (angle > 0) sağ teker içtedir.
                bool isInner = (angle > 0f) == (w.side == Side.Right);
                w.SteerAngle = isInner ? innerAngle : outerAngle;
            }
        }

        // ------------------------------------------------------------------ 4) Viraj demiri
        void ApplyAntiRoll(Axle axle)
        {
            VehicleWheel left = null, right = null;
            foreach (var w in wheels)
            {
                if (w.axle != axle) continue;
                if (w.side == Side.Left) left = w; else right = w;
            }
            if (left == null || right == null) return;

            // Virajda araç dışa yatar: dış teker çok sıkışır, iç teker açılır.
            // Viraj demiri farkla orantılı olarak çok sıkışan tarafı YUKARI, az sıkışanı AŞAĞI iter.
            float travelLeft = left.IsGrounded ? left.Compression01 * left.suspensionLength : 0f;
            float travelRight = right.IsGrounded ? right.Compression01 * right.suspensionLength : 0f;
            float force = (travelLeft - travelRight) * antiRollStiffness;

            Vector3 up = transform.up;
            if (left.IsGrounded) Body.AddForceAtPosition(up * force, left.transform.position);
            if (right.IsGrounded) Body.AddForceAtPosition(up * -force, right.transform.position);
        }

        // ------------------------------------------------------------------ yardımcılar
        public bool IsOwnCollider(Collider c) => ownColliders.Contains(c);

        /// <summary>Takla atan aracı düzeltir: biraz yukarı kaldırır, sadece yönünü korur.</summary>
        public void ResetUpright()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            Body.position += Vector3.up * 1.5f;
            Body.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }
    }
}

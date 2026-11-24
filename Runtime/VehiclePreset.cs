using UnityEngine;

namespace Atthree.Vehicles
{
    public enum DriveType { RearWheel, FrontWheel, AllWheel }

    /// <summary>
    /// Bir araç tipinin tüm ayarları tek yerde: kütle, motor, süspansiyon, lastik...
    ///
    /// Hazır profiller: <see cref="Default"/>, <see cref="Sports"/>, <see cref="Truck"/>, <see cref="OffRoad"/>.
    /// Kendi profilini oluşturmak için: Project penceresi > sağ tık > Create > Vehicle Physics > Vehicle Preset.
    /// </summary>
    [CreateAssetMenu(menuName = "Vehicle Physics/Vehicle Preset", fileName = "VehiclePreset")]
    public class VehiclePreset : ScriptableObject
    {
        [Header("Gövde")]
        public float mass = 1200f;
        [Tooltip("Ağırlık merkezinin kaydırması (yerel). Aşağı = devrilmez.")]
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.35f, 0f);

        [Header("Motor")]
        public DriveType drive = DriveType.RearWheel;
        public float motorForce = 7000f;
        public float maxSpeed = 30f;
        public float maxReverseSpeed = 8f;

        [Header("Fren")]
        [Tooltip("Tekerlek başına.")]
        public float brakeForce = 3000f;
        public float handbrakeForce = 4000f;
        [Range(0f, 1f)] public float handbrakeGrip = 0.35f;

        [Header("Direksiyon")]
        public float maxSteerAngle = 32f;
        public float steerSpeed = 150f;
        [Range(0.1f, 1f)] public float highSpeedSteerFactor = 0.35f;

        [Header("Denge")]
        public float antiRollStiffness = 9000f;
        [Range(0f, 1f)] public float tireForceHeight = 0.5f;
        public float downforce = 2f;

        [Header("Süspansiyon")]
        public float suspensionLength = 0.3f;
        public float springStiffness = 35000f;
        public float bumpDamper = 2500f;
        public float reboundDamper = 4000f;

        [Header("Lastik")]
        public float friction = 1.2f;
        public float lateralResponse = 20f;

        /// <summary>Profili var olan bir araca uygular (controller + tüm tekerlekler).</summary>
        public void ApplyTo(VehicleController vehicle)
        {
            var body = vehicle.GetComponent<Rigidbody>();
            if (body != null) body.mass = mass;

            vehicle.centerOfMassOffset = centerOfMassOffset;
            vehicle.motorForce = motorForce;
            vehicle.maxSpeed = maxSpeed;
            vehicle.maxReverseSpeed = maxReverseSpeed;
            vehicle.brakeForce = brakeForce;
            vehicle.handbrakeForce = handbrakeForce;
            vehicle.handbrakeGrip = handbrakeGrip;
            vehicle.maxSteerAngle = maxSteerAngle;
            vehicle.steerSpeed = steerSpeed;
            vehicle.highSpeedSteerFactor = highSpeedSteerFactor;
            vehicle.antiRollStiffness = antiRollStiffness;
            vehicle.tireForceHeight = tireForceHeight;
            vehicle.downforce = downforce;

            foreach (var wheel in vehicle.GetComponentsInChildren<VehicleWheel>(true))
                ApplyTo(wheel);
        }

        public void ApplyTo(VehicleWheel wheel)
        {
            wheel.suspensionLength = suspensionLength;
            wheel.springStiffness = springStiffness;
            wheel.bumpDamper = bumpDamper;
            wheel.reboundDamper = reboundDamper;
            wheel.friction = friction;
            wheel.lateralResponse = lateralResponse;
            wheel.drive = drive == DriveType.AllWheel
                          || (drive == DriveType.RearWheel && wheel.axle == Axle.Rear)
                          || (drive == DriveType.FrontWheel && wheel.axle == Axle.Front);
        }

        /// <summary>Dururken her tekerleğin yayı ne kadar sıkışır (m).</summary>
        public float StaticCompression =>
            Mathf.Clamp(mass * Mathf.Abs(Physics.gravity.y) / 4f / springStiffness, 0f, suspensionLength * 0.8f);

        // ------------------------------------------------------------------ hazır profiller
        // Amortisörler kritik sönümün ~%40 (sıkışma) ve ~%60 (açılma) civarında seçildi:
        // c = oran * 2 * sqrt(yay * tekerlek başına kütle)

        /// <summary>Dengeli, her işe uygun binek araç (test arabası bunu kullanır).</summary>
        public static VehiclePreset Default => Create("Default", p => { });

        /// <summary>Hafif, güçlü, sert süspansiyon, yüksek tutuş.</summary>
        public static VehiclePreset Sports => Create("Sports", p =>
        {
            p.mass = 1100f; p.centerOfMassOffset = new Vector3(0f, -0.3f, 0f);
            p.drive = DriveType.RearWheel; p.motorForce = 9500f; p.maxSpeed = 50f;
            p.brakeForce = 3600f; p.handbrakeGrip = 0.3f;
            p.maxSteerAngle = 30f; p.steerSpeed = 180f; p.highSpeedSteerFactor = 0.25f;
            p.antiRollStiffness = 15000f; p.downforce = 4f;
            p.suspensionLength = 0.25f; p.springStiffness = 45000f; p.bumpDamper = 2600f; p.reboundDamper = 5000f;
            p.friction = 1.4f; p.lateralResponse = 25f;
        });

        /// <summary>Ağır, yavaş, uzun ve yumuşak süspansiyon; ağırlık merkezi yüksek.</summary>
        public static VehiclePreset Truck => Create("Truck", p =>
        {
            p.mass = 4000f; p.centerOfMassOffset = new Vector3(0f, -0.6f, 0f);
            p.drive = DriveType.RearWheel; p.motorForce = 16000f; p.maxSpeed = 25f; p.maxReverseSpeed = 6f;
            p.brakeForce = 9000f; p.handbrakeForce = 12000f; p.handbrakeGrip = 0.5f;
            p.maxSteerAngle = 30f; p.steerSpeed = 90f; p.highSpeedSteerFactor = 0.4f;
            p.antiRollStiffness = 40000f; p.tireForceHeight = 0.6f; p.downforce = 0f;
            p.suspensionLength = 0.4f; p.springStiffness = 60000f; p.bumpDamper = 5400f; p.reboundDamper = 8500f;
            p.friction = 1.0f; p.lateralResponse = 15f;
        });

        /// <summary>Hafif, 4x4, çok uzun ve yumuşak süspansiyon: çukurları yutar.</summary>
        public static VehiclePreset OffRoad => Create("OffRoad", p =>
        {
            p.mass = 800f; p.centerOfMassOffset = new Vector3(0f, -0.3f, 0f);
            p.drive = DriveType.AllWheel; p.motorForce = 7000f; p.maxSpeed = 28f;
            p.brakeForce = 2500f; p.handbrakeGrip = 0.35f;
            p.maxSteerAngle = 35f; p.steerSpeed = 160f; p.highSpeedSteerFactor = 0.4f;
            p.antiRollStiffness = 8000f; p.downforce = 1f;
            p.suspensionLength = 0.5f; p.springStiffness = 15000f; p.bumpDamper = 1050f; p.reboundDamper = 2800f;
            p.friction = 1.2f; p.lateralResponse = 18f;
        });

        public static readonly string[] BuiltInNames = { "Default", "Sports", "Truck", "OffRoad" };

        public static VehiclePreset BuiltIn(int index) => index switch
        {
            1 => Sports,
            2 => Truck,
            3 => OffRoad,
            _ => Default,
        };

        static VehiclePreset Create(string name, System.Action<VehiclePreset> configure)
        {
            var preset = CreateInstance<VehiclePreset>();
            preset.name = name;
            configure(preset);
            return preset;
        }
    }
}

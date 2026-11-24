using UnityEngine;

namespace Atthree.Vehicles
{
    public enum Axle { Front, Rear }
    public enum Side { Left, Right }

    /// <summary>
    /// Tek bir tekerlek: süspansiyon (yay + amortisör) ve lastik kuvvetleri.
    ///
    /// Bu GameObject süspansiyonun ÜST NOKTASIDIR (gövdeye bağlandığı yer). Her fizik adımında
    /// buradan aşağı, tekerlek yarıçapında bir küre atılır (SphereCast):
    ///
    ///        ● anchor (bu transform)
    ///        |
    ///        |  distance  (0 = tam sıkışmış, suspensionLength = tam açılmış)
    ///        |
    ///       (●) tekerlek merkezi
    ///      ‾‾‾‾‾ zemin
    ///
    /// Küre yere değiyorsa:
    ///  1) Yay kuvveti   = sertlik * sıkışma  +  sönüm * sıkışma hızı   (gövdeyi yukarı iter)
    ///  2) Yanal kuvvet  = tekerleğin yana kayma hızını sıfırlamaya çalışır (viraj tutuşu)
    ///  3) Boyuna kuvvet = motor itişi / fren
    ///  2+3 toplamı "sürtünme çemberi" ile sınırlanır: en fazla friction * yük.
    ///  Yük azalınca (tümsekte havalanınca) tutuş da azalır. Gerçek lastik gibi.
    ///
    /// Hesaplar <see cref="VehicleController"/> tarafından sırayla çağrılır.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleWheel : MonoBehaviour
    {
        [Header("Kurulum")]
        public Axle axle;
        public Side side;
        [Tooltip("Dönen ve direksiyonla çevrilen görsel tekerlek mesh'i.")]
        public Transform visual;
        public float radius = 0.35f;
        public bool steer;
        public bool drive;
        [Tooltip("El freni bu tekerleği kilitler ve yanal tutuşunu azaltır (drift).")]
        public bool handbrake;

        [Header("Süspansiyon")]
        [Tooltip("Tekerlek merkezinin anchor'dan en fazla ne kadar aşağı inebileceği (m).")]
        public float suspensionLength = 0.3f;
        [Tooltip("Yay sertliği (N/m). Yüksek = sert, az yaylanan.")]
        public float springStiffness = 35000f;
        [Tooltip("Sıkışırken (tümseğe girerken) amortisör (N·s/m). Düşük = darbeyi yumuşak alır.")]
        public float bumpDamper = 2500f;
        [Tooltip("Açılırken (tümsekten inerken) amortisör (N·s/m). Yüksek = sekmez, çabuk oturur.")]
        public float reboundDamper = 4000f;
        [Tooltip("Bu sıkışma hızının (m/sn) üstünde sönüm eğimi azalır: ani darbeler aracı fırlatmaz.")]
        public float damperKneeSpeed = 0.6f;
        [Range(0f, 1f)] public float highSpeedDamperRatio = 0.3f;
        [Tooltip("Süspansiyon bu orandan fazla sıkışınca devreye giren sert tampon (dibe vurmayı önler).")]
        [Range(0.5f, 1f)] public float bumpStopStart = 0.85f;

        [Header("Lastik")]
        [Tooltip("Sürtünme katsayısı: lastiğin taşıyabileceği en büyük kuvvet = friction * yük.")]
        public float friction = 1.2f;
        [Tooltip("Yana kaymayı ne hızlı düzeltir (1/sn). Yüksek = ray gibi, düşük = kaygan.")]
        public float lateralResponse = 20f;
        [Tooltip("Yuvarlanma direnci (N per m/s). Gaz bırakınca yavaşlama.")]
        public float rollingResistance = 30f;

        // ---- Durum (her fizik adımında güncellenir) ----
        public bool IsGrounded { get; private set; }
        /// <summary>Anchor'dan tekerlek merkezine mesafe.</summary>
        public float Distance { get; private set; }
        /// <summary>0 = tam açık, 1 = tam sıkışmış.</summary>
        public float Compression01 => 1f - Distance / Mathf.Max(0.0001f, suspensionLength);
        /// <summary>Süspansiyonun taşıdığı yük (N).</summary>
        public float Load { get; private set; }
        public RaycastHit Hit => hit;
        /// <summary>Direksiyon açısı (derece), VehicleController ayarlar.</summary>
        public float SteerAngle { get; set; }
        /// <summary>Tekerleğin dönüş hızı (rad/sn).</summary>
        public float AngularVelocity { get; private set; }
        /// <summary>Lastik sürtünme sınırını aştı mı (kayıyor/patinaj).</summary>
        public bool IsSlipping { get; private set; }
        public float LateralSpeed { get; private set; }
        public float ForwardSpeed { get; private set; }

        VehicleController vehicle;
        Rigidbody body;
        RaycastHit hit;
        float lastCompression;
        float spinAngle;
        Quaternion visualRestRotation; // görselin, araca göre başlangıç rotasyonu
        readonly RaycastHit[] hits = new RaycastHit[8];

        public void Initialize(VehicleController owner)
        {
            vehicle = owner;
            body = owner.Body;
            Distance = suspensionLength;
            if (visual != null)
                visualRestRotation = Quaternion.Inverse(owner.transform.rotation) * visual.rotation;
        }

        // ------------------------------------------------------------------ 1) SÜSPANSİYON
        /// <summary>Zemini bulur ve yay kuvvetini uygular.</summary>
        public void UpdateSuspension(float dt)
        {
            Vector3 up = vehicle.transform.up;
            Vector3 origin = transform.position;

            IsGrounded = CastToGround(origin, -up, out hit);
            Distance = IsGrounded ? Mathf.Clamp(hit.distance, 0f, suspensionLength) : suspensionLength;

            float compression = suspensionLength - Distance; // metre
            float compressionVelocity = (compression - lastCompression) / dt;
            lastCompression = compression;

            if (!IsGrounded)
            {
                Load = 0f;
                return;
            }

            float force = springStiffness * compression + DamperForce(compressionVelocity);

            // Tampon: son %15'lik kısımda 5 kat sert yay.
            float bumpStop = suspensionLength * bumpStopStart;
            if (compression > bumpStop) force += springStiffness * 5f * (compression - bumpStop);

            Load = Mathf.Max(0f, force); // süspansiyon çekemez, sadece iter
            body.AddForceAtPosition(up * Load, origin);
        }

        /// <summary>
        /// Amortisör kuvveti. velocity > 0 = sıkışıyor (tümsek), &lt; 0 = açılıyor.
        /// Diz hızına kadar doğrusal; üstünde eğim highSpeedDamperRatio kadar (digressive damper).
        /// </summary>
        float DamperForce(float velocity)
        {
            float c = velocity > 0f ? bumpDamper : reboundDamper;
            float speed = Mathf.Abs(velocity);
            float magnitude = speed <= damperKneeSpeed
                ? c * speed
                : c * damperKneeSpeed + c * highSpeedDamperRatio * (speed - damperKneeSpeed);
            return Mathf.Sign(velocity) * magnitude;
        }

        bool CastToGround(Vector3 origin, Vector3 direction, out RaycastHit result)
        {
            result = default;
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, suspensionLength,
                vehicle.groundMask, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (vehicle.IsOwnCollider(hits[i].collider)) continue;
                var h = hits[i];
                // distance 0 = küre başlangıçta zemine gömülü; temas noktası yoksa aşağı varsay.
                if (h.distance <= 0f) { h.point = origin + direction * radius; h.normal = -direction; }
                if (h.distance < best) { best = h.distance; result = h; }
            }
            return best < float.MaxValue;
        }

        // ------------------------------------------------------------------ 2+3) LASTİK
        /// <param name="driveForce">Motorun bu tekerleğe verdiği itiş (N), geri için negatif.</param>
        /// <param name="brakeForce">Fren kuvveti (N), her zaman pozitif.</param>
        /// <param name="handbrakeOn">El freni çekili mi.</param>
        public void ApplyTireForces(float driveForce, float brakeForce, bool handbrakeOn, float dt)
        {
            IsSlipping = false;
            if (!IsGrounded || Load <= 0f)
            {
                LateralSpeed = ForwardSpeed = 0f;
                UpdateSpinInAir(driveForce, dt);
                return;
            }

            // Tekerleğin baktığı yön (direksiyon dahil), zemin düzlemine yatırılmış.
            Vector3 up = vehicle.transform.up;
            Vector3 steeredForward = Quaternion.AngleAxis(SteerAngle, up) * vehicle.transform.forward;
            Vector3 forward = Vector3.ProjectOnPlane(steeredForward, hit.normal).normalized;
            Vector3 right = Vector3.Cross(hit.normal, forward);

            Vector3 velocity = body.GetPointVelocity(hit.point);
            ForwardSpeed = Vector3.Dot(velocity, forward);
            LateralSpeed = Vector3.Dot(velocity, right);

            // Bu tekerleğin "taşıdığı" kütle (yaklaşık): yük / g.
            float massShare = Load / Mathf.Abs(Physics.gravity.y);
            bool locked = handbrakeOn && handbrake;

            // --- Yanal: kaymayı sıfırlamaya çalış.
            float lateralGrip = locked ? vehicle.handbrakeGrip : 1f;
            float lateral = -LateralSpeed * massShare * lateralResponse * lateralGrip;

            // --- Boyuna: motor + fren + yuvarlanma direnci.
            float longitudinal = driveForce - ForwardSpeed * rollingResistance;
            float totalBrake = brakeForce + (locked ? vehicle.handbrakeForce : 0f);
            if (totalBrake > 0f)
            {
                // Fren aracı durdurur ama ters yöne itmez: bu adımda hızı sıfırlayacak kuvvetten fazlası yok.
                float stopForce = Mathf.Abs(ForwardSpeed) * massShare / dt;
                longitudinal -= Mathf.Sign(ForwardSpeed) * Mathf.Min(totalBrake, stopForce);
            }

            // --- Sürtünme çemberi: toplam kuvvet friction * yük'ü geçemez.
            Vector3 force = forward * longitudinal + right * lateral;
            float maxForce = friction * Load * (locked ? vehicle.handbrakeGrip : 1f);
            if (force.magnitude > maxForce)
            {
                force = force.normalized * maxForce;
                IsSlipping = true;
            }

            // Kuvveti temas noktası ile ağırlık merkezi arasında bir yüksekliğe uygula
            // (tam temas noktası aracı virajda kolayca devirir).
            Vector3 com = body.worldCenterOfMass;
            Vector3 point = hit.point + up * (Vector3.Dot(com - hit.point, up) * vehicle.tireForceHeight);
            body.AddForceAtPosition(force, point);

            // Görsel dönüş: yerdeyken yere göre yuvarlanır; el freni kilitliyse durur.
            AngularVelocity = locked ? 0f : ForwardSpeed / radius;
        }

        void UpdateSpinInAir(float driveForce, float dt)
        {
            // Havada: gaz verilen tekerlek hızlanır, diğerleri yavaşça durur.
            float target = drive && Mathf.Abs(driveForce) > 1f ? Mathf.Sign(driveForce) * 60f : 0f;
            AngularVelocity = Mathf.MoveTowards(AngularVelocity, target, 40f * dt);
        }

        // ------------------------------------------------------------------ GÖRSEL
        void LateUpdate()
        {
            if (visual == null || vehicle == null) return;

            spinAngle += AngularVelocity * Mathf.Rad2Deg * Time.deltaTime;
            spinAngle %= 360f;

            Transform car = vehicle.transform;
            visual.position = transform.position - car.up * Distance;
            visual.rotation = car.rotation
                              * Quaternion.AngleAxis(SteerAngle, Vector3.up)    // direksiyon
                              * Quaternion.AngleAxis(spinAngle, Vector3.right)  // yuvarlanma
                              * visualRestRotation;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Vector3 up = transform.parent != null ? transform.parent.up : Vector3.up;
            float d = Application.isPlaying ? Distance : suspensionLength;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position - up * suspensionLength);
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position - up * d, radius);
        }
#endif
    }
}

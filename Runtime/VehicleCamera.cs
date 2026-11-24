using UnityEngine;
using UnityEngine.InputSystem;

namespace Atthree.Vehicles
{
    /// <summary>
    /// Takip kamerası: aracın arkasında durur, hızlandıkça biraz uzaklaşır.
    /// Fareyle etrafına bakılabilir; fare bırakılınca kamera yavaşça arkaya döner.
    /// </summary>
    public class VehicleCamera : MonoBehaviour
    {
        public VehicleController target;
        public float distance = 6f;
        public float height = 2.2f;
        [Tooltip("Azami hızda eklenen uzaklık.")]
        public float speedDistance = 2.5f;
        public float lookHeight = 1f;
        [Tooltip("Kameranın aracın arkasına dönme hızı.")]
        public float followSharpness = 5f;
        public float mouseSensitivity = 0.15f;
        [Tooltip("Fareyle bakınma bittikten kaç saniye sonra kamera arkaya döner.")]
        public float recenterDelay = 1.5f;
        public float collisionRadius = 0.25f;
        public LayerMask collisionMask = ~0;

        float yaw, orbitYaw, orbitPitch;
        float lastLookTime = -100f;
        bool initialized;

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            Transform car = target.transform;

            // --- Aracın yön açısı (yalnız yatay), yumuşak takip.
            Vector3 flatForward = Vector3.ProjectOnPlane(car.forward, Vector3.up);
            // Geri giderken kamera ters dönmesin diye aracın burnuna bakmaya devam et.
            float carYaw = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;
            if (!initialized) { yaw = carYaw; initialized = true; }
            yaw = Mathf.LerpAngle(yaw, carYaw, 1f - Mathf.Exp(-followSharpness * dt));

            // --- Fareyle bakınma.
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0.01f && (mouse.rightButton.isPressed || Cursor.lockState == CursorLockMode.Locked))
                {
                    orbitYaw += delta.x * mouseSensitivity;
                    orbitPitch = Mathf.Clamp(orbitPitch - delta.y * mouseSensitivity, -20f, 50f);
                    lastLookTime = Time.time;
                }
            }
            if (Time.time - lastLookTime > recenterDelay)
            {
                float k = 1f - Mathf.Exp(-3f * dt);
                orbitYaw = Mathf.LerpAngle(orbitYaw, 0f, k);
                orbitPitch = Mathf.Lerp(orbitPitch, 0f, k);
            }

            // --- Konum.
            float speed01 = Mathf.Clamp01(target.Body.linearVelocity.magnitude / Mathf.Max(1f, target.maxSpeed));
            float dist = distance + speedDistance * speed01;
            Quaternion rotation = Quaternion.Euler(orbitPitch, yaw + orbitYaw, 0f);
            Vector3 focus = car.position + Vector3.up * lookHeight;
            Vector3 desired = focus + rotation * new Vector3(0f, height - lookHeight, -dist);

            // --- Duvar/zemin arkasına geçme.
            Vector3 toCamera = desired - focus;
            float allowed = toCamera.magnitude;
            foreach (var hit in Physics.SphereCastAll(focus, collisionRadius, toCamera.normalized, allowed, collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (target.IsOwnCollider(hit.collider) || hit.distance <= 0f) continue;
                allowed = Mathf.Min(allowed, hit.distance);
            }
            Vector3 position = focus + toCamera.normalized * allowed;

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position, Vector3.up));
        }
    }
}

using UnityEngine;

namespace Atthree.Vehicles
{
    /// <summary>
    /// Model gerektirmeyen test arabası: küplerden gövde, silindirlerden tekerlek.
    /// Tekerleklerin döndüğü görünsün diye her jantta beyaz bir işaret var.
    /// </summary>
    public static class VehiclePrimitives
    {
        public static VehicleController CreateTestCar(Vector3 position, Quaternion rotation, VehicleSetup.Options options = null)
        {
            options ??= new VehicleSetup.Options();

            var root = new GameObject("Test Car");
            root.transform.SetPositionAndRotation(position, Quaternion.identity);

            var bodyColor = new Color(0.85f, 0.18f, 0.15f);
            Part(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 0.55f, 4.1f), bodyColor);
            Part(root.transform, "Cabin", PrimitiveType.Cube, new Vector3(0f, 1.25f, -0.25f), new Vector3(1.5f, 0.5f, 2.0f), new Color(0.15f, 0.2f, 0.28f));
            Part(root.transform, "Bumper", PrimitiveType.Cube, new Vector3(0f, 0.6f, 2.05f), new Vector3(1.85f, 0.2f, 0.12f), new Color(0.2f, 0.2f, 0.2f));

            const float radius = 0.35f, halfTrack = 0.85f, halfBase = 1.35f;
            var fl = Wheel(root.transform, "Visual FL", new Vector3(-halfTrack, radius, halfBase), radius);
            var fr = Wheel(root.transform, "Visual FR", new Vector3(halfTrack, radius, halfBase), radius);
            var rl = Wheel(root.transform, "Visual RL", new Vector3(-halfTrack, radius, -halfBase), radius);
            var rr = Wheel(root.transform, "Visual RR", new Vector3(halfTrack, radius, -halfBase), radius);

            var vehicle = VehicleSetup.Setup(root, fl, fr, rl, rr, options);
            root.transform.SetPositionAndRotation(position, rotation);
            return vehicle;
        }

        static Transform Wheel(Transform parent, string name, Vector3 localPosition, float radius)
        {
            var tire = Part(parent, name, PrimitiveType.Cylinder, localPosition, new Vector3(radius * 2f, 0.13f, radius * 2f), new Color(0.12f, 0.12f, 0.12f));
            tire.localRotation = Quaternion.Euler(0f, 0f, 90f); // silindiri yatır: aks X ekseni
            // Jant işaretleri (dönüşü görmek için), iki yüzde de. Silindirin yerel uzayında Y = aks yönü.
            Part(tire, "Mark", PrimitiveType.Cube, new Vector3(0f, 1.05f, 0.3f), new Vector3(0.25f, 0.1f, 0.4f), Color.white);
            Part(tire, "Mark", PrimitiveType.Cube, new Vector3(0f, -1.05f, 0.3f), new Vector3(0.25f, 0.1f, 0.4f), Color.white);
            return tire;
        }

        static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            SetColor(go.GetComponent<Renderer>(), color);
            // Görsel parçaların collider'ı olmasın; gövdeye tek bir kutu collider konacak.
            // (DestroyImmediate: Destroy bir sonraki kareye kadar bekler, kurulum onu hâlâ görürdü.)
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        /// <summary>Hem URP (_BaseColor) hem Built-in (_Color) materyallerde çalışan renk ataması.</summary>
        public static void SetColor(Renderer renderer, Color color)
        {
            var material = new Material(renderer.sharedMaterial) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            renderer.sharedMaterial = material;
        }
    }
}

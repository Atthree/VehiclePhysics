using UnityEngine;

namespace Atthree.Vehicles
{
    /// <summary>
    /// Süspansiyonu denemek için prosedürel test pisti. Başlangıç (0,0,0), araç +Z yönünde ilerler:
    ///
    ///   z   0 –  30 : düz zemin (kalkış)
    ///   z  30 –  80 : yumuşak dalgalı tümsekler
    ///   z  80 – 110 : hız kesiciler (keskin sırtlar)
    ///   z 110 – 150 : çukurlar + bozuk yol
    ///   z 150 – 200 : düz, sonunda rampa (zıplama)
    ///
    /// Pistin iki yanı ve geri kalanı düz zemin. Zemin tek bir mesh + MeshCollider.
    /// </summary>
    public static class TestTrack
    {
        public const float Width = 40f;     // x: -20 .. 20
        public const float StartZ = -60f;
        public const float EndZ = 260f;
        const float CellSize = 0.5f;

        public static GameObject Create(Vector3 origin)
        {
            var root = new GameObject("Test Track");
            root.transform.position = origin;

            var ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            ground.transform.SetParent(root.transform, false);
            var mesh = BuildMesh();
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            ground.GetComponent<MeshCollider>().sharedMesh = mesh;
            ApplyDefaultMaterial(ground.GetComponent<MeshRenderer>(), new Color(0.45f, 0.47f, 0.5f));

            // Pistin dışında kalan geniş düz alan: pistin altına girmesin (çukurları doldurur),
            // bu yüzden pistin dört yanına ayrı ayrı döşenir.
            const float far = 200f;
            float half = Width * 0.5f;
            OutsidePlane(root.transform, -half - far, -half, StartZ - far, EndZ + far);
            OutsidePlane(root.transform, half, half + far, StartZ - far, EndZ + far);
            OutsidePlane(root.transform, -half, half, StartZ - far, StartZ);
            OutsidePlane(root.transform, -half, half, EndZ, EndZ + far);

            // Rampa.
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Ramp";
            ramp.transform.SetParent(root.transform, false);
            ramp.transform.localPosition = new Vector3(0f, 0.6f, 190f);
            ramp.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            ramp.transform.localScale = new Vector3(6f, 0.4f, 8f);
            VehiclePrimitives.SetColor(ramp.GetComponent<Renderer>(), new Color(0.85f, 0.65f, 0.2f));

            // Çarpıp itilecek birkaç kutu.
            for (int i = 0; i < 4; i++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "Crate " + i;
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition = new Vector3(-12f + i * 1.2f, 0.5f, 20f);
                box.AddComponent<Rigidbody>().mass = 30f;
                VehiclePrimitives.SetColor(box.GetComponent<Renderer>(), new Color(0.6f, 0.4f, 0.2f));
            }
            return root;
        }

        /// <summary>Pistin bir noktadaki yüksekliği (yerel x, z).</summary>
        public static float Height(float x, float z)
        {
            // Pist kenarlarına doğru yükseklik sıfıra iner, düz zeminle birleşsin.
            float edge = Mathf.Clamp01((Width * 0.5f - Mathf.Abs(x)) / 4f);
            float h = 0f;

            if (z > 30f && z < 80f) // yumuşak dalgalar
            {
                float fade = Mathf.Clamp01((z - 30f) / 5f) * Mathf.Clamp01((80f - z) / 5f);
                h += (Mathf.PerlinNoise(x * 0.12f + 10f, z * 0.12f) - 0.5f) * 0.7f * fade;
                h += Mathf.Sin(z * 0.6f) * 0.12f * fade;
            }
            else if (z >= 80f && z < 110f) // hız kesiciler: her 6 m'de 0.15 m yüksekliğinde sırt
            {
                float local = Mathf.Repeat(z - 80f, 6f);
                if (local < 1f) h += Mathf.Sin(local * Mathf.PI) * 0.15f;
            }
            else if (z >= 110f && z < 150f) // çukurlar + bozuk yol
            {
                float fade = Mathf.Clamp01((z - 110f) / 3f) * Mathf.Clamp01((150f - z) / 3f);
                h += (Mathf.PerlinNoise(x * 0.5f + 50f, z * 0.5f) - 0.5f) * 0.18f * fade;
                // Çukurlar: belirli noktalarda yumuşak kenarlı çöküntüler.
                h -= Pothole(x, z, -3f, 116f, 1.4f, 0.22f);
                h -= Pothole(x, z, 2.5f, 122f, 1.2f, 0.2f);
                h -= Pothole(x, z, -1f, 130f, 1.6f, 0.25f);
                h -= Pothole(x, z, 3.5f, 137f, 1.3f, 0.2f);
                h -= Pothole(x, z, -2.5f, 144f, 1.5f, 0.22f);
            }
            return h * edge;
        }

        static float Pothole(float x, float z, float cx, float cz, float radius, float depth)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) / radius;
            return d >= 1f ? 0f : depth * (0.5f + 0.5f * Mathf.Cos(d * Mathf.PI));
        }

        static Mesh BuildMesh()
        {
            int nx = Mathf.RoundToInt(Width / CellSize) + 1;
            int nz = Mathf.RoundToInt((EndZ - StartZ) / CellSize) + 1;
            var vertices = new Vector3[nx * nz];
            var uv = new Vector2[nx * nz];
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    float x = -Width * 0.5f + ix * CellSize;
                    float z = StartZ + iz * CellSize;
                    vertices[iz * nx + ix] = new Vector3(x, Height(x, z), z);
                    uv[iz * nx + ix] = new Vector2(x, z) * 0.25f;
                }

            var triangles = new int[(nx - 1) * (nz - 1) * 6];
            int t = 0;
            for (int iz = 0; iz < nz - 1; iz++)
                for (int ix = 0; ix < nx - 1; ix++)
                {
                    int i = iz * nx + ix;
                    triangles[t++] = i; triangles[t++] = i + nx; triangles[t++] = i + 1;
                    triangles[t++] = i + 1; triangles[t++] = i + nx; triangles[t++] = i + nx + 1;
                }

            var mesh = new Mesh { name = "TestTrack", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void OutsidePlane(Transform parent, float minX, float maxX, float minZ, float maxZ)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane); // 10x10 birim
            plane.name = "Outside";
            plane.transform.SetParent(parent, false);
            plane.transform.localPosition = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            plane.transform.localScale = new Vector3((maxX - minX) / 10f, 1f, (maxZ - minZ) / 10f);
            VehiclePrimitives.SetColor(plane.GetComponent<Renderer>(), new Color(0.3f, 0.42f, 0.3f));
        }

        static void ApplyDefaultMaterial(MeshRenderer renderer, Color color)
        {
            // Projenin varsayılan materyalini (URP veya Built-in) bir primitive'den ödünç al.
            var temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            renderer.sharedMaterial = temp.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(temp);
            VehiclePrimitives.SetColor(renderer, color);
        }
    }
}

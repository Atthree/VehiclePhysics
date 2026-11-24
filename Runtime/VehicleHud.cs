using UnityEngine;

namespace Atthree.Vehicles
{
    /// <summary>
    /// Basit hata ayıklama göstergesi: hız, gaz/fren ve her tekerleğin süspansiyon sıkışması.
    /// Oyuna geçerken kaldırabilirsin.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleHud : MonoBehaviour
    {
        VehicleController vehicle;
        GUIStyle style;

        void Awake() => vehicle = GetComponent<VehicleController>();

        void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };

            GUILayout.BeginArea(new Rect(12, 12, 340, 260), GUI.skin.box);
            GUILayout.Label($"{vehicle.SpeedKmh:0} km/sa", new GUIStyle(style) { fontSize = 28, fontStyle = FontStyle.Bold });
            GUILayout.Label($"Gaz {vehicle.Throttle:+0.00;-0.00}   Fren {vehicle.Brake:0.00}   El freni {(vehicle.commands.handbrake ? "AÇIK" : "-")}", style);
            GUILayout.Space(6);
            foreach (var w in vehicle.Wheels)
            {
                string bar = new string('█', Mathf.RoundToInt(w.Compression01 * 15f));
                string state = !w.IsGrounded ? "havada" : (w.IsSlipping ? "KAYIYOR" : "");
                GUILayout.Label($"{w.axle,-5} {w.side,-5} {bar,-15} {state}", style);
            }
            GUILayout.Label("WASD sür · Space el freni · R düzelt", style);
            GUILayout.EndArea();
        }
    }
}

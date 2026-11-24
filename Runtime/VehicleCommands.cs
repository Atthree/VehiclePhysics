using UnityEngine;

namespace Atthree.Vehicles
{
    /// <summary>
    /// Araca ne yapmasını istediğimizi söyleyen veri. Oyuncu girdisi (PlayerVehicleInput)
    /// ya da yapay zeka doldurur; VehicleController sadece buradan okur.
    /// </summary>
    [System.Serializable]
    public class VehicleCommands
    {
        [Tooltip("İleri / geri: +1 gaz, -1 fren ya da geri vites.")]
        [Range(-1f, 1f)] public float throttle;

        [Tooltip("Direksiyon: -1 sol, +1 sağ.")]
        [Range(-1f, 1f)] public float steer;

        public bool handbrake;
    }
}

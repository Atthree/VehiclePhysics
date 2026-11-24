using UnityEngine;
using UnityEngine.InputSystem;

namespace Atthree.Vehicles
{
    /// <summary>
    /// Oyuncu girdisini (Input System) <see cref="VehicleCommands"/>'a yazar.
    ///
    ///   W / ↑ / Sağ tetik     : gaz
    ///   S / ↓ / Sol tetik     : fren, durunca geri vites
    ///   A D / ← → / Sol stick : direksiyon
    ///   Space / Gamepad B     : el freni
    ///   R / Gamepad Y         : aracı düzelt (takla atınca)
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class PlayerVehicleInput : MonoBehaviour
    {
        public InputAction throttle = new InputAction("Throttle", InputActionType.Value);
        public InputAction steer = new InputAction("Steer", InputActionType.Value);
        public InputAction handbrake = new InputAction("Handbrake", InputActionType.Button);
        public InputAction resetVehicle = new InputAction("Reset", InputActionType.Button);

        VehicleController vehicle;

        void Reset() => AddDefaultBindings();

        void Awake()
        {
            vehicle = GetComponent<VehicleController>();
            if (throttle.bindings.Count == 0) AddDefaultBindings();
        }

        void AddDefaultBindings()
        {
            throttle = new InputAction("Throttle", InputActionType.Value);
            throttle.AddCompositeBinding("1DAxis").With("Positive", "<Keyboard>/w").With("Negative", "<Keyboard>/s");
            throttle.AddCompositeBinding("1DAxis").With("Positive", "<Keyboard>/upArrow").With("Negative", "<Keyboard>/downArrow");
            throttle.AddCompositeBinding("1DAxis").With("Positive", "<Gamepad>/rightTrigger").With("Negative", "<Gamepad>/leftTrigger");

            steer = new InputAction("Steer", InputActionType.Value);
            steer.AddCompositeBinding("1DAxis").With("Positive", "<Keyboard>/d").With("Negative", "<Keyboard>/a");
            steer.AddCompositeBinding("1DAxis").With("Positive", "<Keyboard>/rightArrow").With("Negative", "<Keyboard>/leftArrow");
            steer.AddBinding("<Gamepad>/leftStick/x");

            handbrake = new InputAction("Handbrake", InputActionType.Button);
            handbrake.AddBinding("<Keyboard>/space");
            handbrake.AddBinding("<Gamepad>/buttonEast");

            resetVehicle = new InputAction("Reset", InputActionType.Button);
            resetVehicle.AddBinding("<Keyboard>/r");
            resetVehicle.AddBinding("<Gamepad>/buttonNorth");
        }

        void OnEnable()
        {
            throttle.Enable(); steer.Enable(); handbrake.Enable(); resetVehicle.Enable();
        }

        void OnDisable()
        {
            throttle.Disable(); steer.Disable(); handbrake.Disable(); resetVehicle.Disable();
        }

        void Update()
        {
            var cmd = vehicle.commands;
            cmd.throttle = throttle.ReadValue<float>();
            cmd.steer = steer.ReadValue<float>();
            cmd.handbrake = handbrake.IsPressed();
            if (resetVehicle.WasPressedThisFrame()) vehicle.ResetUpright();
        }
    }
}

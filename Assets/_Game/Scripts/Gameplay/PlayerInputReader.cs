using UnityEngine;
using UnityEngine.InputSystem;
using YASS.Core;
using NVector2 = System.Numerics.Vector2;

namespace YASS.Gameplay
{
    /// <summary>
    /// Reads the "Player" action map and turns it into a device-independent <see cref="PlayerCommand"/>.
    /// Mouse fire aims at the pointer; otherwise the gamepad right stick or trigger decides (GDD "Controls").
    /// The aim stick is read unprocessed so that <see cref="GameTuning.GamepadAimDeadZone"/> is the only dead zone.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        const string MapName = "Player";

        [SerializeField] InputActionAsset actions;

        InputActionMap _map;
        InputAction _move;
        InputAction _aimStick;
        InputAction _fireForward;
        InputAction _pointerPosition;
        InputAction _firePointer;

        void Awake()
        {
            if (actions == null)
            {
                Debug.LogError($"{nameof(PlayerInputReader)}: no input actions asset assigned.", this);
                return;
            }

            _map = actions.FindActionMap(MapName, true);
            _move = _map.FindAction("Move", true);
            _aimStick = _map.FindAction("AimStick", true);
            _fireForward = _map.FindAction("FireForward", true);
            _pointerPosition = _map.FindAction("PointerPosition", true);
            _firePointer = _map.FindAction("FirePointer", true);
        }

        void OnEnable() => _map?.Enable();
        void OnDisable() => _map?.Disable();

        public PlayerCommand ReadCommand(NVector2 shipPosition, Camera worldCamera)
        {
            var move = _move.ReadValue<Vector2>().ToNumerics();

            bool fire;
            NVector2 direction;
            if (_firePointer.IsPressed())
            {
                var screen = _pointerPosition.ReadValue<Vector2>();
                var world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
                fire = FireInput.ResolvePointer(shipPosition, world.ToNumerics(), true, out direction);
            }
            else
            {
                fire = FireInput.ResolveGamepad(ReadUnprocessed(_aimStick).ToNumerics(), _fireForward.IsPressed(),
                    GameTuning.GamepadAimDeadZone, out direction);
            }

            return new PlayerCommand(move, fire, direction);
        }

        // The gamepad layout applies its own stick dead zone in ReadValue; bypass it for the aim stick.
        static Vector2 ReadUnprocessed(InputAction action) =>
            action.activeControl is InputControl<Vector2> control ? control.ReadUnprocessedValue() : Vector2.zero;
    }
}

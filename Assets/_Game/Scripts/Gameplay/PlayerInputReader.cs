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

        [SerializeField, Tooltip("The on-screen sticks. Optional: a scene without them plays as before.")]
        TouchSticks touch;

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
            // Each half of the command is taken from touch only while a thumb is actually on that stick, not
            // whenever the screen is being touched at all. Taking the whole command the moment either stick
            // was held meant a right thumb firing zeroed the keyboard's steering and a left thumb steering
            // stopped the mouse firing, and any stray contact killed the pad for as long as it rested there.
            // A hybrid device really does keep working with either, whichever the player reaches for.
            var thumbSteering = touch != null && touch.Move.IsHeld;
            var thumbAiming = touch != null && touch.Aim.IsHeld;
            var fromTouch = thumbSteering || thumbAiming ? touch.ReadCommand() : default;

            var move = thumbSteering ? fromTouch.Move : _move.ReadValue<Vector2>().ToNumerics();

            bool fire;
            NVector2 direction;
            if (thumbAiming)
            {
                fire = fromTouch.Fire;
                direction = fromTouch.AimDirection;
            }
            else if (_firePointer.IsPressed())
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

using Planetcraft.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Planetcraft.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [SerializeField] float walkSpeed = 5f;
        [SerializeField] float sprintSpeed = 9f;
        [SerializeField] float jumpHeight = 1.2f;
        [SerializeField] float gravity = -18f;
        [SerializeField] float lookSensitivity = 0.12f;

        CharacterController controller;
        float pitch;
        float verticalVelocity;
        int ignoreLookUntilFrame;

        public float Yaw => transform.eulerAngles.y;

        // Desligado em capturas automáticas para o mouse real não mexer na câmera.
        public bool LookEnabled { get; set; } = true;

        void Awake() => controller = GetComponent<CharacterController>();

        void Start()
        {
            UIState.MenuOpen = false;
            ignoreLookUntilFrame = Time.frameCount + 5;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            bool canAct = !UIState.MenuOpen;
            Vector2 move = Vector2.zero;
            // Ao travar o cursor o primeiro delta do mouse pode vir enorme; ignora os primeiros frames.
            if (canAct && LookEnabled && Time.frameCount > ignoreLookUntilFrame)
            {
                Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
            }

            bool grounded = controller.isGrounded;
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (grounded && canAct && kb.spaceKey.wasPressedThisFrame) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            verticalVelocity += gravity * Time.deltaTime;

            float speed = kb.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
            Vector3 velocity = (transform.right * move.x + transform.forward * move.y).normalized * speed;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }

        public void SetPitch(float degrees)
        {
            pitch = Mathf.Clamp(degrees, -85f, 85f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void Teleport(Vector3 position, float yaw)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = true;
            verticalVelocity = 0f;
        }
    }
}

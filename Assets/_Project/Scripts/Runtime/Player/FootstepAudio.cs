using Planetcraft.Core;
using UnityEngine;

namespace Planetcraft.Player
{
    // Toca um passo a cada intervalo enquanto o jogador anda no chão; mais rápido correndo.
    [RequireComponent(typeof(CharacterController))]
    public class FootstepAudio : MonoBehaviour
    {
        [SerializeField] float walkInterval = 0.5f;
        [SerializeField] float sprintInterval = 0.33f;
        [SerializeField] float sprintSpeed = 7f;
        [SerializeField, Range(0f, 1f)] float volume = 0.55f;

        CharacterController controller;
        float timer;

        void Awake() => controller = GetComponent<CharacterController>();

        void Update()
        {
            var v = controller.velocity;
            v.y = 0f;
            float speed = v.magnitude;
            if (!controller.isGrounded || speed < 1f || UIState.MenuOpen)
            {
                timer = 0.12f; // o primeiro passo sai logo ao começar a andar
                return;
            }
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            Sfx.Step(volume);
            timer = speed > sprintSpeed ? sprintInterval : walkInterval;
        }
    }
}

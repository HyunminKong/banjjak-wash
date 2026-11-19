using UnityEngine;
using UnityEngine.InputSystem;

namespace Banjjak
{
    /// <summary>First-person walker: mouse look, WASD, sprint, jump, Ctrl crouch toggle.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Camera Cam;
        public Transform Head;
        public float Yaw, Pitch;
        public bool Crouch;
        public bool Moving;
        public float Sensitivity = .11f;
        public bool InputEnabled = true;

        CharacterController cc;
        float vy, eye = 1.62f, bobT;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = .3f;
            cc.center = new Vector3(0, .875f, 0);
            cc.stepOffset = .42f;
            cc.slopeLimit = 50;
            cc.skinWidth = .03f;
            gameObject.layer = Layers.Player;
        }

        public void SetPose(Vector3 feet, float yaw, float pitch)
        {
            cc.enabled = false;
            transform.position = feet;
            cc.enabled = true;
            Yaw = yaw; Pitch = pitch; vy = 0;
            Apply(0);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f);
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (InputEnabled && locked && mouse != null)
            {
                var d = mouse.delta.ReadValue();
                Yaw += d.x * Sensitivity;
                Pitch = Mathf.Clamp(Pitch - d.y * Sensitivity, -86, 86);
            }

            Vector2 mv = Vector2.zero;
            bool sprint = false, jump = false;
            if (InputEnabled && kb != null)
            {
                if (kb.wKey.isPressed) mv.y += 1;
                if (kb.sKey.isPressed) mv.y -= 1;
                if (kb.aKey.isPressed) mv.x -= 1;
                if (kb.dKey.isPressed) mv.x += 1;
                sprint = kb.leftShiftKey.isPressed;
                bool onLift = Lift.Active != null && Lift.Active.PlayerOn;
                jump = kb.spaceKey.wasPressedThisFrame && !onLift;
                if (kb.leftCtrlKey.wasPressedThisFrame || (kb.cKey.wasPressedThisFrame && !onLift)) Crouch = !Crouch;
            }
            float speed = Crouch ? 1.7f : sprint ? 5.4f : 3.4f;
            var rot = Quaternion.Euler(0, Yaw, 0);
            var vel = (rot * new Vector3(mv.x, 0, mv.y)).normalized * speed;
            Moving = mv.sqrMagnitude > 0;

            if (cc.isGrounded)
            {
                vy = -1.5f;
                if (jump && !Crouch) vy = 4.6f;
            }
            else vy -= 14f * dt;
            cc.Move((vel + Vector3.up * vy) * dt);

            if (transform.position.y < -5) SetPose(new Vector3(transform.position.x, 1, transform.position.z), Yaw, Pitch);
            Apply(dt);
        }

        void Apply(float dt)
        {
            float targetEye = Crouch ? 1.0f : 1.62f;
            eye = dt > 0 ? Mathf.MoveTowards(eye, targetEye, dt * 3.5f) : targetEye;
            float h = Crouch ? 1.15f : 1.75f;
            cc.height = h; cc.center = new Vector3(0, h / 2, 0);
            if (Moving && cc.isGrounded) bobT += dt * (Crouch ? 6 : 9);
            float bob = Moving ? Mathf.Sin(bobT) * .025f : 0;
            transform.rotation = Quaternion.Euler(0, Yaw, 0);
            Head.localPosition = new Vector3(0, eye + bob, 0);
            Head.localRotation = Quaternion.Euler(Pitch, 0, 0);
        }

        public float BobPhase => bobT;
        public float EyeHeight => eye;
    }
}

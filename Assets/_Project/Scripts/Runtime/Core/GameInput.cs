using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Planetcraft.Core
{
    // Entrada do jogo: ações do Input System com teclado/mouse e controle, remapeáveis pelo jogador.
    // Os remapeamentos ficam em PlayerPrefs (JSON de overrides do Input System).
    public static class GameInput
    {
        const string OverridesKey = "pc.bindings";

        static InputActionMap map;

        public static InputAction Move { get; private set; }
        public static InputAction Look { get; private set; }
        public static InputAction LookStick { get; private set; }
        public static InputAction Jump { get; private set; }
        public static InputAction Sprint { get; private set; }
        public static InputAction Interact { get; private set; }
        public static InputAction Deconstruct { get; private set; }
        public static InputAction Menu { get; private set; }
        public static InputAction Pause { get; private set; }
        public static InputAction Place { get; private set; }
        public static InputAction Cancel { get; private set; }
        public static InputAction Rotate { get; private set; }
        public static InputAction QuickSave { get; private set; }
        public static InputAction QuickLoad { get; private set; }
        public static InputAction SimulationSpeed { get; private set; }

        // Ações que aparecem na tela de controles (rótulo, ação). Movimento e câmera ficam fixos.
        public static (string label, InputAction action)[] Rebindable => new[]
        {
            ("Pular", Jump), ("Correr", Sprint), ("Interagir", Interact), ("Desmontar", Deconstruct),
            ("Menu", Menu), ("Pausa", Pause), ("Construir / confirmar", Place), ("Cancelar", Cancel),
            ("Girar construção", Rotate), ("Salvar", QuickSave), ("Carregar", QuickLoad),
        };

        /// <summary>Último dispositivo usado: troca as dicas de tecla na tela (teclado ou controle).</summary>
        public static bool UsingGamepad { get; private set; }

        public static event Action BindingsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            map?.Dispose();
            map = null;
            BindingsChanged = null;
            UsingGamepad = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Init()
        {
            if (map != null) return;
            map = new InputActionMap("Jogo");

            Move = map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");

            // Mouse em pixels por frame e analógico em -1..1: escalas diferentes, por isso são ações separadas.
            Look = map.AddAction("Look", InputActionType.Value);
            Look.AddBinding("<Mouse>/delta");
            LookStick = map.AddAction("LookStick", InputActionType.Value);
            LookStick.AddBinding("<Gamepad>/rightStick");

            Jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Sprint = Button("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            Interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            Deconstruct = Button("Deconstruct", "<Keyboard>/x", "<Gamepad>/buttonNorth");
            Menu = Button("Menu", "<Keyboard>/tab", "<Gamepad>/select");
            Pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");
            Place = Button("Place", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            Cancel = Button("Cancel", "<Mouse>/rightButton", "<Gamepad>/buttonEast");
            Rotate = Button("Rotate", "<Keyboard>/r", "<Gamepad>/rightShoulder");
            QuickSave = Button("QuickSave", "<Keyboard>/f5", null);
            QuickLoad = Button("QuickLoad", "<Keyboard>/f9", null);
            SimulationSpeed = Button("SimulationSpeed", "<Keyboard>/f2", null);

            string saved = PlayerPrefs.GetString(OverridesKey, "");
            if (!string.IsNullOrEmpty(saved)) map.LoadBindingOverridesFromJson(saved);
            map.actionTriggered += ctx => UsingGamepad = ctx.control?.device is Gamepad;
            map.Enable();
        }

        static InputAction Button(string name, string keyboard, string gamepad)
        {
            var a = map.AddAction(name, InputActionType.Button);
            a.AddBinding(keyboard).WithGroup("Keyboard");
            if (gamepad != null) a.AddBinding(gamepad).WithGroup("Gamepad");
            return a;
        }

        /// <summary>Índice do binding de teclado/mouse da ação (o que a tela de controles remapeia).</summary>
        public static int KeyboardBinding(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
                if (!action.bindings[i].isComposite && !action.bindings[i].effectivePath.StartsWith("<Gamepad>")) return i;
            return 0;
        }

        public static string Display(InputAction action, bool gamepad = false)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                bool isPad = action.bindings[i].effectivePath.StartsWith("<Gamepad>");
                if (isPad == gamepad && !action.bindings[i].isComposite)
                    return action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions).ToUpperInvariant();
            }
            return "—";
        }

        /// <summary>Dica de tecla para a tela, conforme o dispositivo em uso ("[E]" ou "[X]" do controle).</summary>
        public static string Hint(InputAction action) => $"[{Display(action, UsingGamepad)}]";

        /// <summary>Remapeamento interativo: espera a próxima tecla/botão (Esc cancela) e salva.</summary>
        public static void StartRebind(InputAction action, Action done)
        {
            int index = KeyboardBinding(action);
            map.Disable();
            action.PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Gamepad>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op => Finish(op, done))
                .OnCancel(op => Finish(op, done))
                .Start();
        }

        static void Finish(InputActionRebindingExtensions.RebindingOperation op, Action done)
        {
            op.Dispose();
            map.Enable();
            SaveOverrides();
            done?.Invoke();
        }

        public static void ResetBindings()
        {
            map.RemoveAllBindingOverrides();
            SaveOverrides();
        }

        static void SaveOverrides()
        {
            PlayerPrefs.SetString(OverridesKey, map.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
        }
    }
}

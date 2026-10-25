using UnityEngine;

namespace Planetcraft.Core
{
    public static class UIState
    {
        static bool menuOpen;

        // Menu aberto = cursor livre e controles de câmera/ação bloqueados.
        public static bool MenuOpen
        {
            get => menuOpen;
            set
            {
                menuOpen = value;
                Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = value;
            }
        }
    }
}

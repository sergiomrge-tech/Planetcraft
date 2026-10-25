using Planetcraft.Core;
using Planetcraft.Player;
using Planetcraft.UI;
using UnityEngine;

namespace Planetcraft.World
{
    // Terminal com um registro de história; ler marca como lido e abre o leitor do HUD.
    public class LogTerminal : MonoBehaviour, IInteractable
    {
        [SerializeField] StoryLog log;

        public string Prompt => log == null ? "" :
            StoryLogs.IsRead(log.id) ? $"[E] Reler: {log.title}" : $"[E] Ler registro: {log.title}";

        public void Interact(GameObject interactor)
        {
            if (log == null) return;
            StoryLogs.MarkRead(log.id);
            if (GameHud.Instance != null) GameHud.Instance.ShowLog(log);
        }

#if UNITY_EDITOR
        public void EditorSetup(StoryLog storyLog) => log = storyLog;
#endif
    }
}

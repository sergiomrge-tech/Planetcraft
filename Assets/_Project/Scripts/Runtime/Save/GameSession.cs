using Planetcraft.Building;
using Planetcraft.Core;
using Planetcraft.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Planetcraft.Save
{
    public static class Scenes
    {
        public const string Title = "Title";
        public const string Game = "Prototype";
        public const string Epilogue = "Epilogue";
    }

    // Transições entre título e jogo. O estado global (projetos, registros, nós coletados...) vive em
    // estáticos, então um jogo novo precisa limpá-lo explicitamente.
    public static class GameSession
    {
        public static void NewGame()
        {
            SaveSystem.ClearPendingLoad();
            PlayerPrefs.DeleteKey(UI.TutorialHints.PrefKey); // as dicas de primeira vez voltam num jogo novo
            Blueprints.Restore(null);
            StoryLogs.Restore(null);
            ResourceNode.RestoreCollected(null);
            ShipProgress.Restore(null, false);
            PlayClock.Seconds = 0;
            StartGame();
        }

        public static bool ContinueGame()
        {
            if (!SaveSystem.QueueLoadFromFile()) return false;
            StartGame();
            return true;
        }

        public static void StartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(Scenes.Game);
        }

        public static void ToTitle()
        {
            Time.timeScale = 1f;
            UIState.MenuOpen = true;
            SceneManager.LoadScene(Scenes.Title);
        }
    }
}

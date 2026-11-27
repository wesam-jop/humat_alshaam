using UnityEngine;
using UnityEngine.SceneManagement;

namespace Adventure3D.Bootstrap
{
    /// <summary>
    /// Lives in Bootstrap.unity (build index 0), the natural entry point. Creates the single AppRoot,
    /// then opens the Arabic main menu, or starts the session requested on the command line
    /// (dedicated server, automated tests).
    /// </summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        void Start()
        {
            var app = AppRoot.EnsureExists();
            var request = app.Options.ToRequest(app.Config.Network);
            if (request != null) app.StartSession(request);
            else SceneManager.LoadScene(SceneNames.MainMenu);
        }
    }
}

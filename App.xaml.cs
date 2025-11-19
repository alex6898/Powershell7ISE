using System.Windows;
using PsConsoleHost.Services;

namespace PsConsoleHost
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialiser le service de mise à jour automatique
            UpdateService.Initialize();
            
            // Optionnel: Vérifier les mises à jour au démarrage (décommentez si souhaité)
            // UpdateService.CheckForUpdate();
        }
    }
}

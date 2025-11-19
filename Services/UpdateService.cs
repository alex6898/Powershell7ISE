using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace PsConsoleHost.Services
{
    /// <summary>
    /// Service de gestion des mises à jour automatiques via GitHub Releases
    /// </summary>
    public class UpdateService
    {
        // URL de votre fichier XML de version sur GitHub
        // Format: https://raw.githubusercontent.com/USERNAME/REPO/BRANCH/version.xml
        private const string UpdateUrl = "https://raw.githubusercontent.com/alex6898/Powershell7ISE/main/version.xml";
        
        // Version actuelle de l'application (lue automatiquement depuis l'assembly)
        private static readonly Version CurrentVersion = GetCurrentVersion();
        
        /// <summary>
        /// Récupère la version actuelle depuis l'assembly
        /// </summary>
        private static Version GetCurrentVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                if (version != null)
                {
                    // Si Build est -1, utiliser seulement Major.Minor
                    if (version.Build >= 0)
                    {
                        return new Version(version.Major, version.Minor, version.Build);
                    }
                    else
                    {
                        return new Version(version.Major, version.Minor);
                    }
                }
                
                // Essayer aussi avec AssemblyInformationalVersion
                var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (infoVersion != null && !string.IsNullOrEmpty(infoVersion.InformationalVersion))
                {
                    // Extraire seulement la partie version (avant le + si présent)
                    var versionStr = infoVersion.InformationalVersion.Split('+')[0].Split('-')[0];
                    if (Version.TryParse(versionStr, out var parsedVersion))
                    {
                        return new Version(parsedVersion.Major, parsedVersion.Minor, parsedVersion.Build >= 0 ? parsedVersion.Build : 0);
                    }
                }
            }
            catch
            {
                // En cas d'erreur, utiliser une version par défaut
            }
            return new Version("1.0.0");
        }

        /// <summary>
        /// Initialise le service de mise à jour automatique
        /// </summary>
        public static void Initialize()
        {
            // Le service est initialisé mais ne vérifie pas automatiquement au démarrage
            // Décommentez la ligne suivante pour vérifier automatiquement au démarrage :
            // _ = CheckForUpdateAsync();
        }

        /// <summary>
        /// Vérifie manuellement les mises à jour disponibles
        /// </summary>
        public static void CheckForUpdate()
        {
            _ = CheckForUpdateAsync();
        }

        /// <summary>
        /// Vérifie les mises à jour de manière asynchrone
        /// </summary>
        private static async Task CheckForUpdateAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                
                var response = await client.GetStringAsync(UpdateUrl);
                var doc = XDocument.Parse(response);
                var item = doc.Element("item");
                
                if (item == null)
                {
                    MessageBox.Show(
                        "Impossible de lire les informations de mise à jour.",
                        "Erreur de vérification",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var versionStr = item.Element("version")?.Value;
                var downloadUrl = item.Element("url")?.Value;
                var changelogUrl = item.Element("changelog")?.Value;
                var mandatory = item.Element("mandatory")?.Value == "true";

                if (string.IsNullOrEmpty(versionStr) || string.IsNullOrEmpty(downloadUrl))
                {
                    MessageBox.Show(
                        "Les informations de mise à jour sont incomplètes.",
                        "Erreur de vérification",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var latestVersion = new Version(versionStr);
                
                // Debug: Afficher les versions pour diagnostic
                System.Diagnostics.Debug.WriteLine($"Version actuelle: {CurrentVersion}, Version disponible: {latestVersion}");
                
                if (latestVersion > CurrentVersion)
                {
                    var message = $"Une nouvelle version ({versionStr}) est disponible.\n\n" +
                                 $"Vous utilisez actuellement la version {CurrentVersion}.\n\n" +
                                 $"Voulez-vous télécharger et installer la mise à jour maintenant ?";
                    
                    if (mandatory)
                    {
                        message += "\n\nCette mise à jour est obligatoire.";
                    }

                    var dialogResult = MessageBox.Show(
                        message,
                        "Mise à jour disponible",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (dialogResult == MessageBoxResult.Yes)
                    {
                        await DownloadAndInstallUpdateAsync(downloadUrl);
                    }
                }
                else
                {
                    // Afficher un message de diagnostic si les versions sont identiques
                    var diagnosticMessage = latestVersion == CurrentVersion
                        ? $"Vous utilisez déjà la dernière version disponible (v{CurrentVersion})."
                        : $"Version actuelle: {CurrentVersion}\nVersion disponible: {latestVersion}\n\nAucune mise à jour nécessaire.";
                    
                    MessageBox.Show(
                        diagnosticMessage,
                        "Aucune mise à jour",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Impossible de vérifier les mises à jour:\n{ex.Message}\n\nVérifiez votre connexion Internet.",
                    "Erreur de vérification",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Télécharge et installe la mise à jour
        /// </summary>
        private static async Task DownloadAndInstallUpdateAsync(string downloadUrl)
        {
            try
            {
                var downloadPath = Path.Combine(
                    Path.GetTempPath(),
                    "Powershell7ISE",
                    $"Update_{DateTime.Now:yyyyMMddHHmmss}.exe");

                Directory.CreateDirectory(Path.GetDirectoryName(downloadPath)!);

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(10);

                MessageBox.Show(
                    "Téléchargement de la mise à jour en cours...\n\nL'application va se fermer une fois le téléchargement terminé.",
                    "Téléchargement",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                var fileBytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(downloadPath, fileBytes);

                // Lancer l'installateur
                var processInfo = new ProcessStartInfo
                {
                    FileName = downloadPath,
                    UseShellExecute = true
                };

                Process.Start(processInfo);

                // Fermer l'application
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erreur lors du téléchargement de la mise à jour:\n{ex.Message}",
                    "Erreur de mise à jour",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}


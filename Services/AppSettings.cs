using System;
using System.IO;
using System.Text.Json;

namespace PsConsoleHost.Services
{
    /// <summary>
    /// Gère les paramètres de l'application
    /// </summary>
    public class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PsConsoleHost",
            "settings.json");

        public const string DefaultClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";

        private string? _lastSelectedSharePointSiteId;

        public string SharePointClientId => DefaultClientId;

        /// <summary>
        /// ID du dernier site SharePoint sélectionné
        /// </summary>
        public string? LastSelectedSharePointSiteId
        {
            get => _lastSelectedSharePointSiteId;
            set => _lastSelectedSharePointSiteId = value;
        }


        /// <summary>
        /// Charge les paramètres depuis le fichier
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch
            {
                // En cas d'erreur, retourne les paramètres par défaut
            }

            return new AppSettings();
        }

        /// <summary>
        /// Enregistre les paramètres dans le fichier
        /// </summary>
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Ignore les erreurs d'écriture
            }
        }
    }
}


using PsConsoleHost.Services;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SharePointFolder = PsConsoleHost.Services.SharePointFolder;

namespace PsConsoleHost
{
    public partial class SharePointBrowser : Window
    {
        private readonly SharePointService _sharePointService;
        private AppSettings _settings;
        private SharePointSite? _selectedSite;
        private SharePointFile? _selectedFile;

        public string? SelectedFileContent { get; private set; }
        public string? SelectedFileName { get; private set; }

        public SharePointBrowser()
        {
            InitializeComponent();
            _sharePointService = SharePointService.Instance;
            _settings = AppSettings.Load();
            Loaded += SharePointBrowser_Loaded;
        }

        private async void SharePointBrowser_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var isAuthenticated = await _sharePointService.TryAuthenticateSilentlyAsync();
                if (isAuthenticated)
                {
                    StatusText.Text = "Connecté";
                    ConnectButton.Content = "✅ Connecté";
                    ConnectButton.IsEnabled = false;
                    DisconnectButton.Visibility = Visibility.Visible;
                    await LoadSitesAsync();
                }
                else
                {
                    StatusText.Text = "Non connecté";
                    ConnectButton.IsEnabled = true;
                }
            }
            catch
            {
                StatusText.Text = "Non connecté";
                ConnectButton.IsEnabled = true;
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConnectButton.IsEnabled = false;
                ProgressBar.Visibility = Visibility.Visible;
                StatusText.Text = "Connexion en cours...";

                var authenticated = await _sharePointService.AuthenticateAsync();

                if (authenticated)
                {
                    StatusText.Text = "Connecté";
                    ConnectButton.Content = "✅ Connecté";
                    ConnectButton.IsEnabled = false;
                    DisconnectButton.Visibility = Visibility.Visible;

                    await LoadSitesAsync();
                }
                else
                {
                    StatusText.Text = "Échec de la connexion";
                    MessageBox.Show(
                        "Impossible de se connecter à SharePoint. Vérifiez votre connexion et vos identifiants.",
                        "Erreur de connexion",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (MsalServiceException msalEx)
            {
                StatusText.Text = "Erreur de connexion";
                
                string errorMessage = "Impossible de se connecter à SharePoint.\n\n";
                
                if (msalEx.ErrorCode == "AADSTS700016" || msalEx.ErrorCode == "invalid_client")
                {
                    errorMessage += "Le Client ID n'est pas valide ou n'est pas configuré correctement.\n\n";
                    errorMessage += "Solution : Cliquez sur '⚙️ Paramètres' pour configurer un Client ID Azure AD valide.\n\n";
                    errorMessage += "Vous pouvez créer un Client ID gratuitement sur https://portal.azure.com";
                }
                else if (msalEx.ErrorCode == "AADSTS50020")
                {
                    errorMessage += "Le Client ID n'est pas autorisé pour ce type d'application.\n\n";
                    errorMessage += "Solution : Configurez un Client ID avec le type 'Public client/native' dans Azure AD.";
                }
                else
                {
                    errorMessage += $"Code d'erreur : {msalEx.ErrorCode}\n";
                    errorMessage += $"Détails : {msalEx.Message}";
                }
                
                MessageBox.Show(
                    errorMessage,
                    "Erreur de connexion",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Erreur de connexion";
                
                string errorMessage = $"Erreur lors de la connexion : {ex.Message}\n\n";
                
                if (ex.InnerException != null)
                {
                    errorMessage += $"Détails : {ex.InnerException.Message}\n\n";
                }
                
                errorMessage += "Conseil : Si le problème persiste, vérifiez votre connexion Internet et essayez de configurer un Client ID personnalisé dans les paramètres.";
                
                MessageBox.Show(
                    errorMessage,
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                ConnectButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadSitesAsync()
        {
            try
            {
                ProgressBar.Visibility = Visibility.Visible;
                SitesListBox.ItemsSource = null;

                var sites = await _sharePointService.GetSitesAsync();
                SitesListBox.ItemsSource = sites;

                if (sites.Count == 0)
                {
                    StatusText.Text = "Aucun site trouvé";
                }
                else
                {
                    var lastSiteId = _settings.LastSelectedSharePointSiteId;
                    if (!string.IsNullOrEmpty(lastSiteId))
                    {
                        var lastSite = sites.FirstOrDefault(s => s.Id == lastSiteId);
                        if (lastSite != null)
                        {
                            SitesListBox.SelectedItem = lastSite;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erreur lors du chargement des sites: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void SitesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SitesListBox.SelectedItem is SharePointSite site)
            {
                _selectedSite = site;
                _settings.LastSelectedSharePointSiteId = site.Id;
                _settings.Save();
                await LoadFolderTreeAsync(site.Id);
            }
        }

        private async Task LoadFolderTreeAsync(string siteId)
        {
            try
            {
                ProgressBar.Visibility = Visibility.Visible;
                FolderTreeView.ItemsSource = null;
                OpenButton.IsEnabled = false;

                var rootFolder = await _sharePointService.GetFolderTreeAsync(siteId, ".ps1");
                
                if (rootFolder.ItemCount > 0 || rootFolder.SubFolders.Count > 0)
                {
                    FolderTreeView.ItemsSource = new[] { rootFolder };
                    
                    int totalFiles = CountFiles(rootFolder);
                    StatusText.Text = totalFiles > 0
                        ? $"{totalFiles} script(s) trouvé(s)"
                        : "Aucun script .ps1 trouvé dans ce site";
                }
                else
                {
                    StatusText.Text = "Aucun script .ps1 trouvé dans ce site";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erreur lors du chargement de l'arborescence: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private int CountFiles(SharePointFolder folder)
        {
            int count = folder.Files.Count;
            foreach (var subFolder in folder.SubFolders)
            {
                count += CountFiles(subFolder);
            }
            return count;
        }

        private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is SharePointFile file)
            {
                _selectedFile = file;
                OpenButton.IsEnabled = true;
            }
            else
            {
                _selectedFile = null;
                OpenButton.IsEnabled = false;
            }
        }

        private async void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSite == null || _selectedFile == null)
                return;

            try
            {
                OpenButton.IsEnabled = false;
                ProgressBar.Visibility = Visibility.Visible;
                StatusText.Text = "Téléchargement en cours...";

                SelectedFileContent = await _sharePointService.DownloadFileContentAsync(
                    _selectedSite.Id, 
                    _selectedFile.Id);
                SelectedFileName = _selectedFile.Name;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erreur lors du téléchargement du fichier: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                OpenButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _sharePointService.SignOutAsync();
                
                StatusText.Text = "Déconnecté";
                ConnectButton.Content = "🔐 Se connecter à SharePoint";
                ConnectButton.IsEnabled = true;
                DisconnectButton.Visibility = Visibility.Collapsed;
                SitesListBox.ItemsSource = null;
                FolderTreeView.ItemsSource = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erreur lors de la déconnexion: {ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


    }
}


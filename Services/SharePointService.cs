using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Desktop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace PsConsoleHost.Services
{
    /// <summary>
    /// Service pour interagir avec SharePoint via Microsoft Graph API
    /// </summary>
    public class SharePointService
    {
        private static SharePointService? _instance;
        private static readonly object _lock = new object();

        private HttpClient? _httpClient;
        private IPublicClientApplication? _publicClientApp;
        private string? _accessToken;
        private string? _currentClientId; // Pour détecter les changements de Client ID
        
        // Scopes nécessaires pour accéder à SharePoint
        private readonly string[] _scopes = { "Files.Read.All", "Sites.Read.All", "User.Read" };
        
        private const string GraphApiBaseUrl = "https://graph.microsoft.com/v1.0";

        /// <summary>
        /// Instance singleton du service SharePoint
        /// </summary>
        public static SharePointService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SharePointService();
                        }
                    }
                }
                return _instance;
            }
        }

        private SharePointService()
        {
        }

        /// <summary>
        /// Initialise l'application MSAL
        /// </summary>
        private void InitializePublicClientApp()
        {
            var clientId = AppSettings.DefaultClientId;
            
            if (_publicClientApp == null || _currentClientId != clientId)
            {
                if (_publicClientApp != null && _currentClientId != clientId)
                {
                    _httpClient?.Dispose();
                    _httpClient = null;
                    _accessToken = null;
                }
                
                _currentClientId = clientId;
                
                _publicClientApp = PublicClientApplicationBuilder
                    .Create(clientId)
                    .WithRedirectUri("http://localhost")
                    .WithAuthority(AzureCloudInstance.AzurePublic, "common")
                    .WithWindowsEmbeddedBrowserSupport()
                    .Build();
            }
        }

        /// <summary>
        /// Réinitialise l'application MSAL
        /// </summary>
        public void ResetAuthentication()
        {
            _httpClient?.Dispose();
            _httpClient = null;
            _accessToken = null;
            _publicClientApp = null;
            _currentClientId = null;
        }

        /// <summary>
        /// Vérifie si l'utilisateur est déjà authentifié (utilise le cache MSAL)
        /// </summary>
        public async Task<bool> TryAuthenticateSilentlyAsync()
        {
            // Si déjà authentifié, retourner true immédiatement
            if (IsAuthenticated())
            {
                return true;
            }

            try
            {
                InitializePublicClientApp();

                var accounts = await _publicClientApp!.GetAccountsAsync();
                if (accounts == null || !accounts.Any())
                {
                    return false;
                }

                try
                {
                    var result = await _publicClientApp
                        .AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                        .ExecuteAsync();

                    if (result != null)
                    {
                        _accessToken = result.AccessToken;
                        
                        if (_httpClient == null)
                        {
                            _httpClient = new HttpClient();
                        }
                        
                        _httpClient.DefaultRequestHeaders.Remove("Authorization");
                        _httpClient.DefaultRequestHeaders.Authorization = 
                            new AuthenticationHeaderValue("Bearer", _accessToken);
                        return true;
                    }
                }
                catch (MsalUiRequiredException)
                {
                    return false;
                }
                catch (MsalException)
                {
                    return false;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Initialise et authentifie l'utilisateur avec Microsoft Graph (SSO)
        /// </summary>
        public async Task<bool> AuthenticateAsync()
        {
            try
            {
                InitializePublicClientApp();

                var accounts = await _publicClientApp!.GetAccountsAsync();
                AuthenticationResult? result;

                try
                {
                    result = await _publicClientApp
                        .AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                        .ExecuteAsync();
                }
                catch (MsalUiRequiredException)
                {
                    try
                    {
                        result = await _publicClientApp
                            .AcquireTokenInteractive(_scopes)
                            .ExecuteAsync();
                    }
                    catch (MsalException msalEx)
                    {
                        if (msalEx.ErrorCode == "user_cancelled" || msalEx.ErrorCode == "authentication_canceled")
                        {
                            throw new Exception("L'authentification a été annulée par l'utilisateur.", msalEx);
                        }
                        throw;
                    }
                }

                if (result != null)
                {
                    _accessToken = result.AccessToken;
                    
                    if (_httpClient == null)
                    {
                        _httpClient = new HttpClient();
                    }
                    
                    _httpClient.DefaultRequestHeaders.Remove("Authorization");
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Bearer", _accessToken);
                    return true;
                }

                return false;
            }
            catch (MsalException msalEx)
            {
                throw new Exception($"Erreur d'authentification Microsoft: {msalEx.Message}", msalEx);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de la connexion: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Vérifie si l'utilisateur est actuellement authentifié
        /// </summary>
        public bool IsAuthenticated()
        {
            return _httpClient != null && !string.IsNullOrEmpty(_accessToken);
        }

        /// <summary>
        /// Récupère la liste des sites SharePoint accessibles
        /// </summary>
        public async Task<List<SharePointSite>> GetSitesAsync()
        {
            if (_httpClient == null || string.IsNullOrEmpty(_accessToken))
                throw new InvalidOperationException("Non authentifié. Appelez AuthenticateAsync() d'abord.");

            var sites = new List<SharePointSite>();
            var siteIds = new HashSet<string>(); // Pour éviter les doublons

            try
            {
                await GetSitesFromUrlAsync($"{GraphApiBaseUrl}/me/followedSites", sites, siteIds);
                await GetSitesFromUrlAsync($"{GraphApiBaseUrl}/sites?$select=id,displayName,name,webUrl&$top=200", sites, siteIds);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur lors de la récupération des sites: {ex.Message}");
            }

            return sites;
        }

        /// <summary>
        /// Récupère les sites depuis une URL Graph API avec pagination
        /// </summary>
        private async Task GetSitesFromUrlAsync(string url, List<SharePointSite> sites, HashSet<string> siteIds)
        {
            try
            {
                await ProcessPaginatedResponseAsync(url, async (doc) =>
                {
                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var siteElement in valueArray.EnumerateArray())
                        {
                            var site = ParseSharePointSite(siteElement);
                            if (site != null && !string.IsNullOrEmpty(site.Id) && !siteIds.Contains(site.Id))
                            {
                                siteIds.Add(site.Id);
                                sites.Add(site);
                            }
                        }
                    }
                });
            }
            catch
            {
                // Ignore les erreurs
            }
        }

        /// <summary>
        /// Parse un élément JSON en SharePointSite
        /// </summary>
        private static SharePointSite? ParseSharePointSite(JsonElement element)
        {
            var siteId = element.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(siteId))
                return null;

            string siteName = "Sans nom";
            if (element.TryGetProperty("displayName", out var nameProp))
            {
                siteName = nameProp.GetString() ?? "Sans nom";
            }
            else if (element.TryGetProperty("name", out var nameProp2))
            {
                siteName = nameProp2.GetString() ?? "Sans nom";
            }

            return new SharePointSite
            {
                Id = siteId,
                Name = siteName,
                WebUrl = element.TryGetProperty("webUrl", out var urlProp) ? urlProp.GetString() ?? "" : ""
            };
        }

        /// <summary>
        /// Parse un élément JSON en SharePointFile
        /// </summary>
        private static SharePointFile? ParseSharePointFile(JsonElement element, string? folderPath = null, string? fileExtension = null)
        {
            var name = element.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
            
            if (fileExtension != null && !name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                return null;

            var id = element.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(id))
                return null;

            return new SharePointFile
            {
                Id = id,
                Name = name,
                WebUrl = element.TryGetProperty("webUrl", out var urlProp) ? urlProp.GetString() ?? "" : "",
                Size = element.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0,
                LastModified = element.TryGetProperty("lastModifiedDateTime", out var dateProp)
                    && DateTime.TryParse(dateProp.GetString(), out var date)
                    ? date
                    : DateTime.MinValue,
                FolderPath = folderPath
            };
        }

        /// <summary>
        /// Traite une réponse paginée de l'API Graph
        /// </summary>
        private async Task ProcessPaginatedResponseAsync(string initialUrl, Func<JsonDocument, Task> processPage)
        {
            var url = initialUrl;
            while (!string.IsNullOrEmpty(url))
            {
                var response = await _httpClient!.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    break;

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                
                await processPage(doc);

                // Vérifie s'il y a une page suivante
                url = doc.RootElement.TryGetProperty("@odata.nextLink", out var nextLinkProp)
                    ? nextLinkProp.GetString()
                    : null;
            }
        }

        /// <summary>
        /// Récupère les fichiers d'un site SharePoint (optionnellement filtrés par extension)
        /// </summary>
        public async Task<List<SharePointFile>> GetFilesAsync(string siteId, string? folderPath = null, string? fileExtension = ".ps1")
        {
            if (_httpClient == null || string.IsNullOrEmpty(_accessToken))
                throw new InvalidOperationException("Non authentifié. Appelez AuthenticateAsync() d'abord.");

            var files = new List<SharePointFile>();

            try
            {
                var driveUrl = $"{GraphApiBaseUrl}/sites/{siteId}/drive";
                var driveResponse = await _httpClient.GetAsync(driveUrl);
                
                if (!driveResponse.IsSuccessStatusCode)
                    return files;

                var itemsUrl = string.IsNullOrEmpty(folderPath)
                    ? $"{GraphApiBaseUrl}/sites/{siteId}/drive/root/children"
                    : $"{GraphApiBaseUrl}/sites/{siteId}/drive/root:/{folderPath}:/children";

                await ProcessPaginatedResponseAsync(itemsUrl, async (doc) =>
                {
                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var itemElement in valueArray.EnumerateArray())
                        {
                            if (itemElement.TryGetProperty("file", out _))
                            {
                                var file = ParseSharePointFile(itemElement, folderPath, fileExtension);
                                if (file != null)
                                {
                                    files.Add(file);
                                }
                            }
                        }
                    }
                });
            }
            catch
            {
                // Retourne une liste vide en cas d'erreur
            }

            return files;
        }

        /// <summary>
        /// Récupère l'arborescence complète des dossiers et fichiers d'un site SharePoint
        /// </summary>
        public async Task<SharePointFolder> GetFolderTreeAsync(string siteId, string? fileExtension = ".ps1")
        {
            if (_httpClient == null || string.IsNullOrEmpty(_accessToken))
                throw new InvalidOperationException("Non authentifié. Appelez AuthenticateAsync() d'abord.");

            var rootFolder = new SharePointFolder
            {
                Id = "root",
                Name = "Racine",
                Path = ""
            };

            await LoadFolderRecursiveAsync(siteId, rootFolder, fileExtension);

            return rootFolder;
        }

        /// <summary>
        /// Charge récursivement un dossier et ses enfants
        /// </summary>
        private async Task LoadFolderRecursiveAsync(string siteId, SharePointFolder folder, string? fileExtension)
        {
            try
            {
                var itemsUrl = string.IsNullOrEmpty(folder.Path)
                    ? $"{GraphApiBaseUrl}/sites/{siteId}/drive/root/children"
                    : $"{GraphApiBaseUrl}/sites/{siteId}/drive/root:/{folder.Path}:/children";

                await ProcessPaginatedResponseAsync(itemsUrl, async (doc) =>
                {
                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var itemElement in valueArray.EnumerateArray())
                        {
                            var name = itemElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                            var id = itemElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";

                            if (itemElement.TryGetProperty("folder", out _))
                            {
                                var newFolder = new SharePointFolder
                                {
                                    Id = id,
                                    Name = name,
                                    Path = string.IsNullOrEmpty(folder.Path) ? name : $"{folder.Path}/{name}"
                                };
                                
                                folder.SubFolders.Add(newFolder);
                                await LoadFolderRecursiveAsync(siteId, newFolder, fileExtension);
                            }
                            else if (itemElement.TryGetProperty("file", out _))
                            {
                                var file = ParseSharePointFile(itemElement, folder.Path, fileExtension);
                                if (file != null)
                                {
                                    folder.Files.Add(file);
                                }
                            }
                        }
                    }
                });
            }
            catch
            {
                // Continue avec les autres éléments en cas d'erreur
            }
        }

        /// <summary>
        /// Télécharge le contenu d'un fichier SharePoint
        /// </summary>
        public async Task<string> DownloadFileContentAsync(string siteId, string fileId)
        {
            if (_httpClient == null || string.IsNullOrEmpty(_accessToken))
                throw new InvalidOperationException("Non authentifié. Appelez AuthenticateAsync() d'abord.");

            try
            {
                var url = $"{GraphApiBaseUrl}/sites/{siteId}/drive/items/{fileId}/content";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    throw new Exception($"Erreur HTTP {response.StatusCode}: {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors du téléchargement du fichier: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Déconnecte l'utilisateur
        /// </summary>
        public async Task SignOutAsync()
        {
            if (_publicClientApp != null)
            {
                var accounts = await _publicClientApp.GetAccountsAsync();
                foreach (var account in accounts)
                {
                    await _publicClientApp.RemoveAsync(account);
                }
            }
            _httpClient?.Dispose();
            _httpClient = null;
            _accessToken = null;
        }

    }

    /// <summary>
    /// Représente un site SharePoint
    /// </summary>
    public class SharePointSite
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string WebUrl { get; set; } = "";
    }

    /// <summary>
    /// Représente un fichier SharePoint
    /// </summary>
    public class SharePointFile
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string WebUrl { get; set; } = "";
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string? FolderPath { get; set; }
    }

    /// <summary>
    /// Représente un dossier SharePoint avec ses sous-dossiers et fichiers
    /// </summary>
    public class SharePointFolder
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public List<SharePointFolder> SubFolders { get; set; } = new();
        public List<SharePointFile> Files { get; set; } = new();
        public bool IsExpanded { get; set; } = false;

        /// <summary>
        /// Retourne une liste combinée de sous-dossiers et fichiers pour le TreeView
        /// </summary>
        public List<object> Items
        {
            get
            {
                var items = new List<object>();
                items.AddRange(SubFolders.Cast<object>());
                items.AddRange(Files.Cast<object>());
                return items;
            }
        }

        /// <summary>
        /// Retourne le nombre total d'éléments (dossiers + fichiers)
        /// </summary>
        public int ItemCount => SubFolders.Count + Files.Count;
    }
}

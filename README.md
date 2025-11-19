# Powershell 7 ISE

WPF .NET 8 hébergeant PowerShell 7 via redirection `stdin/stdout`.

## ✨ Fonctionnalités

- 🖥️ Terminal PowerShell 7 intégré avec coloration syntaxique
- 📝 Éditeur de scripts PowerShell avec coloration syntaxique
- 💾 Ouvrir/Enregistrer des scripts .ps1
- ☁️ Intégration SharePoint pour ouvrir des scripts
- 🔄 **Mises à jour automatiques via GitHub Releases**
- ▶️ Exécution de scripts (F5) ou sélection (F8)
- 🎨 Interface moderne avec thème sombre

## Prérequis
- Windows 10/11
- .NET SDK 8+
- PowerShell 7 (`pwsh` doit être dans le PATH)

## 🚀 Lancer

### Développement
```bash
cd PsConsoleHost
dotnet build
dotnet run
```

### Installation
Téléchargez l'installateur depuis les [Releases GitHub](https://github.com/alex6898/Powershell7ISE/releases) ou consultez [INSTALLATION_INNO_SETUP.md](INSTALLATION_INNO_SETUP.md) pour créer votre propre installateur.

## 🔄 Mises à jour automatiques

L'application vérifie automatiquement les mises à jour disponibles via GitHub Releases. Les utilisateurs peuvent :
- Recevoir une notification lorsqu'une nouvelle version est disponible
- Télécharger et installer automatiquement les mises à jour
- Vérifier manuellement les mises à jour via le bouton "🔄 Vérifier les mises à jour"

Pour configurer les mises à jour automatiques, consultez [GITHUB_SETUP.md](GITHUB_SETUP.md).

## 📦 Créer une release

Utilisez le script PowerShell fourni pour créer facilement une nouvelle release :

```powershell
.\create-release.ps1 -Version "1.0.1"
```

Les paramètres `-GitHubUser` et `-RepoName` sont optionnels et utilisent par défaut `alex6898` et `Powershell7ISE`.

Ou suivez le guide complet dans [GITHUB_SETUP.md](GITHUB_SETUP.md).

## 📚 Documentation

- [INSTALLATION_INNO_SETUP.md](INSTALLATION_INNO_SETUP.md) - Guide d'installation avec Inno Setup
- [GITHUB_SETUP.md](GITHUB_SETUP.md) - Configuration GitHub et mises à jour automatiques
- [SHAREPOINT_SETUP.md](SHAREPOINT_SETUP.md) - Configuration SharePoint
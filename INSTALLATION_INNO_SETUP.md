# Instructions d'installation avec Inno Setup

Ce document explique comment créer un installateur pour PsConsoleHost en utilisant Inno Setup.

## Prérequis

1. **Inno Setup** (version 6.0 ou supérieure)
   - Télécharger depuis: https://jrsoftware.org/isdl.php
   - Installer Inno Setup sur votre machine

2. **Build Release de l'application**
   - Assurez-vous d'avoir compilé l'application en mode Release:
     ```bash
     dotnet build -c Release
     ```

## Utilisation du script setup.iss

### Option 1: Utilisation directe (téléchargement automatique)

Le script `setup.iss` télécharge automatiquement les prérequis (.NET 8.0 Desktop Runtime et WebView2 Runtime) si nécessaire.

1. Ouvrez Inno Setup Compiler
2. Ouvrez le fichier `setup.iss`
3. Cliquez sur "Compile" (ou appuyez sur F9)
4. L'installateur sera créé dans le dossier `installer\`

### Option 2: Inclusion des prérequis (recommandé pour distribution)

Pour éviter les téléchargements lors de l'installation, vous pouvez inclure les installateurs des prérequis:

1. **Télécharger .NET 8.0 Desktop Runtime:**
   - Visitez: https://dotnet.microsoft.com/download/dotnet/8.0
   - Téléchargez la version appropriée (x64, x86, ou ARM64)
   - Placez le fichier dans le dossier `installer\` avec le nom:
     - `dotnet-desktop-runtime-8.0.0-win-x64.exe` (pour x64)
     - `dotnet-desktop-runtime-8.0.0-win-x86.exe` (pour x86)
     - `dotnet-desktop-runtime-8.0.0-win-arm64.exe` (pour ARM64)

2. **Télécharger WebView2 Runtime:**
   - Visitez: https://go.microsoft.com/fwlink/p/?LinkId=2124703
   - Téléchargez l'installateur
   - Placez le fichier dans le dossier `installer\` avec le nom:
     - `MicrosoftEdgeWebview2Setup.exe`

3. Compilez le script avec Inno Setup

## Fonctionnalités du script

Le script `setup.iss` inclut:

- ✅ **Vérification automatique des prérequis:**
  - .NET 8.0 Desktop Runtime
  - WebView2 Runtime

- ✅ **Installation automatique des prérequis manquants:**
  - Téléchargement automatique si nécessaire
  - Installation silencieuse

- ✅ **Installation de l'application:**
  - Copie de tous les fichiers nécessaires
  - Création des raccourcis (Menu Démarrer, Bureau)
  - Ajout au Panneau de configuration (désinstallation)

- ✅ **Support multilingue:**
  - Français
  - Anglais

- ✅ **Support multi-architecture:**
  - x64 (64-bit)
  - x86 (32-bit)
  - ARM64

## Personnalisation

Vous pouvez personnaliser le script en modifiant les constantes en haut du fichier:

```pascal
#define MyAppName "PsConsoleHost"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "PsConsoleHost"
```

## Structure des dossiers

```
PsConsoleHost/
├── setup.iss                    # Script Inno Setup
├── installer/                   # Dossier de sortie (créé automatiquement)
│   ├── PsConsoleHost-Setup.exe # Installateur final
│   └── [prérequis optionnels]  # .NET et WebView2 installateurs
├── bin/Release/net8.0-windows/ # Fichiers de l'application
└── Resources/app.ico            # Icône de l'application
```

## Notes importantes

- L'installation nécessite des privilèges administrateur
- Les prérequis sont installés en mode silencieux
- Si un redémarrage est requis après l'installation des prérequis, l'utilisateur sera informé
- L'application sera installée dans `C:\Program Files\PsConsoleHost\` (ou `Program Files (x86)` pour x86)

## Dépannage

### Erreur: "Impossible de télécharger .NET 8.0 Desktop Runtime"
- Vérifiez votre connexion Internet
- Téléchargez manuellement le runtime et placez-le dans le dossier `installer\`
- Vérifiez que PowerShell est disponible sur le système

### Erreur: "Impossible de télécharger WebView2 Runtime"
- Vérifiez votre connexion Internet
- Téléchargez manuellement WebView2 Runtime et placez-le dans le dossier `installer\`

### L'installateur ne se compile pas
- Vérifiez que tous les chemins dans le script sont corrects
- Assurez-vous que le build Release existe dans `bin\Release\net8.0-windows\`
- Vérifiez que l'icône `Resources\app.ico` existe


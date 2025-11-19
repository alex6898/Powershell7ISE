# Guide de configuration GitHub et mises à jour automatiques

Ce guide explique comment configurer votre projet sur GitHub et activer les mises à jour automatiques pour vos utilisateurs.

## 📋 Prérequis

1. Un compte GitHub
2. Git installé sur votre machine
3. Le projet compilé en mode Release

## 🚀 Étape 1 : Créer le dépôt GitHub

1. Allez sur [GitHub](https://github.com) et connectez-vous
2. Cliquez sur le bouton **"+"** en haut à droite → **"New repository"**
3. Remplissez les informations :
   - **Repository name** : `PsConsoleHost` (ou le nom de votre choix)
   - **Description** : Description de votre projet
   - **Visibility** : Public ou Private (selon vos préférences)
   - **Ne cochez PAS** "Initialize this repository with a README" (le projet existe déjà)
4. Cliquez sur **"Create repository"**

## 🔧 Étape 2 : Configurer Git et pousser le code

Ouvrez PowerShell dans le dossier de votre projet et exécutez :

```powershell
# Initialiser Git (si pas déjà fait)
git init

# Ajouter tous les fichiers
git add .

# Créer le premier commit
git commit -m "Initial commit - PsConsoleHost avec système de mise à jour automatique"

# Ajouter le dépôt distant (remplacez VOTRE_USERNAME par votre nom d'utilisateur GitHub)
git remote add origin https://github.com/VOTRE_USERNAME/PsConsoleHost.git

# Renommer la branche principale en 'main'
git branch -M main

# Pousser le code vers GitHub
git push -u origin main
```

## ⚙️ Étape 3 : Configurer les URLs dans le code

Vous devez mettre à jour les URLs dans deux fichiers pour qu'elles pointent vers votre dépôt GitHub :

### 1. Mettre à jour `Services/UpdateService.cs`

Ouvrez `Services/UpdateService.cs` et remplacez :
```csharp
private const string UpdateUrl = "https://raw.githubusercontent.com/VOTRE_USERNAME/VOTRE_REPO/main/version.xml";
```

Par :
```csharp
private const string UpdateUrl = "https://raw.githubusercontent.com/VOTRE_USERNAME/PsConsoleHost/main/version.xml";
```

### 2. Mettre à jour `version.xml`

Ouvrez `version.xml` et remplacez toutes les occurrences de :
- `VOTRE_USERNAME` par votre nom d'utilisateur GitHub
- `VOTRE_REPO` par le nom de votre dépôt (probablement `PsConsoleHost`)

Exemple :
```xml
<?xml version="1.0" encoding="UTF-8"?>
<item>
    <version>1.0.0</version>
    <url>https://github.com/VOTRE_USERNAME/PsConsoleHost/releases/download/v1.0.0/PsConsoleHost-Setup.exe</url>
    <changelog>https://github.com/VOTRE_USERNAME/PsConsoleHost/releases/tag/v1.0.0</changelog>
    <mandatory>false</mandatory>
</item>
```

## 📦 Étape 4 : Créer votre première release

### Option A : Manuellement

1. **Compiler l'application** :
   ```powershell
   dotnet build -c Release
   ```

2. **Compiler l'installateur** :
   - Ouvrez `setup.iss` dans Inno Setup Compiler
   - Assurez-vous que `#define MyAppVersion "1.0.0"` correspond à la version dans `version.xml`
   - Compilez (F9)
   - L'installateur sera créé dans `installer/PsConsoleHost-Setup.exe`

3. **Créer la release sur GitHub** :
   - Allez sur votre dépôt GitHub
   - Cliquez sur **"Releases"** → **"Create a new release"**
   - **Tag version** : `v1.0.0` (doit commencer par `v`)
   - **Release title** : `Version 1.0.0`
   - **Description** : Notes de version (optionnel)
   - Glissez-déposez `installer/PsConsoleHost-Setup.exe` dans la zone de fichiers
   - Cliquez sur **"Publish release"**

4. **Pousser `version.xml`** :
   ```powershell
   git add version.xml
   git commit -m "Mise à jour version.xml pour v1.0.0"
   git push
   ```

### Option B : Automatiquement avec GitHub Actions

Le workflow GitHub Actions créera automatiquement une release lorsque vous pousserez un tag :

1. **Mettre à jour la version** :
   - Modifiez `setup.iss` : `#define MyAppVersion "1.0.1"`
   - Modifiez `version.xml` avec la nouvelle version et l'URL correspondante

2. **Créer et pousser un tag** :
   ```powershell
   git add .
   git commit -m "Version 1.0.1"
   git tag v1.0.1
   git push origin main
   git push origin v1.0.1
   ```

3. **GitHub Actions va automatiquement** :
   - Compiler l'application
   - Créer l'installateur
   - Créer une release avec l'installateur

## 🔄 Étape 5 : Publier une nouvelle version

Pour publier une mise à jour :

1. **Mettre à jour la version dans `setup.iss`** :
   ```pascal
   #define MyAppVersion "1.0.1"
   ```

2. **Mettre à jour `version.xml`** :
   ```xml
   <version>1.0.1</version>
   <url>https://github.com/VOTRE_USERNAME/PsConsoleHost/releases/download/v1.0.1/PsConsoleHost-Setup.exe</url>
   <changelog>https://github.com/VOTRE_USERNAME/PsConsoleHost/releases/tag/v1.0.1</changelog>
   ```

3. **Créer un tag et pousser** :
   ```powershell
   git add .
   git commit -m "Version 1.0.1"
   git tag v1.0.1
   git push origin main
   git push origin v1.0.1
   ```

4. **Les utilisateurs recevront une notification** lorsqu'ils ouvriront l'application ou cliqueront sur "🔄 Vérifier les mises à jour"

## 📝 Notes importantes

- ⚠️ **La version dans `version.xml` doit être supérieure** à la version installée pour déclencher une mise à jour
- ⚠️ **Le tag GitHub doit commencer par `v`** (ex: `v1.0.1`)
- ⚠️ **L'URL dans `version.xml` doit correspondre** exactement au tag de la release
- ✅ Les utilisateurs peuvent choisir de reporter la mise à jour
- ✅ Les mises à jour sont téléchargées dans `%LocalAppData%\PsConsoleHost\Updates\`

## 🐛 Dépannage

### L'application ne détecte pas les mises à jour

1. Vérifiez que `version.xml` est accessible publiquement sur GitHub
2. Vérifiez que l'URL dans `UpdateService.cs` est correcte
3. Vérifiez que la version dans `version.xml` est supérieure à la version installée
4. Vérifiez votre connexion Internet

### Erreur lors du téléchargement de la mise à jour

1. Vérifiez que la release GitHub existe et contient le fichier `PsConsoleHost-Setup.exe`
2. Vérifiez que l'URL dans `version.xml` correspond exactement au tag de la release
3. Vérifiez les permissions du dossier `%LocalAppData%\PsConsoleHost\Updates\`

### GitHub Actions ne fonctionne pas

1. Vérifiez que le workflow est activé dans l'onglet "Actions" de votre dépôt
2. Vérifiez que le tag commence par `v`
3. Consultez les logs dans l'onglet "Actions" pour voir les erreurs

## 📚 Ressources

- [Documentation AutoUpdater.NET](https://github.com/ravibpatel/AutoUpdater.NET)
- [Documentation GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github)
- [Documentation GitHub Actions](https://docs.github.com/en/actions)


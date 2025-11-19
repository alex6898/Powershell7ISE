# Script PowerShell pour créer une nouvelle release GitHub
# Usage: .\create-release.ps1 -Version "1.0.1" -GitHubUser "alex6898" -RepoName "Powershell7ISE"

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$GitHubUser = "alex6898",
    
    [Parameter(Mandatory=$false)]
    [string]$RepoName = "Powershell7ISE"
)

Write-Host "🚀 Création de la release v$Version" -ForegroundColor Cyan

# Recharger le PATH pour inclure Git si installé récemment
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")

# Fonction pour trouver et exécuter Git
function Invoke-Git {
    param([string[]]$Arguments)
    
    # Chercher Git dans le PATH d'abord
    $gitCmd = Get-Command git -ErrorAction SilentlyContinue
    if ($gitCmd) {
        & git $Arguments
        return $LASTEXITCODE
    }
    
    # Chercher Git dans les emplacements courants
    $gitPaths = @(
        "C:\Program Files\Git\bin\git.exe",
        "C:\Program Files (x86)\Git\bin\git.exe",
        "$env:LOCALAPPDATA\Programs\Git\bin\git.exe",
        "$env:ProgramFiles\Git\bin\git.exe"
    )
    
    foreach ($path in $gitPaths) {
        if (Test-Path $path) {
            & $path $Arguments
            return $LASTEXITCODE
        }
    }
    
    return -1
}

# Vérifier que Git est disponible
Write-Host "🔍 Vérification de Git..." -ForegroundColor Yellow
$gitTest = Invoke-Git @("--version") 2>&1
if ($LASTEXITCODE -ne 0 -and $gitTest -match "not recognized") {
    Write-Host "⚠️  Git n'est pas trouvé dans le PATH" -ForegroundColor Yellow
    Write-Host "   Redémarrez PowerShell après l'installation de Git pour que le PATH soit mis à jour" -ForegroundColor Yellow
    Write-Host "   Ou le script essaiera de trouver Git automatiquement..." -ForegroundColor Yellow
    Write-Host ""
}

# Vérifier que nous sommes dans un dépôt Git
$skipGit = $false
if (-not (Test-Path ".git")) {
    Write-Host "⚠️  Ce dossier n'est pas un dépôt Git" -ForegroundColor Yellow
    Write-Host "   Le script va initialiser Git automatiquement" -ForegroundColor Yellow
    Write-Host ""
}

# Étape 1: Mettre à jour setup.iss
Write-Host "📝 Mise à jour de setup.iss..." -ForegroundColor Yellow
$setupIss = Get-Content "setup.iss" -Raw
$setupIss = $setupIss -replace '(?m)^#define MyAppVersion ".*"', "#define MyAppVersion `"$Version`""
Set-Content -Path "setup.iss" -Value $setupIss -NoNewline

# Étape 1b: Mettre à jour PsConsoleHost.csproj
Write-Host "📝 Mise à jour de PsConsoleHost.csproj..." -ForegroundColor Yellow
$csproj = Get-Content "PsConsoleHost.csproj" -Raw
# Remplacer Version
if ($csproj -match '<Version>.*?</Version>') {
    $csproj = $csproj -replace '<Version>.*?</Version>', "<Version>$Version</Version>"
} else {
    # Si la balise n'existe pas, l'ajouter après ApplicationIcon
    $csproj = $csproj -replace '(<ApplicationIcon>.*?</ApplicationIcon>)', "`$1`n    <Version>$Version</Version>"
}
# Remplacer AssemblyVersion
if ($csproj -match '<AssemblyVersion>.*?</AssemblyVersion>') {
    $csproj = $csproj -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
} else {
    $csproj = $csproj -replace '(<Version>.*?</Version>)', "`$1`n    <AssemblyVersion>$Version.0</AssemblyVersion>"
}
# Remplacer FileVersion
if ($csproj -match '<FileVersion>.*?</FileVersion>') {
    $csproj = $csproj -replace '<FileVersion>.*?</FileVersion>', "<FileVersion>$Version.0</FileVersion>"
} else {
    $csproj = $csproj -replace '(<AssemblyVersion>.*?</AssemblyVersion>)', "`$1`n    <FileVersion>$Version.0</FileVersion>"
}
Set-Content -Path "PsConsoleHost.csproj" -Value $csproj -NoNewline

# Étape 2: Mettre à jour version.xml
Write-Host "📝 Mise à jour de version.xml..." -ForegroundColor Yellow
$versionXml = @"
<?xml version="1.0" encoding="UTF-8"?>
<item>
    <version>$Version</version>
    <url>https://github.com/$GitHubUser/$RepoName/releases/download/v$Version/Powershell7ISE-Setup.exe</url>
    <changelog>https://github.com/$GitHubUser/$RepoName/releases/tag/v$Version</changelog>
    <mandatory>false</mandatory>
</item>
"@
Set-Content -Path "version.xml" -Value $versionXml

# Étape 3: Restaurer les packages NuGet
Write-Host "📦 Restauration des packages NuGet..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Erreur lors de la restauration des packages" -ForegroundColor Red
    exit 1
}

# Étape 4: Compiler l'application
Write-Host "🔨 Compilation de l'application en mode Release..." -ForegroundColor Yellow
dotnet build -c Release --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Erreur lors de la compilation" -ForegroundColor Red
    exit 1
}

# Étape 5: Vérifier si Inno Setup est installé
$innoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $innoSetupPath)) {
    Write-Host "⚠️  Inno Setup n'est pas trouvé à $innoSetupPath" -ForegroundColor Yellow
    Write-Host "   Veuillez compiler l'installateur manuellement avec Inno Setup Compiler" -ForegroundColor Yellow
    Write-Host "   Fichier: setup.iss" -ForegroundColor Yellow
} else {
    Write-Host "📦 Compilation de l'installateur avec Inno Setup..." -ForegroundColor Yellow
    & $innoSetupPath "setup.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Erreur lors de la compilation de l'installateur" -ForegroundColor Red
        exit 1
    }
}

# Étape 6: Vérifier que l'installateur existe
$installerPath = "installer\Powershell7ISE-Setup.exe"
if (-not (Test-Path $installerPath)) {
    Write-Host "❌ L'installateur n'a pas été créé: $installerPath" -ForegroundColor Red
    exit 1
}

# Étape 7: Initialiser Git si nécessaire et créer le commit/tag
Write-Host "📤 Gestion Git..." -ForegroundColor Yellow

# Initialiser Git si nécessaire
if (-not (Test-Path ".git")) {
    Write-Host "   Initialisation du dépôt Git..." -ForegroundColor Gray
    $initResult = Invoke-Git @("init")
    if ($initResult -ne 0) {
        Write-Host "⚠️  Impossible d'initialiser Git. Continuons sans Git..." -ForegroundColor Yellow
        $skipGit = $true
    } else {
        # Configurer le remote si pas déjà configuré
        $remoteCheck = Invoke-Git @("remote", "get-url", "origin") 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "   Configuration du remote GitHub..." -ForegroundColor Gray
            Invoke-Git @("remote", "add", "origin", "https://github.com/$GitHubUser/$RepoName.git") | Out-Null
        }
    }
}

if (-not $skipGit) {
    Write-Host "   Création du commit et du tag..." -ForegroundColor Gray
    
    # Ajouter tous les fichiers si c'est le premier commit
    $commitCount = Invoke-Git @("rev-list", "--count", "HEAD") 2>&1
    $isFirstCommit = ($LASTEXITCODE -ne 0) -or ($commitCount -match "0" -or [string]::IsNullOrWhiteSpace($commitCount))
    
    if ($isFirstCommit) {
        Write-Host "   Premier commit - ajout de tous les fichiers..." -ForegroundColor Gray
        $addResult = Invoke-Git @("add", ".")
    } else {
        $addResult = Invoke-Git @("add", "setup.iss", "version.xml", "PsConsoleHost.csproj")
    }
    
    if ($addResult -eq 0) {
        $commitResult = Invoke-Git @("commit", "-m", "Version $Version")
        if ($commitResult -eq 0) {
            Write-Host "   Commit créé avec succès" -ForegroundColor Gray
        } else {
            # Si le commit échoue (aucun changement), on continue quand même pour créer le tag
            Write-Host "⚠️  Aucun changement à commiter (fichiers déjà à jour?)" -ForegroundColor Yellow
        }
        
        # Créer le tag (même si le commit n'a pas changé)
        $tagResult = Invoke-Git @("tag", "-f", "v$Version")
        if ($tagResult -ne 0) {
            # Essayer sans -f si le tag n'existe pas
            $tagResult = Invoke-Git @("tag", "v$Version")
            if ($tagResult -ne 0) {
                Write-Host "⚠️  Erreur lors de la création du tag" -ForegroundColor Yellow
            } else {
                Write-Host "   Tag v$Version créé" -ForegroundColor Gray
            }
        } else {
            Write-Host "   Tag v$Version créé/mis à jour" -ForegroundColor Gray
        }
    } else {
        Write-Host "⚠️  Erreur lors de l'ajout des fichiers" -ForegroundColor Yellow
    }
}

# Étape 8: Résumé et instructions
Write-Host ""
Write-Host "✅ Préparation terminée!" -ForegroundColor Green
Write-Host "   Version: $Version" -ForegroundColor White
Write-Host "   Installateur: $installerPath" -ForegroundColor White
Write-Host ""

if ($skipGit) {
    Write-Host "📋 Prochaines étapes manuelles:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Installer Git (si pas déjà fait):" -ForegroundColor Yellow
    Write-Host "   https://git-scm.com/download/win" -ForegroundColor White
    Write-Host ""
    Write-Host "2. Initialiser Git et pousser vers GitHub:" -ForegroundColor Yellow
    Write-Host "   git init" -ForegroundColor White
    Write-Host "   git add ." -ForegroundColor White
    Write-Host "   git commit -m `"Version $Version`"" -ForegroundColor White
    Write-Host "   git remote add origin https://github.com/$GitHubUser/$RepoName.git" -ForegroundColor White
    Write-Host "   git branch -M main" -ForegroundColor White
    Write-Host "   git push -u origin main" -ForegroundColor White
    Write-Host "   git tag v$Version" -ForegroundColor White
    Write-Host "   git push origin v$Version" -ForegroundColor White
    Write-Host ""
    Write-Host "3. Créer la release sur GitHub:" -ForegroundColor Yellow
    Write-Host "   - Allez sur https://github.com/$GitHubUser/$RepoName/releases/new" -ForegroundColor White
    Write-Host "   - Tag: v$Version" -ForegroundColor White
    Write-Host "   - Title: Version $Version" -ForegroundColor White
    Write-Host "   - Upload: $installerPath" -ForegroundColor White
    Write-Host ""
} else {
    $confirm = Read-Host "Voulez-vous pousser vers GitHub maintenant? (O/N)"
    if ($confirm -eq "O" -or $confirm -eq "o" -or $confirm -eq "Y" -or $confirm -eq "y") {
        Write-Host "📤 Push vers GitHub..." -ForegroundColor Yellow
        
        # Vérifier la branche actuelle
        $branchOutput = Invoke-Git @("branch", "--show-current") 2>&1
        $currentBranch = if ($LASTEXITCODE -eq 0 -and $branchOutput -is [string] -and $branchOutput.Trim()) { 
            $branchOutput.Trim() 
        } else { 
            # Essayer de détecter la branche par défaut
            $defaultBranch = Invoke-Git @("symbolic-ref", "--short", "HEAD") 2>&1
            if ($LASTEXITCODE -eq 0 -and $defaultBranch -is [string]) {
                $defaultBranch.Trim()
            } else {
                "main"
            }
        }
        
        # Renommer en main si nécessaire
        if ($currentBranch -ne "main" -and $currentBranch -ne "master") {
            Write-Host "   Renommage de la branche en 'main'..." -ForegroundColor Gray
            Invoke-Git @("branch", "-M", "main") | Out-Null
            $currentBranch = "main"
        }
        
        # Push de la branche
        Write-Host "   Push de la branche $currentBranch..." -ForegroundColor Gray
        $pushBranch = Invoke-Git @("push", "-u", "origin", $currentBranch)
        
        # Push du tag (forcer si nécessaire)
        Write-Host "   Push du tag v$Version..." -ForegroundColor Gray
        $pushTag = Invoke-Git @("push", "origin", "v$Version")
        
        # Si le tag existe déjà, forcer le push
        if ($pushTag -ne 0) {
            Write-Host "   Le tag existe déjà, mise à jour forcée..." -ForegroundColor Gray
            $pushTag = Invoke-Git @("push", "--force", "origin", "v$Version")
        }
        
        if ($pushBranch -eq 0 -or $pushTag -eq 0) {
            Write-Host "✅ Code poussé avec succès!" -ForegroundColor Green
            Write-Host ""
            Write-Host "📦 Créez maintenant la release sur GitHub:" -ForegroundColor Cyan
            Write-Host "   - Allez sur https://github.com/$GitHubUser/$RepoName/releases/new" -ForegroundColor White
            Write-Host "   - Tag: v$Version (sélectionnez-le dans la liste)" -ForegroundColor White
            Write-Host "   - Title: Version $Version" -ForegroundColor White
            Write-Host "   - Upload: $installerPath" -ForegroundColor White
            Write-Host "   - Publish release" -ForegroundColor White
        } else {
            Write-Host "⚠️  Erreur lors du push. Vérifiez votre authentification GitHub." -ForegroundColor Yellow
            Write-Host "   Vous pouvez pousser manuellement avec:" -ForegroundColor Yellow
            Write-Host "   git push -u origin $currentBranch" -ForegroundColor White
            Write-Host "   git push origin v$Version" -ForegroundColor White
        }
    } else {
        Write-Host "⏸️  Push annulé. Vous pouvez le faire manuellement avec:" -ForegroundColor Yellow
        Write-Host "   git push -u origin main" -ForegroundColor White
        Write-Host "   git push origin v$Version" -ForegroundColor White
    }
}


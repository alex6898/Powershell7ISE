# Utilisation SharePoint dans PsConsoleHost

Ce guide explique comment utiliser l'intégration SharePoint pour récupérer des scripts depuis SharePoint Online.

## Utilisation simple (recommandé)

**Aucune configuration n'est nécessaire !** L'application utilise un Client ID public par défaut qui permet à n'importe quel utilisateur de se connecter avec son compte Microsoft.

### Étapes d'utilisation :

1. Lancez l'application PsConsoleHost
2. Cliquez sur le bouton **☁️ Ouvrir SharePoint**
3. Cliquez sur **🔐 Se connecter à SharePoint**
4. Connectez-vous avec votre compte Microsoft 365
5. Sélectionnez un site SharePoint dans la liste de gauche
6. Les scripts PowerShell (.ps1) du site s'affichent dans la liste de droite
7. Sélectionnez un script et cliquez sur **Ouvrir**

Le script sera chargé dans l'éditeur et vous pourrez l'exécuter normalement.

## Configuration optionnelle (Client ID personnalisé)

Si vous souhaitez utiliser votre propre Client ID Azure AD (par exemple pour une organisation spécifique) :

1. Dans la fenêtre SharePoint, cliquez sur **⚙️ Paramètres**
2. Entrez votre Client ID Azure AD (optionnel)
3. Cliquez sur **Enregistrer**

**Note** : Si vous configurez un Client ID personnalisé, vous devrez créer une application dans Azure AD avec les permissions appropriées.

### Créer votre propre application Azure AD (optionnel)

1. Connectez-vous au [Portail Azure](https://portal.azure.com)
2. Allez dans **Azure Active Directory** > **App registrations** > **New registration**
3. Configurez l'application :
   - **Name** : `PsConsoleHost SharePoint Integration`
   - **Supported account types** : `Accounts in any organizational directory and personal Microsoft accounts`
   - **Redirect URI** : 
     - Type : `Public client/native (mobile & desktop)`
     - URI : `http://localhost`
4. Cliquez sur **Register**
5. Allez dans **API permissions** et ajoutez :
   - `Files.Read.All` - Lire tous les fichiers
   - `Sites.Read.All` - Lire tous les sites
   - `User.Read` - Lire le profil utilisateur
6. Copiez le **Application (client) ID** et utilisez-le dans les paramètres

## Utilisation

1. Lancez l'application PsConsoleHost
2. Cliquez sur le bouton **☁️ Ouvrir SharePoint**
3. Cliquez sur **🔐 Se connecter à SharePoint**
4. Connectez-vous avec votre compte Microsoft 365
5. Sélectionnez un site SharePoint dans la liste de gauche
6. Les scripts PowerShell (.ps1) du site s'affichent dans la liste de droite
7. Sélectionnez un script et cliquez sur **Ouvrir**

Le script sera chargé dans l'éditeur et vous pourrez l'exécuter normalement.

## Notes importantes

- **Sécurité** : Le Client ID peut être partagé publiquement, mais ne partagez jamais les secrets d'application
- **Permissions** : Les utilisateurs devront accepter les permissions lors de la première connexion
- **Limitations** : Cette implémentation utilise Microsoft Graph API et fonctionne uniquement avec SharePoint Online (pas SharePoint Server on-premises)

## Dépannage

### Erreur "AADSTS70011: The provided value for the input parameter 'scope' is not valid"

Vérifiez que les permissions sont correctement configurées dans Azure AD et que vous avez accordé le consentement administrateur.

### Erreur "No sites found"

Assurez-vous que :
- Votre compte a accès à au moins un site SharePoint
- Les permissions `Sites.Read.All` sont accordées et consenties

### Erreur "No scripts found"

Vérifiez que :
- Le site SharePoint contient des fichiers avec l'extension `.ps1`
- Votre compte a les permissions de lecture sur ces fichiers


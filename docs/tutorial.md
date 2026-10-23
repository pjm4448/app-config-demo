# Tutorial: Azure App Configuration, Azure Key Vault, and a C# Console Application

## Overview

This tutorial demonstrates how to:

1. Create an **Azure App Configuration** store in the Azure portal.
2. Grant the developer the required App Configuration data-plane permissions.
3. Create an **Azure Key Vault** and grant the developer permission to manage secrets.
4. Add ordinary values, hierarchical values, and a Key Vault reference to App Configuration.
5. Automate and verify the configuration with PowerShell and Azure CLI.
6. Create a C# console application that retrieves the configuration and resolves the Key Vault reference.
7. Distinguish between **developer/setup permissions** and **application runtime permissions**.
8. Troubleshoot RBAC, PowerShell variable, corporate VPN, proxy, and certificate-trust issues.

> [!IMPORTANT]
> Azure App Configuration stores a Key Vault reference containing the secret URI. The application recognises that reference and retrieves the secret from Key Vault. The application identity therefore needs permission to read both App Configuration and Key Vault.

---

## Solution architecture

```text
Azure Resource Group
|
|-- Azure App Configuration
|   |-- DemoApp:ApplicationName = App Configuration Demo
|   |-- DemoApp:Environment     = Development
|   |-- DemoApp:Database:Server = sql-demo.database.windows.net
|   |-- DemoApp:Database:Name   = DemoDb
|   `-- DemoApp:ApiKey
|       `-- Key Vault reference
|           `-- Azure Key Vault
|               `-- DemoApiKey = secret value
|
`-- C# Console Application
    |-- DefaultAzureCredential
    |-- Azure App Configuration provider
    `-- IConfiguration
```

The examples use colon-separated key names such as `DemoApp:Database:Server`. .NET interprets these as hierarchical configuration keys.

---

## Permissions used in this tutorial

It is important to distinguish between permissions required to **build the tutorial resources** and those required by an application that only **reads** them.

| Resource | Developer/setup identity | Runtime application identity |
|---|---|---|
| Azure App Configuration | **App Configuration Data Owner** | **App Configuration Data Reader** |
| Azure Key Vault | **Key Vault Secrets Officer** | **Key Vault Secrets User** |

During local development, the console application uses the signed-in developer identity through `DefaultAzureCredential`. Because the developer identity already has the broader setup roles above, the corresponding read-only roles do not also need to be assigned to the same identity for this tutorial.

For production, assign only the read permissions required by the application's managed identity.

---

## 1. Prerequisites

You need:

- an Azure subscription;
- permission to create Azure resources and role assignments;
- access to the [Azure portal](https://portal.azure.com);
- Azure CLI;
- PowerShell;
- the .NET SDK;
- Visual Studio, Visual Studio Code, or another suitable editor.

Check the installed .NET SDK:

```powershell
dotnet --version
```

Check Azure CLI:

```powershell
az version
```

Sign in:

```powershell
az login
```

The C# application will use `DefaultAzureCredential`, avoiding application credentials or App Configuration access keys in source code.

### Corporate network note

If your organisation uses a corporate VPN, proxy, or HTTPS inspection, connect to the approved corporate network or VPN before running Azure CLI data-plane commands.

A corporate proxy or HTTPS inspection service can cause Azure CLI errors such as:

```text
CERTIFICATE_VERIFY_FAILED
unable to get local issuer certificate
Request timeout
```

If connecting to the approved corporate VPN resolves the issue, keep the VPN connected while following this tutorial. If it does not, follow your organisation's approved certificate-trust process. Do not disable TLS certificate validation.

---

## 2. Create Azure App Configuration

1. Open the [Azure portal](https://portal.azure.com).
2. Search for **App Configuration**.
3. Select **Create**.
4. Enter values similar to the following:

| Setting | Example |
|---|---|
| Subscription | Your Azure subscription |
| Resource group | `rg-appconfig-demo` |
| Resource name | `appcs-demo-<unique-suffix>` |
| Location | A suitable Azure region |
| Pricing tier | A suitable tier for the tutorial |

5. Select **Review + create**.
6. Select **Create** after validation succeeds.
7. Open the deployed App Configuration resource.
8. Record its endpoint, for example:

```text
https://appcs-demo-pjm.azconfig.io
```

---

## 3. Grant the developer access to App Configuration data

Creating or managing the App Configuration resource and creating key-values inside the store are separately authorised operations. Before using **Configuration explorer**, grant the developer identity permission to work with the store's data.

1. Open the **App Configuration** resource.
2. Select **Access control (IAM)**.
3. Select **Add > Add role assignment**.
4. Select **App Configuration Data Owner**.
5. Select **Next**.
6. Leave **User, group, or service principal** selected.
7. Select **Select members**.
8. Select your Azure developer account.
9. Select **Review + assign**.
10. Select **Review + assign** again.

**App Configuration Data Owner** provides read, write, and delete access to App Configuration data. This is required because the tutorial creates and modifies key-values.

> [!NOTE]
> Azure role assignments can take time to become effective. If Configuration explorer immediately reports that you do not have access using Microsoft Entra ID or access keys, wait for the role assignment to propagate and retry.

---

## 4. Create Azure Key Vault

1. In the Azure portal, select **Create a resource**.
2. Search for **Key Vault**.
3. Select **Create**.
4. Enter values similar to the following:

| Setting | Example |
|---|---|
| Subscription | Your Azure subscription |
| Resource group | `rg-appconfig-demo` |
| Key vault name | `kv-appconfig-demo-<unique-suffix>` |
| Region | A suitable Azure region |
| Permission model | Azure role-based access control |

5. Select **Review + create**.
6. Select **Create** after validation succeeds.

---

## 5. Grant the developer permission to manage Key Vault secrets

Creating the Key Vault resource does not by itself grant the developer identity permission to create secrets in a vault that uses the Azure RBAC permission model.

1. Open the **Key Vault**.
2. Select **Access control (IAM)**.
3. Select **Add > Add role assignment**.
4. Select **Key Vault Secrets Officer**.
5. Select **Next**.
6. Leave **User, group, or service principal** selected.
7. Select **Select members**.
8. Select your Azure developer account.
9. Select **Review + assign**.
10. Select **Review + assign** again.

**Key Vault Secrets Officer** allows the developer to manage secrets for this exercise.

> [!NOTE]
> If secret creation reports `The operation is not allowed by RBAC`, allow time for the role assignment to propagate and then retry.

---

## 6. Add a secret to Key Vault

1. Open the Key Vault.
2. Go to **Objects > Secrets**.
3. Select **Generate/Import**.
4. Enter:

```text
Name: DemoApiKey
Value: SuperSecretValue123!
```

5. Select **Create**.

> [!WARNING]
> This value is deliberately simple for the tutorial. Never commit genuine secrets to a source repository or hard-code them in production scripts.

---

## 7. Add ordinary App Configuration values

Open the App Configuration resource and go to:

**Configuration management > Configuration explorer**

Create the following key-values.

### Application name

```text
Key:   DemoApp:ApplicationName
Value: App Configuration Demo
```

### Environment

```text
Key:   DemoApp:Environment
Value: Development
```

These values are stored directly in App Configuration because they are not secrets.

---

## 8. Add hierarchical configuration values

Add:

```text
Key:   DemoApp:Database:Server
Value: sql-demo.database.windows.net
```

and:

```text
Key:   DemoApp:Database:Name
Value: DemoDb
```

The logical .NET structure is:

```text
DemoApp
|-- ApplicationName
|-- Environment
`-- Database
    |-- Server
    `-- Name
```

Each entry remains an individual key-value in Azure App Configuration. The hierarchy comes from the colon-separated names interpreted by the .NET configuration system.

---

## 9. Add the Key Vault reference

1. In **Configuration explorer**, select the option to create a **Key vault reference**.
2. Enter the key:

```text
DemoApp:ApiKey
```

3. Select the Key Vault created earlier.
4. Select the `DemoApiKey` secret.
5. Save the entry.

The store should now contain:

| Key | Type |
|---|---|
| `DemoApp:ApplicationName` | Standard key-value |
| `DemoApp:Environment` | Standard key-value |
| `DemoApp:Database:Server` | Standard key-value |
| `DemoApp:Database:Name` | Standard key-value |
| `DemoApp:ApiKey` | Key Vault reference |

The secret is not copied into App Configuration. The App Configuration entry references the Key Vault secret, and the application resolves the reference.

---

## 10. Automate the configuration with PowerShell and Azure CLI

The same data can be created from PowerShell by invoking Azure CLI commands. The identity running these commands needs the data-plane roles assigned in Steps 3 and 5.

If required by your organisation, connect to the corporate VPN before running these commands.

Define the resource names in the current PowerShell session:

```powershell
$appConfigName = "appcs-demo-pjm"
$keyVaultName  = "kv-appconfig-demo-pjm"
```

Confirm they are populated:

```powershell
Write-Host "App Configuration store: $appConfigName"
Write-Host "Key Vault: $keyVaultName"
```

Create the configuration:

```powershell
# Create standard App Configuration values.
az appconfig kv set `
    --name $appConfigName `
    --auth-mode login `
    --key "DemoApp:ApplicationName" `
    --value "App Configuration Demo" `
    --yes

az appconfig kv set `
    --name $appConfigName `
    --auth-mode login `
    --key "DemoApp:Environment" `
    --value "Development" `
    --yes

# Create hierarchical configuration values.
az appconfig kv set `
    --name $appConfigName `
    --auth-mode login `
    --key "DemoApp:Database:Server" `
    --value "sql-demo.database.windows.net" `
    --yes

az appconfig kv set `
    --name $appConfigName `
    --auth-mode login `
    --key "DemoApp:Database:Name" `
    --value "DemoDb" `
    --yes

# Create or update the Key Vault secret and capture its identifier.
$secretUri = az keyvault secret set `
    --vault-name $keyVaultName `
    --name "DemoApiKey" `
    --value "SuperSecretValue123!" `
    --query id `
    --output tsv

if ([string]::IsNullOrWhiteSpace($secretUri)) {
    throw "The Key Vault secret URI could not be retrieved."
}

# Add the Key Vault reference to App Configuration.
az appconfig kv set-keyvault `
    --name $appConfigName `
    --auth-mode login `
    --key "DemoApp:ApiKey" `
    --secret-identifier $secretUri `
    --yes
```

### Add environment labels

Keep the unlabeled values above as the baseline configuration. Then create a complete
set of values for each environment label. Labels are case-sensitive.

```powershell
# This step can run in a new PowerShell session, so retrieve the existing secret URI.
$secretUri = az keyvault secret show `
    --vault-name $keyVaultName `
    --name "DemoApiKey" `
    --query id `
    --output tsv

if ([string]::IsNullOrWhiteSpace($secretUri)) {
    throw "The Key Vault secret URI could not be retrieved."
}

function Set-DemoAppConfigurationLabel {
    param(
        [Parameter(Mandatory)]
        [string]$Label,

        [Parameter(Mandatory)]
        [string]$DatabaseServer,

        [Parameter(Mandatory)]
        [string]$DatabaseName
    )

    az appconfig kv set `
        --name $appConfigName `
        --auth-mode login `
        --key "DemoApp:ApplicationName" `
        --label $Label `
        --value "App Configuration Demo" `
        --yes

    az appconfig kv set `
        --name $appConfigName `
        --auth-mode login `
        --key "DemoApp:Environment" `
        --label $Label `
        --value $Label `
        --yes

    az appconfig kv set `
        --name $appConfigName `
        --auth-mode login `
        --key "DemoApp:Database:Server" `
        --label $Label `
        --value $DatabaseServer `
        --yes

    az appconfig kv set `
        --name $appConfigName `
        --auth-mode login `
        --key "DemoApp:Database:Name" `
        --label $Label `
        --value $DatabaseName `
        --yes

    az appconfig kv set-keyvault `
        --name $appConfigName `
        --auth-mode login `
        --key "DemoApp:ApiKey" `
        --label $Label `
        --secret-identifier $secretUri `
        --yes
}

Set-DemoAppConfigurationLabel `
    -Label "Development" `
    -DatabaseServer "sql-demo-dev.database.windows.net" `
    -DatabaseName "DemoDbDevelopment"

Set-DemoAppConfigurationLabel `
    -Label "Test" `
    -DatabaseServer "sql-demo-test.database.windows.net" `
    -DatabaseName "DemoDbTest"

Set-DemoAppConfigurationLabel `
    -Label "Production" `
    -DatabaseServer "sql-demo-prod.database.windows.net" `
    -DatabaseName "DemoDbProduction"
```

Each label has its own version of every `DemoApp` key. The same Key Vault secret is
referenced for all three labels solely to keep this exercise self-contained; use
environment-specific secrets for a real deployment.

> [!CAUTION]
> The secret is hard-coded only to keep the tutorial self-contained. Supply secrets securely at runtime in real automation.

### Verify the App Configuration entries

Run the verification command in the same PowerShell session in which `$appConfigName` was defined:

```powershell
$appConfigName = "appcs-demo-pjm"

az appconfig kv list `
    --name $appConfigName `
    --auth-mode login `
    --output table
```

Verify the values created under the `Production` label:

```powershell
az appconfig kv list `
    --name $appConfigName `
    --auth-mode login `
    --label "Production" `
    --output table
```

Expected keys for each label include:

```text
DemoApp:ApiKey
DemoApp:ApplicationName
DemoApp:Database:Name
DemoApp:Database:Server
DemoApp:Environment
```

The Key Vault reference is returned as a JSON object containing a secret URI. The actual secret value is not stored in App Configuration.

---

## 11. Understand the local runtime permissions

The console application in this tutorial uses the developer identity through `DefaultAzureCredential`.

Because Steps 3 and 5 already give that identity:

- **App Configuration Data Owner**; and
- **Key Vault Secrets Officer**,

there is no need to add **App Configuration Data Reader** or **Key Vault Secrets User** to the same developer account merely for this exercise.

For a production application, do not copy these broader setup permissions. Give its managed identity the narrower runtime roles described in Step 20.

---

## 12. Create the C# console application

Create the project:

```powershell
mkdir AppConfigurationDemo
cd AppConfigurationDemo
dotnet new console
```

---

## 13. Install the NuGet packages

Install the App Configuration provider:

```powershell
dotnet add package Microsoft.Extensions.Configuration.AzureAppConfiguration
```

Install Azure Identity:

```powershell
dotnet add package Azure.Identity
```

Restore packages:

```powershell
dotnet restore
```

---

## 14. Set the App Configuration endpoint

Set an environment variable in the PowerShell session from which the application will run:

```powershell
$env:AppConfigurationEndpoint = "https://appcs-demo-pjm.azconfig.io"
```

Replace the value with the actual App Configuration endpoint.

---

## 15. Authenticate locally

If required by your organisation, connect to the corporate VPN first.

Sign in using the Azure developer account to which the roles were assigned:

```powershell
az login
```

Review the active Azure context:

```powershell
az account show
```

If required, select the intended subscription:

```powershell
az account set --subscription "<subscription-id-or-name>"
```

---

## 16. Implement the console application

Replace `Program.cs` with:

```csharp
using Azure.Identity;
using Microsoft.Extensions.Configuration;

string? endpoint = Environment.GetEnvironmentVariable(
    "AppConfigurationEndpoint");

if (string.IsNullOrWhiteSpace(endpoint))
{
    Console.Error.WriteLine(
        "The AppConfigurationEndpoint environment variable is not defined.");

    return;
}

var credential = new DefaultAzureCredential();

var configuration = new ConfigurationBuilder()
    .AddAzureAppConfiguration(options =>
    {
        options
            .Connect(new Uri(endpoint), credential)
            .ConfigureKeyVault(keyVaultOptions =>
            {
                keyVaultOptions.SetCredential(credential);
            });
    })
    .Build();

Console.WriteLine("Azure App Configuration Demo");
Console.WriteLine("----------------------------");
Console.WriteLine(
    $"Application Name : {configuration["DemoApp:ApplicationName"]}");
Console.WriteLine(
    $"Environment      : {configuration["DemoApp:Environment"]}");
Console.WriteLine(
    $"Database Server  : {configuration["DemoApp:Database:Server"]}");
Console.WriteLine(
    $"Database Name    : {configuration["DemoApp:Database:Name"]}");
Console.WriteLine(
    $"API Key          : {configuration["DemoApp:ApiKey"]}");
```

The same `DefaultAzureCredential` is supplied to App Configuration and to the Key Vault reference resolver.

---

## 17. Run the application

Run:

```powershell
dotnet run
```

For this tutorial, expected output is:

```text
Azure App Configuration Demo
----------------------------
Application Name : App Configuration Demo
Environment      : Development
Database Server  : sql-demo.database.windows.net
Database Name    : DemoDb
API Key          : SuperSecretValue123!
```

> [!WARNING]
> Printing a secret is done only to demonstrate that the reference was resolved. Production applications should not write secrets to console output, logs, telemetry, or exception messages.

---

## 18. Optional: bind hierarchical values to a C# class

Create:

```csharp
public sealed class DatabaseSettings
{
    public string? Server { get; set; }
    public string? Name { get; set; }
}
```

Install the binder if needed:

```powershell
dotnet add package Microsoft.Extensions.Configuration.Binder
```

Bind the section:

```csharp
DatabaseSettings databaseSettings =
    configuration
        .GetSection("DemoApp:Database")
        .Get<DatabaseSettings>()
    ?? new DatabaseSettings();

Console.WriteLine($"Database Server: {databaseSettings.Server}");
Console.WriteLine($"Database Name:   {databaseSettings.Name}");
```

This maps `DemoApp:Database:Server` and `DemoApp:Database:Name` onto properties of `DatabaseSettings`.

---

## 19. Decide what belongs in each service

| Example | Recommended store |
|---|---|
| Application name | Azure App Configuration |
| Environment name | Azure App Configuration |
| API base URL | Azure App Configuration |
| Retry count | Azure App Configuration |
| Feature settings | Azure App Configuration |
| Database server name | Azure App Configuration |
| Database password | Azure Key Vault |
| API key | Azure Key Vault |
| Client secret | Azure Key Vault |

A useful rule is:

```text
Azure App Configuration:
How should the application behave?

Azure Key Vault:
What sensitive values does the application require?
```

---

## 20. Production deployment with managed identity

For an Azure-hosted application, enable a managed identity and grant that identity only the runtime access it needs:

- **App Configuration Data Reader** on the App Configuration store;
- **Key Vault Secrets User** on the Key Vault.

Continue using:

```csharp
new DefaultAzureCredential()
```

This keeps the production application's permissions narrower than the developer's setup permissions.

```text
Azure-hosted application
|
`-- Managed identity
    |-- App Configuration Data Reader
    |   `-- Azure App Configuration
    |
    `-- Key Vault Secrets User
        `-- Azure Key Vault
```

For production, separately review network access, private endpoints, firewall rules, secret rotation, diagnostic logging, resiliency, and configuration refresh requirements.

---

## 21. Troubleshooting

### Configuration explorer says you do not have access

If you see a message similar to:

```text
You don't have access to the configuration store data using
Microsoft Entra ID or Access Keys.
```

Verify that the user creating the tutorial values has **App Configuration Data Owner** on the App Configuration resource. A read-only role is not sufficient to create or modify key-values.

If the role was just assigned, allow time for the assignment to propagate and retry.

### Key Vault reports that the operation is not allowed by RBAC

Verify that the user creating the tutorial secret has **Key Vault Secrets Officer** on the vault. If the role was recently assigned, allow time for it to propagate and retry.

### Azure CLI says `argument --name/-n: expected one argument`

The PowerShell variable supplied to `--name` is empty or was defined in another PowerShell session.

Define and verify it before running the command:

```powershell
$appConfigName = "appcs-demo-pjm"
Write-Host $appConfigName
```

Then retry:

```powershell
az appconfig kv list `
    --name $appConfigName `
    --auth-mode login `
    --output table
```

### Azure CLI reports `Request timeout`

First confirm that the resource can be retrieved through Azure Resource Manager:

```powershell
az appconfig show `
    --name $appConfigName `
    --output table
```

Obtain and test the endpoint:

```powershell
$endpoint = az appconfig show `
    --name $appConfigName `
    --query endpoint `
    --output tsv

$uri = [System.Uri]$endpoint

Test-NetConnection `
    -ComputerName $uri.Host `
    -Port 443
```

If the organisation requires a corporate VPN, connect to it and retry the Azure CLI command.

### Azure CLI reports `CERTIFICATE_VERIFY_FAILED`

A debug trace may show:

```text
SSLCertVerificationError
certificate verify failed: unable to get local issuer certificate
```

This indicates that Azure CLI's Python TLS stack cannot establish trust in the presented certificate chain. In a corporate environment this can be caused by HTTPS inspection or a proxy.

Use the organisation's approved remedy:

1. Connect to the corporate VPN if required.
2. If necessary, obtain the approved corporate/proxy CA certificate from the organisation's IT or security team.
3. Configure Azure CLI to use an approved PEM CA bundle through `REQUESTS_CA_BUNDLE`.
4. Do not disable TLS certificate verification.

Example for the current PowerShell session, using a CA bundle supplied or approved by the organisation:

```powershell
$env:REQUESTS_CA_BUNDLE = `
    "C:\path\to\approved\azure-cli-ca-bundle.pem"
```

Test HTTPS without sending a bearer token manually:

```powershell
az rest `
    --method get `
    --url "https://appcs-demo-pjm.azconfig.io/kv?api-version=1.0" `
    --resource "https://appcs-demo-pjm.azconfig.io"
```

> [!WARNING]
> Do not paste access tokens into documentation, chat, source control, tickets, or logs. Treat bearer tokens as credentials.

### `403 Forbidden` when the production application reads App Configuration

Verify that the application's identity has **App Configuration Data Reader** on the App Configuration store.

### Ordinary configuration loads but the Key Vault reference fails

Verify that the application identity has **Key Vault Secrets User**, that the referenced secret exists and is enabled, and that relevant Key Vault network restrictions permit access.

### `AppConfigurationEndpoint` is missing

Set the variable in the same PowerShell session used to run the application:

```powershell
$env:AppConfigurationEndpoint = "https://<store-name>.azconfig.io"
dotnet run
```

### Wrong tenant or subscription

Inspect the current Azure CLI context:

```powershell
az account show
```

Then, if required:

```powershell
az account set --subscription "<subscription-id-or-name>"
```

---

## 22. Clean up

When the tutorial resources are no longer required, the resource group can be removed:

```powershell
az group delete `
    --name "rg-appconfig-demo" `
    --yes
```

Review the contents of the resource group before deleting it.

---

## Suggested follow-on exercises

1. Add `Development`, `Test`, and `Production` labels.
2. Select configuration by label in the C# application.
3. Add dynamic configuration refresh.
4. Add feature flags.
5. Replace the console application with Azure App Service, Azure Functions, or Azure Container Apps.
6. Enable managed identity on the hosted workload.
7. Provision the solution using Bicep or Terraform.
8. Add private endpoints and restrict public network access.

---

## Microsoft documentation

- [Azure App Configuration documentation](https://learn.microsoft.com/en-us/azure/azure-app-configuration/)
- [Access Azure App Configuration using Microsoft Entra ID](https://learn.microsoft.com/en-us/azure/azure-app-configuration/concept-enable-rbac)
- [Azure App Configuration Microsoft Entra authorisation](https://learn.microsoft.com/en-us/azure/azure-app-configuration/rest-api-authorization-azure-ad)
- [Use Key Vault references in an ASP.NET Core app](https://learn.microsoft.com/en-us/azure/azure-app-configuration/use-key-vault-references-dotnet-core)
- [.NET configuration provider for Azure App Configuration](https://learn.microsoft.com/en-us/azure/azure-app-configuration/reference-dotnet-provider)
- [Azure App Configuration key-value CLI commands](https://learn.microsoft.com/en-us/cli/azure/appconfig/kv)
- [Azure Key Vault RBAC guide](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide)
- [Azure Key Vault built-in security roles](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security)
- [Azure CLI Key Vault secret commands](https://learn.microsoft.com/en-us/cli/azure/keyvault/secret)
- [Use Azure CLI effectively behind a proxy](https://learn.microsoft.com/en-us/cli/azure/use-cli-effectively#work-behind-a-proxy)

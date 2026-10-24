using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

string? endpoint = Environment.GetEnvironmentVariable(
    "AppConfigurationEndpoint");
string? label = Environment.GetEnvironmentVariable(
    "AppConfigurationLabel");

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
            .Select("DemoApp:*", LabelFilter.Null);

        if (!string.IsNullOrWhiteSpace(label))
        {
            options.Select("DemoApp:*", label);
        }

        options.ConfigureKeyVault(keyVaultOptions =>
        {
            keyVaultOptions.SetCredential(credential);
        });
    })
    .Build();

DatabaseSettings databaseSettings =
    configuration
        .GetSection("DemoApp:Database")
        .Get<DatabaseSettings>()
    ?? new DatabaseSettings();

Console.WriteLine("Azure App Configuration Demo");
Console.WriteLine("----------------------------");
Console.WriteLine(
    $"Application Name : {configuration["DemoApp:ApplicationName"]}");
Console.WriteLine(
    $"Environment      : {configuration["DemoApp:Environment"]}");
Console.WriteLine(
    $"Database Server  : {databaseSettings.Server}");
Console.WriteLine(
    $"Database Name    : {databaseSettings.Name}");
Console.WriteLine(
    $"API Key          : {configuration["DemoApp:ApiKey"]}");

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

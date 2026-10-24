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
IConfigurationRefresher? refresher = null;

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

        options.ConfigureRefresh(refreshOptions =>
        {
            refreshOptions
                .RegisterAll()
                .SetRefreshInterval(TimeSpan.FromSeconds(5));
        });

        options.ConfigureKeyVault(keyVaultOptions =>
        {
            keyVaultOptions.SetCredential(credential);
        });

        refresher = options.GetRefresher();
    })
    .Build();

if (refresher is null)
{
    Console.Error.WriteLine("The configuration refresher could not be created.");
    return;
}

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

PrintConfiguration(configuration);
Console.WriteLine("Watching for changes. Press Ctrl+C to stop.");

try
{
    while (!cancellationSource.IsCancellationRequested)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(5),
            cancellationSource.Token);

        if (await refresher.TryRefreshAsync(cancellationSource.Token))
        {
            Console.WriteLine();
            Console.WriteLine($"Configuration refreshed at {DateTimeOffset.Now:T}.");
            PrintConfiguration(configuration);
        }
    }
}
catch (OperationCanceledException)
{
    // Ctrl+C requested a graceful shutdown.
}

static void PrintConfiguration(IConfiguration configuration)
{
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
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Services;
using Flurl.Http;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

public static class MigrateEmailTemplates
{
    private const string EmailChange = "https://raw.githubusercontent.com/Kareadita/KavitaEmail/main/KavitaEmail/config/templates/EmailChange.html";
    private const string EmailConfirm = "https://raw.githubusercontent.com/Kareadita/KavitaEmail/main/KavitaEmail/config/templates/EmailConfirm.html";
    private const string EmailPasswordReset = "https://raw.githubusercontent.com/Kareadita/KavitaEmail/main/KavitaEmail/config/templates/EmailPasswordReset.html";
    private const string SendToDevice = "https://raw.githubusercontent.com/Kareadita/KavitaEmail/main/KavitaEmail/config/templates/SendToDevice.html";
    private const string EmailTest = "https://raw.githubusercontent.com/Kareadita/KavitaEmail/main/KavitaEmail/config/templates/EmailTest.html";

    public static async Task Migrate(IDirectoryService directoryService, ILogger<Program> logger)
    {
        var files = directoryService.GetFiles(directoryService.CustomizedTemplateDirectory);
        if (files.Any())
        {
            return;
        }

        logger.LogCritical("Running MigrateEmailTemplates migration - Please be patient, this may take some time. This is not an error");

        // Write files to directory
        await DownloadAndWriteToFile(EmailChange, Path.Join(directoryService.CustomizedTemplateDirectory, "EmailChange.html"), logger);
        await DownloadAndWriteToFile(EmailConfirm, Path.Join(directoryService.CustomizedTemplateDirectory, "EmailConfirm.html"), logger);
        await DownloadAndWriteToFile(EmailPasswordReset, Path.Join(directoryService.CustomizedTemplateDirectory, "EmailPasswordReset.html"), logger);
        await DownloadAndWriteToFile(SendToDevice, Path.Join(directoryService.CustomizedTemplateDirectory, "SendToDevice.html"), logger);
        await DownloadAndWriteToFile(EmailTest, Path.Join(directoryService.CustomizedTemplateDirectory, "EmailTest.html"), logger);


        logger.LogCritical("Running MigrateEmailTemplates migration - Completed. This is not an error");
    }

    private static async Task DownloadAndWriteToFile(string url, string filePath, ILogger<Program> logger)
    {
        try
        {
            // Download the raw text using Flurl
            var content = await url.GetStringAsync();

            // Write the content to a file
            await File.WriteAllTextAsync(filePath, content);

            logger.LogInformation("{File} downloaded and written successfully", filePath);
        }
        catch (FlurlHttpException ex)
        {
            logger.LogError(ex, "Unable to download {Url} to {FilePath}. Please perform yourself!", url, filePath);
        }
    }


}

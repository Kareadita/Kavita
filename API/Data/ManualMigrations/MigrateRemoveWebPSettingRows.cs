using System.Threading.Tasks;
using API.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// Added in v0.7.2.7/v0.7.3 in which the ConvertXToWebP Setting keys were removed. This migration will remove them.
/// </summary>
public static class MigrateRemoveWebPSettingRows
{
    public static async Task Migrate(IUnitOfWork unitOfWork, ILogger<Program> logger)
    {
        logger.LogCritical("Running MigrateRemoveWebPSettingRows migration - Please be patient, this may take some time. This is not an error");

        var key = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.ConvertBookmarkToWebP);
        var key2 = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.ConvertCoverToWebP);
        if (key == null && key2 == null)
        {
            logger.LogCritical("Running MigrateRemoveWebPSettingRows migration - complete. Nothing to do");
            return;
        }

        unitOfWork.SettingsRepository.Remove(key);
        unitOfWork.SettingsRepository.Remove(key2);

        await unitOfWork.CommitAsync();

        logger.LogCritical("Running MigrateRemoveWebPSettingRows migration - Completed. This is not an error");
    }
}

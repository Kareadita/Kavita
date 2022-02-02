using System.Collections.Generic;
using API.DTOs.Email;
using API.Entities;
using API.Entities.Enums;
using AutoMapper;

namespace API.Helpers.Converters;

public abstract class SmtpConverter : ITypeConverter<IEnumerable<ServerSetting>, SmtpConfig>
{
    public SmtpConfig Convert(IEnumerable<ServerSetting> source, SmtpConfig destination, ResolutionContext context)
    {
        destination ??= new SmtpConfig();
        foreach (var row in source)
        {
            switch (row.Key)
            {
                case ServerSettingKey.SmtpHost:
                    destination.Host = row.Value;
                    break;
                case ServerSettingKey.SmtpPort:
                    destination.Port = int.Parse(row.Value);
                    break;
                case ServerSettingKey.SmtpUsername:
                    destination.UserName = row.Value;
                    break;
                case ServerSettingKey.SmtpEnableSsl:
                    destination.EnableSsl = bool.Parse(row.Value);
                    break;
                case ServerSettingKey.SmtpSenderAddress:
                    destination.SenderAddress = row.Value;
                    break;
                case ServerSettingKey.SmtpSenderDisplayName:
                    destination.SenderDisplayName = row.Value;
                    break;
                case ServerSettingKey.EnableSmtp:
                    destination.Enabled = bool.Parse(row.Value);
                    break;
                case ServerSettingKey.SmtpPassword:
                    destination.Password = row.Value;
                    break;
            }
        }

        return destination;
    }
}

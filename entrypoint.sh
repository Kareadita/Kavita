#! /bin/bash

if [ ! -f "/kavita/config/appsettings.json" ]; then
    echo "Kavita configuration file does not exist, creating..."
    echo '{
  "ConnectionStrings": {
    "DefaultConnection": "Data source=config//kavita.db"
  },
  "TokenKey": "super secret unguessable key",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Information",
      "Microsoft.Hosting.Lifetime": "Error",
      "Hangfire": "Information",
      "Microsoft.AspNetCore.Hosting.Internal.WebHost": "Information"
    },
    "File": {
      "Path": "config//logs/kavita.log",
      "Append": "True",
      "FileSizeLimitBytes": 26214400,
      "MaxRollingFiles": 2
    }
  },
  "Port": 5000
}' >> /kavita/config/appsettings.json
fi

chmod +x Kavita

./Kavita
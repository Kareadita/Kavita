#! /bin/bash

if [ ! -f "/kavita/config/appsettings.json" ]; then
    echo "Kavita configuration file does not exist, creating..."
    echo '{
  "TokenKey": "super secret unguessable key",
  "Port": 5000
}' >> /kavita/config/appsettings.json
fi

chmod +x Kavita

./Kavita

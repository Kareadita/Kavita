#! /bin/bash

# Set default username, UID and GID for Kavita but allow overrides
PUID=${PUID:-1000}
PGID=${PGID:-1000}
KAVITAUSER=${KAVITAUSER:-kavita}

# Add Kavita group if it doesn't already exist
if [[ $(getent group "$PGID" | cut -d':' -f1) != "$KAVITAUSER" ]]; then
    groupadd -o -g "$PGID" "$KAVITAUSER"
fi

# Add Kavita user if it doesn't already exist
if [[ $(getent passwd "$PUID" | cut -d':' -f1) != "$KAVITAUSER" ]]; then
    useradd -o -u "$PUID" -g "$PGID" -d /kavita "$KAVITAUSER"
fi

if [ ! -f /kavita/config/appsettings.json" ]; then
    echo "Kavita configuration file does not exist, creating..."
    echo '{
  "TokenKey": "super secret unguessable key",
  "Port": 5000
}' >> /kavita/config/appsettings.json
fi

chown -R "$PUID":"$PGID" /kavita
chmod 0500 /kavita/Kavita

su -l "$KAVITAUSER" -c ./Kavita

#! /bin/bash

# Set default UID and GID for Kavita but allow overrides
PUID=${PUID:-0}
PGID=${PGID:-0}

# Add Kavita group if it doesn't already exist
if [[ -z "$(getent group "$PGID" | cut -d':' -f1)" ]]; then
    groupadd -o -g "$PGID" kavita
fi

# Add Kavita user if it doesn't already exist
if [[ -z "$(getent passwd "$PUID" | cut -d':' -f1)" ]]; then
    useradd -o -u "$PUID" -g "$PGID" -d /kavita kavita
fi

if [ ! -f "/kavita/config/appsettings.json" ]; then
    echo "Kavita configuration file does not exist, creating..."
    echo '{
  "TokenKey": "super secret unguessable key",
  "Port": 5000
}' >> /kavita/config/appsettings.json
fi

chmod 0500 /kavita/Kavita

if [[ "$PUID" -eq 0 ]]; then
    # Run as root
    ./Kavita
else
    # Set ownership on all files except the library if running non-root
    find /kavita -path /kavita/library -prune -o -exec chown "$PUID":"$PGID" {} \;

    # Run as non-root user
    su -l kavita -c ./Kavita
fi

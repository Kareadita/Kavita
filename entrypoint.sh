#! /bin/bash

# Removed - This is causing issues for Synology users
## Set default UID and GID for Kavita but allow overrides
#PUID=${PUID:-0}
#PGID=${PGID:-0}
#
## Add Kavita group if it doesn't already exist
#if [[ -z "$(getent group "$PGID" | cut -d':' -f1)" ]]; then
#    groupadd -o -g "$PGID" kavita
#fi
#
## Add Kavita user if it doesn't already exist
#if [[ -z "$(getent passwd "$PUID" | cut -d':' -f1)" ]]; then
#    useradd -o -u "$PUID" -g "$PGID" -d /kavita kavita
#fi

if [ ! -f "/kavita/config/appsettings.json" ]; then
    echo "Kavita configuration file does not exist, creating..."
    echo '{
  "TokenKey": "super secret unguessable key",
  "Port": 5000
}' >> /kavita/config/appsettings.json
fi

chmod +x Kavita

./Kavita

#if [[ "$PUID" -eq 0 ]]; then
#    # Run as root
#    ./Kavita
#else
#    # Set ownership on config dir if running non-root and current ownership is different
#    if [[ ! "$(stat -c %u /kavita/config)" = "$PUID" ]]; then
#        echo "Specified PUID differs from Kavita config dir ownership, updating permissions now..."
#        if [[ ! "$(stat -c %g /kavita/config)" = "$PGID" ]]; then
#            chown -R "$PUID":"$PGID" /kavita/config
#        else
#            chown -R "$PUID" /kavita/config
#        fi
#
#    elif [[ ! "$(stat -c %g /kavita/config)" = "$PGID" ]]; then
#        echo "Specified PGID differs from Kavita config dir ownership, updating permissions now..."
#        chgrp -R "$PGID" /kavita/config
#    fi
#
#    # Run as non-root user
#    su -l kavita -c ./Kavita
#fi

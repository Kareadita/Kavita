#!/bin/bash

migrations=($(ls -1 Data/Migrations/*.cs 2>/dev/null | grep -v "Designer.cs" | grep -v "Snapshot.cs" | sort -r))

if [ ${#migrations[@]} -lt 2 ]; then
    echo "Error: Need at least 2 migrations to redo"
    exit 1
fi

second_last=$(basename "${migrations[1]}" .cs)
last=$(basename "${migrations[0]}" .cs)
last_name=$(echo "$last" | sed 's/^[0-9]*_//')

new_name=${1:-$last_name}

echo "Rolling back to: $second_last"
echo "Removing $last_name and re-adding as $new_name"
read -p "Continue? (y/N) " -n 1 -r
echo ""

if [[ $REPLY =~ ^[Yy]$ ]]; then
    dotnet ef database update "$second_last" && \
    dotnet ef migrations remove && \
    dotnet ef migrations add "$new_name"
else
    echo "Cancelled"
    exit 0
fi

# fsprobe

Checks how a library folder reports file and folder times on your setup. We use the results to decide how Kavita's scanner should detect changes. Run it where Kavita runs (inside the Docker container if that is how you run Kavita), against a real library folder.

## Get the binary

Pick the archive for where you run it. Each one holds a single `fsprobe` program and this README, nothing to install.

| Where | Archive |
|---|---|
| Kavita Docker image, most Linux, Unraid host | `fsprobe-linux-x64.tar.gz` |
| Raspberry Pi, ARM NAS | `fsprobe-linux-arm64.tar.gz` |
| Alpine-based images | `fsprobe-linux-musl-x64.tar.gz` |
| Windows | `fsprobe-win-x64.zip` |
| macOS (Apple Silicon) | `fsprobe-osx-arm64.tar.gz` |

Not sure which Linux one? Run `uname -m`: `x86_64` is x64, `aarch64` is arm64.

To build them yourself: `./publish.sh` (needs the .NET 10 SDK), output in `dist/`.

## Docker (most setups)

Unpack it into your Kavita config folder on the host, so the container can see it at `/kavita/config`:

```bash
tar -xzf fsprobe-linux-x64.tar.gz --strip-components=1 -C /path/to/kavita/config/ fsprobe-linux-x64/fsprobe
```

If it says `Permission denied` when run, add the executable bit: `chmod +x /path/to/kavita/config/fsprobe`.

Reports are written next to the binary, so they show up in the same config folder on the host.

### 1. Passive (read-only, always safe)

```bash
docker exec kavita /kavita/config/fsprobe passive /manga --label "my-setup-name"
```

Use the container name and the library path as Kavita sees them. A large library takes a while, because every file is read several times. A few thousand files is plenty, so pointing it at one big subfolder is fine.

### 2. Active (writes a temp folder, then deletes it)

```bash
docker exec kavita /kavita/config/fsprobe active /manga --label "my-setup-name"
```

It creates `.kavita-fsprobe-<time>/` inside the folder, makes small test changes (add, delete, rename, overwrite, copy-over that keeps the old date, symlinks, ...) using `.bin` files, which Kavita never scans,, and reads the folder right away, after 5 s and after 65 s. Takes about 70 s. If the library is mounted read-only (`:ro`), point it at any writable folder **on the same mount/share** instead.

Symlink cases are skipped on Windows unless Developer Mode is on. That is expected.

### 3. Mark and check (a change made from another machine)

This catches caching on network shares, where the NAS writes the file and Kavita reads it over SMB/NFS.

```bash
docker exec kavita /kavita/config/fsprobe mark /manga --label "my-setup-name"
```

Now, **from the machine that normally writes to the library** (NAS, downloader, your PC over the share), do a few of these in one series folder:
- add a new file
- copy a file over an existing one with the same name (Windows Explorer copy-over is a great test)
- rename a file

Then run check right away, and again a minute later (the command is printed by `mark`):

```bash
docker exec kavita /kavita/config/fsprobe check /manga --snapshot /kavita/config/fsprobe-mark-....json --label "my-setup-name"
```

## Unraid extra

If you can, run passive and active twice, once on the user share and once on a single disk, to compare shfs with the underlying disk:

```bash
./fsprobe active /mnt/user/manga  --label unraid-user-share
./fsprobe active /mnt/disk1/manga --label unraid-disk1
```

## What to send back

The `fsprobe-*.json` files, plus one line about the setup: OS/NAS, how the library is mounted into Kavita (bind mount, SMB, NFS, mergerfs, ...), and where the files are written from.

Reports include folder and file paths from your library. Remove anything you would rather not share.

## Reading the output

Active mode prints one row per test case:

```
copy-over-keep-mtime             - - - S T
```

| Letter | Means: this way of checking noticed the change |
|---|---|
| F | the folders' own times only |
| K | Kavita today (folder time plus every file's time) |
| C | the faster candidate (same as K, times read from the directory listing) |
| S | the list of file names and sizes changed |
| T | change time (ctime on Linux, ChangeTime on Windows). The OS sets it on every write, tools cannot set it back |

A `-` means that check would have missed the change. `?` means change time is not available on this system. If a row says `candidate differs from Kavita today`, that is exactly what we are looking for, please flag it.

**Linux testers:** please check that the `T` column shows `T` or `-`, not `?`. If it shows `?` on Linux, change time could not be read (this part has not been run on Linux yet).

## NOTE: This is completely AI-coded as part of a data collection program aligned with Scanner work (https://github.com/Kareadita/Kavita/pull/4983)

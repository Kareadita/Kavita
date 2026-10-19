# watchprobe

Checks which way of watching a folder notices changes on your setup. Kavita's folder watching does not pick up changes for some setups (Docker with the library on a network share), and we use the results to decide how to fix it. Run it where Kavita runs (inside the Docker container if that is how you run Kavita), against a real library folder.

It runs three watchers on the same folder at once and prints every change each one sees:

| Name | What it is |
|---|---|
| `watcher` | The same file watcher Kavita uses today |
| `provider` | .NET's polling file provider. It only says "something changed", never which file |
| `poller` | Lists the folder every few seconds and compares names, sizes and dates |

## Get the binary

Pick the archive for where you run it. Each one holds a single `watchprobe` program and this README, nothing to install.

| Where | Archive |
|---|---|
| Kavita Docker image, most Linux, Unraid host | `watchprobe-linux-x64.tar.gz` |
| Raspberry Pi, ARM NAS | `watchprobe-linux-arm64.tar.gz` |
| Alpine-based images | `watchprobe-linux-musl-x64.tar.gz` |
| Windows | `watchprobe-win-x64.zip` |
| macOS (Apple Silicon) | `watchprobe-osx-arm64.tar.gz` |

Not sure which Linux one? Run `uname -m`: `x86_64` is x64, `aarch64` is arm64.

To build them yourself: `./publish.sh` (needs the .NET 10 SDK), output in `dist/`.

## Docker (most setups)

Unpack it into your Kavita config folder on the host, so the container can see it at `/kavita/config`:

```bash
tar -xzf watchprobe-linux-x64.tar.gz --strip-components=1 -C /path/to/kavita/config/ watchprobe-linux-x64/watchprobe
```

If it says `Permission denied` when run, add the executable bit: `chmod +x /path/to/kavita/config/watchprobe`.

### Pick a small folder

Point it at **one series folder**, or any folder with a few hundred files, not the whole library. Two of the three watchers list the whole folder every few seconds, and on a large library over a network share that keeps the share busy. If the first listing takes over 30 s, the probe warns you; press Ctrl+C and pick a smaller folder.

### Run it

```bash
docker exec -it kavita /kavita/config/watchprobe "/manga/Some Series" --label "my-setup-name"
```

Use the container name and the folder path as Kavita sees them. Keep `-it`, so Enter and Ctrl+C reach the probe.

What happens:
1. **Control (about 30 s).** The probe writes a small `watchprobe-control-<time>.bin` file into the folder, waits, then deletes it. This shows whether each watcher works at all when the change is made from inside the container. If the folder is mounted read-only, add `--no-control`.
2. **Your changes (the important part).** When the probe prints "Now make changes FROM ANOTHER MACHINE", go to the machine that normally writes to the library (the NAS, a downloader, your PC over the share) and, in that same folder:
   - copy a new `.cbz` or `.zip` in
   - rename a file
   - delete a file (a copy you made, not a real one)
   - create a new folder and copy a file into it
   - copy a file over an existing one with the same name (Windows Explorer: "Replace the file")

   Wait about 15 s between changes so they can be told apart. Each change should show up as lines in the probe's window.
3. **Finish.** It stops after 10 minutes, or press Enter or Ctrl+C when you are done. The report is written either way, and saved every 30 s while it runs.

Options: `--minutes 10` how long to watch, `--interval 5` seconds between the poller's listings.

### Optional: run it on the host too

If the host (outside Docker) can reach the same share, run the same test there as well, with a different label. It tells us whether the problem is Docker or the share itself.

```bash
./watchprobe "/mnt/nas/manga/Some Series" --label "my-setup-host"
```

## Cost mode: how long polling a whole library takes

This one is read-only and is meant for a **whole library**, not one series. It changes nothing and needs no changes from you. It times four ways of checking a library for changes:

| Name | What it does |
|---|---|
| `folder-names` | Walks every folder, reads no times |
| `folder-times` | Walks every folder and reads each folder's last-modified time |
| `known-folders` | Reads each folder's last-modified time from a list, without walking |
| `full` | Lists every file and folder with its time, like the `poller` above |

```bash
docker exec -it kavita /kavita/config/watchprobe cost /library --label "my-setup-name"
```

Each way runs twice, with 15 s between runs so the share's cache has expired. On a large library on a network share, the `full` runs can take several minutes each, so expect it to take a while. Add `--no-full` to skip them.

Options: `--runs 2` how many times to time each way, `--gap 15` seconds between runs, `--methods known-folders,full` time only these ways (comma-separated, default all four), `--no-full` skip the whole-tree listing.

### Repeated folder-time checks

A real poller would read the known folders' times over and over, with no walk in between. To time that:

```bash
docker exec -it kavita /kavita/config/watchprobe cost /library --methods known-folders --runs 5 --gap 60 --label "my-setup-known"
```

It walks the library once at the start to find the folders (not timed), then reads their times 5 times, a minute apart.

## Copy mode: can a half-copied file be told apart

Copies a large file into a folder several ways and, every 200 ms while each copy runs, records the file's size, creation time, last-modified time, and whether opening it while denying writers fails. Two watchers run alongside: one set up like Kavita's, one that also listens for size changes. It **writes** into the folder (a `watchprobe-copy-<method>` folder per copy, deleted afterwards), so point it at an empty test folder, not a library.

```bash
watchprobe copy /path/to/empty-test-folder --size-mb 300 --seconds 60 --label "my-setup-copy"
```

| Method | How it copies |
|---|---|
| `stream` | .NET writes in 1 MB chunks spread over `--seconds`, like a slow network copy |
| `file-copy` | `File.Copy` (Windows: the CopyFileEx engine) |
| `robocopy` | Windows only |
| `shell` | Windows only, the engine Explorer uses (`Shell.Application.CopyHere`) |

Options: `--source <file>` copy this file instead of a generated one, `--size-mb 300` size of the generated file (use a few GB so the fast copies last long enough to sample), `--seconds 60` length of the `stream` copy, `--methods stream,shell` copy only these ways.

## Selfread mode: which reads make the watcher report a change

Creates a `watchprobe-self-<time>` folder with one small `.cbz`, waits for the watcher to go quiet, then makes the reads Kavita's validation and scan make, one at a time: list the names, read sizes and times, read the folder's time, open the file denying writers, read the zip's entries, read the whole file. Then it writes a second file, deletes it and renames the first, and reports every watcher event after each step. It deletes the folder at the end. Point it at a test folder, not a library.

```bash
watchprobe selfread /path/to/test-folder --label "my-setup-selfread"
```

Options: `--pause 5` seconds to collect events after each step.

## What to send back

The `watchprobe-*.json` file (written next to the program, so in your config folder), plus one line about the setup: OS/NAS, how the library is mounted into Kavita (bind mount, SMB/CIFS, NFS, mergerfs, ...), and which machine you made the changes from.

Reports include folder and file paths from your library. Remove anything you would rather not share.

## Reading the output

Each line is one thing one watcher saw:

```
  [    32.0s] watcher  Created  series-a\c002.cbz
  [    32.0s] provider Fired
  [    34.1s] poller   Added    series-a\c002.cbz  4 bytes
```

The summary at the end says, for the control file and for your changes, which watchers noticed and how fast:

```
  control create  watcher 0.0 s, provider 8.0 s, poller 2.0 s
  poller found 6 file changes besides the control: watcher saw 6, provider fired near 6, folder times would have caught 5
```

`folder times would have caught` counts the changes where the file's folder also got a new last-modified time, so a check that reads only folder times would have noticed it. `missed` means that watcher never reported it. If the `watcher` column misses your changes from another machine, that is the bug we are chasing.

## NOTE: This is completely AI-coded as part of a data collection program for Kavita's folder watching work

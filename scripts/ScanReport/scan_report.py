#!/usr/bin/env python3
"""
Kavita scan report. Reads kavita.db (read-only) and writes kavita-scan-report.txt for the Kavita team.

Usage:
  python3 scan_report.py /path/to/config/kavita.db
  python3 scan_report.py /path/to/config/kavita.db --series "Chamber of Chills"
  python3 scan_report.py /path/to/config/kavita.db --file "James Bond 007 For King and Country 006"
  python3 scan_report.py /path/to/config/kavita.db --days 30
  python3 scan_report.py /path/to/config/kavita.db --compare /path/to/config/backups/kavita_backup_2026-10-01.zip
  python3 scan_report.py /path/to/config/kavita.db --audit-tags            (counts only, safe to paste anywhere)
  python3 scan_report.py /path/to/config/kavita.db --audit-tags --names    (adds sample series and folder names)

Only Python 3 is needed, no packages. Nothing in the database is changed.
The report contains library names, series names and file paths. It does not read users, emails or passwords.
"""
import argparse
import collections
import datetime
import os
import re
import shutil
import sqlite3
import sys
import tempfile
import unicodedata
import zipfile

parser = argparse.ArgumentParser(description='Collect scanner diagnostics from a Kavita database')
parser.add_argument('db', help='Path to kavita.db (in Kavita\'s config folder)')
parser.add_argument('--series', action='append', default=[], help='Part of a series name to dump in full. Can repeat')
parser.add_argument('--file', action='append', default=[], help='Part of a file name to look up. Can repeat')
parser.add_argument('--days', type=int, default=14, help='How far back to list newly created file rows (default 14)')
parser.add_argument('--compare', help='An older kavita.db, or a Kavita backup .zip, to compare file rows against')
parser.add_argument('--audit-tags', action='store_true',
                    help='Only count series that may come from wrong ComicInfo tags. Counts only unless --names')
parser.add_argument('--names', action='store_true', help='With --audit-tags, also list sample series and folder names')
parser.add_argument('--out', default='kavita-scan-report.txt', help='Report file to write')
args = parser.parse_args()

temp_dir = tempfile.mkdtemp(prefix='kavita-scan-report-')


def open_db(path):
    if not os.path.isfile(path):
        sys.exit(f'Database not found: {path}')

    if zipfile.is_zipfile(path):
        with zipfile.ZipFile(path) as archive:
            entry = next((n for n in archive.namelist() if n.replace('\\', '/').split('/')[-1] == 'kavita.db'), None)
            if entry is None:
                sys.exit(f'No kavita.db inside {path}')
            target = os.path.join(temp_dir, f'{len(os.listdir(temp_dir))}-kavita.db')
            with archive.open(entry) as source, open(target, 'wb') as dest:
                shutil.copyfileobj(source, dest)
            path = target

    connection = sqlite3.connect('file:' + os.path.abspath(path).replace('\\', '/') + '?mode=ro', uri=True)
    connection.row_factory = sqlite3.Row
    return connection


con = open_db(args.db)

LIBRARY_TYPES = {0: 'Manga', 1: 'Comic', 2: 'Book', 3: 'Image', 4: 'LightNovel', 5: 'ComicVine'}
FOLDER_WATCH_MODES = {0: 'Off', 1: 'Automatic', 2: 'FileWatcher', 3: 'Polling'}
MAX_ROWS = 300
SCRIPT_VERSION = 2

out = open(args.out, 'w', encoding='utf-8')


def write(line=''):
    out.write(line + '\n')


def section(title):
    write()
    write('=' * 100)
    write(title)
    write('=' * 100)


def columns(table):
    return {r['name'] for r in con.execute(f'PRAGMA table_info("{table}")')}


def table_exists(table):
    return con.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name=?", (table,)).fetchone() is not None


def folder_of(path):
    return path.rsplit('/', 1)[0] if '/' in path else path


def rows(sql, params=()):
    return con.execute(sql, params).fetchall()


file_cols = columns('MangaFile')
has_write_time = 'FileLastWriteTimeUtc' in file_cols
write_time_col = 'f.FileLastWriteTimeUtc' if has_write_time else 'NULL'

section('Server')
write(f'Report made: {datetime.datetime.now().isoformat(timespec="seconds")}')
for key, label in ((11, 'Install version'), (39, 'First install version')):
    r = con.execute('SELECT Value FROM ServerSetting WHERE Key = ?', (key,)).fetchone()
    write(f'{label}: {r["Value"] if r else "?"}')
if table_exists('__EFMigrationsHistory'):
    last = rows('SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 3')
    write('Last migrations: ' + ', '.join(r['MigrationId'] for r in last))
write(f'MangaFile.FileLastWriteTimeUtc present: {has_write_time}')
write(f'Script version: {SCRIPT_VERSION}')


STOP_WORDS = {'the', 'of', 'and', 'a', 'an', 'annual', 'vol', 'volume', 'special', 'specials'}


def name_parts(name):
    without_accents = ''.join(c for c in unicodedata.normalize('NFKD', (name or '').lower()) if not unicodedata.combining(c))
    without_years = re.sub(r'\(.*?\)', ' ', without_accents)
    without_joiners = re.sub(r"['.’]", '', without_years)
    # \w matches '_', which would make Abe_Sapien_(2013) a single word
    return re.findall(r'[^\W_]+', without_joiners)


def name_words(name):
    return {w for w in name_parts(name) if len(w) > 1 and w not in STOP_WORDS}


def joined_name(name):
    return ''.join(name_parts(name))


def audit_tags(show_names):
    has_cv_id = 'ComicVineId' in columns('Chapter')
    cv_col = 'c.ComicVineId' if has_cv_id else 'NULL'
    sample_size = 25

    for library in rows('SELECT Id, Name, Type FROM Library ORDER BY Id'):
        files = rows(f'''
            SELECT s.Id AS SeriesId, s.Name, s.OriginalName, s.LocalizedName, f.FilePath, {cv_col} AS ComicVineId
            FROM MangaFile f
            JOIN Chapter c ON c.Id = f.ChapterId
            JOIN Volume v ON v.Id = c.VolumeId
            JOIN Series s ON s.Id = v.SeriesId
            WHERE s.LibraryId = ?
        ''', (library['Id'],))
        if not files:
            continue

        series_words = {}
        series_joined = {}
        series_names = {}
        series_file_count = collections.Counter()
        files_by_folder = collections.defaultdict(collections.Counter)
        small_cv_series = set()
        for f in files:
            sid = f['SeriesId']
            series_names[sid] = f['Name']
            if sid not in series_words:
                names = (f['Name'], f['OriginalName'], f['LocalizedName'])
                series_words[sid] = set().union(*(name_words(n) for n in names))
                series_joined[sid] = {joined_name(n) for n in names} - {''}
            series_file_count[sid] += 1
            files_by_folder[folder_of(f['FilePath'])][sid] += 1
            if f['ComicVineId'] and 0 < int(f['ComicVineId']) < 1000:
                small_cv_series.add(sid)

        # Series to the folders it sits in as a guest, where another series has strictly more files
        guest_folders = collections.defaultdict(set)
        for folder, counts in files_by_folder.items():
            if len(counts) < 2:
                continue
            (owner, owner_count), (_, runner_up_count) = counts.most_common(2)
            if owner_count == runner_up_count:
                continue
            for sid in counts:
                if sid != owner:
                    guest_folders[sid].add(folder)

        def folder_name(folder):
            return folder.rsplit('/', 1)[-1]

        def shares_word(sid, folder):
            return bool(name_words(folder_name(folder)) & series_words[sid])

        def spelling_variant(sid, folder):
            joined_folder = joined_name(folder_name(folder))
            # Under 4 letters, a catch-all folder like 'a' or 'new' would match almost any name
            return any(len(shorter) >= 4 and shorter in longer
                       for name in series_joined[sid]
                       for shorter, longer in [sorted((name, joined_folder), key=len)])

        def named_for_owner(folder):
            owner = files_by_folder[folder].most_common(1)[0][0]
            return shares_word(owner, folder) or spelling_variant(owner, folder)

        def keep(folders_by_series, test):
            kept = {sid: [f for f in folders if test(sid, f)] for sid, folders in folders_by_series.items()}
            return {sid: folders for sid, folders in kept.items() if folders}

        all_guest = {sid: sorted(folders) for sid, folders in guest_folders.items()}
        c_folders = keep(all_guest, lambda sid, f: not shares_word(sid, f))
        d_folders = keep(c_folders, lambda sid, f: not spelling_variant(sid, f))
        e_folders = keep(d_folders, lambda sid, f: named_for_owner(f))

        rule_a = set(all_guest)
        rule_b = {sid for sid, folders in all_guest.items() if len(folders) >= 2}
        rule_c, rule_d, rule_e = set(c_folders), set(d_folders), set(e_folders)

        section(f'Tag audit: library #{library["Id"]} ({LIBRARY_TYPES.get(library["Type"], library["Type"])})')
        write(f'{len(series_names)} series, {len(files)} files, {len(files_by_folder)} folders')
        write(f'Rule A (series has files in another series\' folder):        {len(rule_a)} series')
        write(f'Rule B (A, in 2+ such folders):                              {len(rule_b)} series')
        write(f'Rule C (A, folder name shares no word with series name):     {len(rule_c)} series')
        write(f'Rule D (C, folder name is not a spelling variant either):    {len(rule_d)} series')
        write(f'Rule E (D, folder is named after the series that owns it):   {len(rule_e)} series')
        write(f'Only A (what C lets through):                                {len(rule_a - rule_c)} series')
        if has_cv_id:
            write(f'Series with a chapter ComicVine id under 1000:              {len(small_cv_series)} series '
                  f'({len(small_cv_series & rule_e)} also in E)')

        if not show_names:
            continue

        def describe(sid, folder):
            counts = files_by_folder[folder]
            total = sum(counts.values())
            owner_share = round(100 * counts.most_common(1)[0][1] / total)
            return (f'{counts[sid]} of the series\' {series_file_count[sid]} files here; folder has {len(counts)} series, '
                    f'owner holds {owner_share}% of {total} files')

        def sample(title, folders_by_series, ids=None):
            ids = set(folders_by_series) if ids is None else ids
            write()
            write(f'{title} (up to {sample_size}):')
            for sid in sorted(ids, key=lambda s: series_names[s])[:sample_size]:
                folders = folders_by_series.get(sid, [])
                if not folders:
                    write(f'  {series_names[sid]}  in  -')
                    continue
                more = len(all_guest[sid]) - 1
                write(f'  {series_names[sid]}  in  {folder_name(folders[0])}  [{describe(sid, folders[0])}]'
                      + (f'  (+{more} more guest folders)' if more > 0 else ''))

        sample('Rule E', e_folders)
        sample('In D, not E (folder not named after its owner)', d_folders, rule_d - rule_e)
        sample('In C, not D (spelling variant)', c_folders, rule_c - rule_d)
        sample('Rule B', all_guest, rule_b)
        sample('Only A', all_guest, rule_a - rule_c)
        if has_cv_id:
            sample('ComicVine id under 1000, not in E', all_guest, small_cv_series - rule_e)


if args.audit_tags:
    audit_tags(args.names)
    out.close()
    con.close()
    shutil.rmtree(temp_dir, ignore_errors=True)
    print(f'Wrote {os.path.abspath(args.out)}')
    sys.exit(0)

section('Libraries')
for r in rows('SELECT * FROM Library ORDER BY Id'):
    series_count = con.execute('SELECT COUNT(*) FROM Series WHERE LibraryId = ?', (r['Id'],)).fetchone()[0]
    folders = [f['Path'] for f in rows('SELECT Path FROM FolderPath WHERE LibraryId = ?', (r['Id'],))]
    # Databases before v0.9.2 have the FolderWatching bool instead of the mode
    if 'FolderWatchMode' in r.keys():
        watch_mode = FOLDER_WATCH_MODES.get(r['FolderWatchMode'], r['FolderWatchMode'])
    else:
        watch_mode = 'Automatic' if r['FolderWatching'] else 'Off'
    write(f'#{r["Id"]} {r["Name"]} | type {LIBRARY_TYPES.get(r["Type"], r["Type"])} | {series_count} series | '
          f'last scanned {r["LastScanned"]} | folder watching {watch_mode} | K+ matching {bool(r["AllowMetadataMatching"])}')
    for f in folders:
        write(f'    root: {f}')

file_sql = f'''
    SELECT f.Id, f.Created, f.CreatedUtc, f.LastModified, {write_time_col} AS FileWriteTimeUtc, f.Bytes, f.Pages, f.FilePath,
           c.Id AS ChapterId, c.Range, c.Number, c.IsSpecial, c.Created AS ChapterCreated,
           v.Id AS VolumeId, v.Name AS VolumeName, s.Id AS SeriesId, s.Name AS SeriesName, s.LibraryId
    FROM MangaFile f
    JOIN Chapter c ON c.Id = f.ChapterId
    JOIN Volume v ON v.Id = c.VolumeId
    JOIN Series s ON s.Id = v.SeriesId
'''


def write_file_row(r):
    write(f'  file {r["Id"]} | created {r["Created"]} | file write time {r["FileWriteTimeUtc"]} | {r["Bytes"]} bytes, {r["Pages"]} pages')
    write(f'      {r["FilePath"]}')
    write(f'      series {r["SeriesId"]} "{r["SeriesName"]}" | volume {r["VolumeId"]} "{r["VolumeName"]}" | '
          f'chapter {r["ChapterId"]} range "{r["Range"]}" (created {r["ChapterCreated"]})')


for name in args.series:
    section(f'Series matching "{name}"')
    matches = rows('''
        SELECT Id, LibraryId, Name, OriginalName, LocalizedName, SortName, NormalizedName, NormalizedOriginalName,
               NormalizedLocalizedName, Format, Created, LastModified, LastFolderScanned, FolderPath, LowestFolderPath,
               NameLocked, DontMatch, IsBlacklisted
        FROM Series WHERE Name LIKE ? OR OriginalName LIKE ? OR LocalizedName LIKE ?
    ''', (f'%{name}%',) * 3)
    if not matches:
        write('No series found')
    for s in matches:
        write()
        for key in s.keys():
            write(f'{key}: {s[key]}')
        if table_exists('ExternalSeriesMetadata'):
            for e in rows('SELECT * FROM ExternalSeriesMetadata WHERE SeriesId = ?', (s['Id'],)):
                write('Kavita+ metadata: ' + ', '.join(f'{k}={e[k]}' for k in e.keys()))
        files = rows(file_sql + ' WHERE s.Id = ? ORDER BY f.Created', (s['Id'],))
        write(f'{len(files)} files, in {len({folder_of(f["FilePath"]) for f in files})} folders:')
        for f in files[:MAX_ROWS]:
            write_file_row(f)
        if len(files) > MAX_ROWS:
            write(f'  ... {len(files) - MAX_ROWS} more')

for name in args.file:
    section(f'Files matching "{name}"')
    found = rows(file_sql + ' WHERE f.FilePath LIKE ? ORDER BY f.FilePath', (f'%{name}%',))
    if not found:
        write('No file rows found')
    for f in found[:MAX_ROWS]:
        write_file_row(f)

all_files = rows('''
    SELECT f.Id, f.FilePath, c.Id AS ChapterId, c.Range, s.Id AS SeriesId, s.Name AS SeriesName, s.LibraryId
    FROM MangaFile f
    JOIN Chapter c ON c.Id = f.ChapterId
    JOIN Volume v ON v.Id = c.VolumeId
    JOIN Series s ON s.Id = v.SeriesId
''')

section('Series with files in more than one folder (most folders first)')
write('Normal for volume subfolders or Specials folders. Suspicious when the folders are different series.')
folders_by_series = collections.defaultdict(set)
names = {}
for f in all_files:
    folders_by_series[f['SeriesId']].add(folder_of(f['FilePath']))
    names[f['SeriesId']] = f['SeriesName']
spread = sorted(((sid, fs) for sid, fs in folders_by_series.items() if len(fs) > 1), key=lambda x: -len(x[1]))
write(f'{len(spread)} series')
for sid, fs in spread[:50]:
    write(f'series {sid} "{names[sid]}": {len(fs)} folders')
    for folder in sorted(fs)[:10]:
        write(f'    {folder}')
    if len(fs) > 10:
        write(f'    ... {len(fs) - 10} more')

section('Chapters with files from more than one folder')
folders_by_chapter = collections.defaultdict(set)
chapter_info = {}
for f in all_files:
    folders_by_chapter[f['ChapterId']].add(folder_of(f['FilePath']))
    chapter_info[f['ChapterId']] = (f['SeriesId'], f['SeriesName'], f['Range'])
mixed = sorted(((cid, fs) for cid, fs in folders_by_chapter.items() if len(fs) > 1), key=lambda x: -len(x[1]))
write(f'{len(mixed)} chapters')
for cid, fs in mixed[:100]:
    sid, sname, rng = chapter_info[cid]
    write(f'chapter {cid} range "{rng}" in series {sid} "{sname}": {len(fs)} folders')
    for folder in sorted(fs)[:5]:
        write(f'    {folder}')

section(f'File rows created in the last {args.days} days')
since = (datetime.datetime.now(datetime.timezone.utc).replace(tzinfo=None) - datetime.timedelta(days=args.days)).strftime('%Y-%m-%d %H:%M:%S')
recent = rows(file_sql + ' WHERE f.CreatedUtc >= ? ORDER BY f.Id', (since,))
write(f'{len(recent)} rows')
per_day = collections.Counter((r['Created'] or '')[:10] for r in recent)
for day, count in sorted(per_day.items()):
    write(f'  {day}: {count}')

if has_write_time:
    write()
    write('Rows created recently for files whose own write time is more than 30 days older (old file, new row):')
    old_files = []
    for r in recent:
        if not r['FileWriteTimeUtc'] or not r['CreatedUtc']:
            continue
        try:
            created = datetime.datetime.fromisoformat(str(r['CreatedUtc'])[:19])
            written = datetime.datetime.fromisoformat(str(r['FileWriteTimeUtc'])[:19])
        except ValueError:
            continue
        if created - written > datetime.timedelta(days=30):
            old_files.append(r)
    write(f'{len(old_files)} rows')
    for r in old_files[:MAX_ROWS]:
        write_file_row(r)

write()
write('Newest file rows:')
for r in recent[-50:]:
    write_file_row(r)

if args.compare:
    old_con = open_db(args.compare)
    section(f'Compared with {os.path.basename(args.compare)}')
    old_version = old_con.execute('SELECT Value FROM ServerSetting WHERE Key = 11').fetchone()
    write(f'Older database install version: {old_version["Value"] if old_version else "?"}')

    compare_sql = '''
        SELECT f.Id, f.FilePath, f.Created, c.Id AS ChapterId, c.Range, s.Id AS SeriesId, s.Name AS SeriesName
        FROM MangaFile f
        JOIN Chapter c ON c.Id = f.ChapterId
        JOIN Volume v ON v.Id = c.VolumeId
        JOIN Series s ON s.Id = v.SeriesId
    '''
    old_by_path = {r['FilePath']: r for r in old_con.execute(compare_sql)}
    new_by_path = {r['FilePath']: r for r in con.execute(compare_sql)}
    write(f'File rows: {len(old_by_path)} before, {len(new_by_path)} now')

    new_id = []
    moved_series = []
    moved_chapter = []
    for path, now in new_by_path.items():
        before = old_by_path.get(path)
        if before is None:
            continue
        if before['Id'] != now['Id']:
            new_id.append((before, now))
        if before['SeriesName'] != now['SeriesName']:
            moved_series.append((before, now))
        elif before['Range'] != now['Range']:
            moved_chapter.append((before, now))

    def write_pairs(title, pairs):
        write()
        write(f'{title}: {len(pairs)}')
        for before, now in pairs[:MAX_ROWS]:
            write(f'  {now["FilePath"]}')
            write(f'      before: file {before["Id"]} created {before["Created"]} | series {before["SeriesId"]} '
                  f'"{before["SeriesName"]}" | chapter {before["ChapterId"]} range "{before["Range"]}"')
            write(f'      now:    file {now["Id"]} created {now["Created"]} | series {now["SeriesId"]} '
                  f'"{now["SeriesName"]}" | chapter {now["ChapterId"]} range "{now["Range"]}"')
        if len(pairs) > MAX_ROWS:
            write(f'  ... {len(pairs) - MAX_ROWS} more')

    write_pairs('Same file, new row Id (deleted and inserted again)', new_id)
    write_pairs('Same file, now in a series with a different name', moved_series)
    write_pairs('Same file and series, different chapter range', moved_chapter)

    only_new = sorted(set(new_by_path) - set(old_by_path))
    only_old = sorted(set(old_by_path) - set(new_by_path))
    write()
    write(f'Files only in the current database: {len(only_new)}')
    for path in only_new[:MAX_ROWS]:
        r = new_by_path[path]
        write(f'  file {r["Id"]} created {r["Created"]} | "{r["SeriesName"]}" | {path}')
    write()
    write(f'Files only in the older database: {len(only_old)}')
    for path in only_old[:MAX_ROWS]:
        r = old_by_path[path]
        write(f'  file {r["Id"]} | "{r["SeriesName"]}" | {path}')

    old_series = {r['Name'] for r in old_con.execute('SELECT Name FROM Series')}
    new_series = {r['Name'] for r in con.execute('SELECT Name FROM Series')}
    write()
    write(f'Series names only now: {len(new_series - old_series)}')
    for n in sorted(new_series - old_series)[:MAX_ROWS]:
        write(f'  {n}')
    write(f'Series names only before: {len(old_series - new_series)}')
    for n in sorted(old_series - new_series)[:MAX_ROWS]:
        write(f'  {n}')
    old_con.close()

out.close()
con.close()
shutil.rmtree(temp_dir, ignore_errors=True)
print(f'Wrote {os.path.abspath(args.out)}')

"""Extract a .unitypackage (gzipped tar of <guid>/{asset,asset.meta,pathname}) into a project folder.
Usage: python -I unitypackage_extract.py <package> <project dir>"""
import os
import sys
import tarfile

package, project = sys.argv[1], sys.argv[2]
entries = {}
with tarfile.open(package, 'r:gz') as tar:
    for member in tar.getmembers():
        if not member.isfile():
            continue
        guid, _, kind = member.name.lstrip('./').partition('/')
        entries.setdefault(guid, {})[kind] = tar.extractfile(member).read()

count = 0
for guid, parts in entries.items():
    if 'pathname' not in parts:
        continue
    rel = parts['pathname'].decode('utf-8').splitlines()[0].strip()
    target = os.path.normpath(os.path.join(project, rel))
    if not target.startswith(os.path.normpath(project) + os.sep):
        raise SystemExit('refusing path outside project: ' + rel)
    if 'asset' in parts:
        os.makedirs(os.path.dirname(target), exist_ok=True)
        with open(target, 'wb') as f:
            f.write(parts['asset'])
    else:
        os.makedirs(target, exist_ok=True)
    if 'asset.meta' in parts:
        with open(target + '.meta', 'wb') as f:
            f.write(parts['asset.meta'])
    count += 1
print(f'extracted {count} entries')

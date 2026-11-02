#!/usr/bin/env python3
"""Verify sealed prepublication inputs or their signed, payload-identical public form."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def candidate_feed():
    value = os.environ.get('BOUTQUIN_PREPARED_FEED')
    return Path(value).resolve() if value else None


def source(item):
    feed = candidate_feed()
    return str(feed) if feed else item['packageSource']


def verify(item, path):
    content = path.read_bytes()
    digest = hashlib.sha256(content).hexdigest()
    if candidate_feed() is not None:
        if digest != item['sha256']:
            raise ValueError('Prepared archive bytes changed: ' + item['id'])
    else:
        # Repository signing changes the ZIP, but must leave every original entry intact.
        subprocess.run(['dotnet', 'nuget', 'verify', str(path), '--all'], check=True,
                       stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)
        with zipfile.ZipFile(path) as archive:
            names = archive.namelist()
            if len(names) != len(set(names)) or '.signature.p7s' not in names:
                raise ValueError('Invalid signed archive inventory')
            actual = {n: hashlib.sha256(archive.read(n)).hexdigest()
                      for n in names if n != '.signature.p7s'}
            if actual != item['payloadSha256']:
                raise ValueError('Signed package payload differs from sealed input: ' + item['id'])
    return content


def write_config(path, items):
    feed = candidate_feed()
    if feed is None:
        raise ValueError('Set BOUTQUIN_PREPARED_FEED for private qualification')
    for item in items:
        verify(item, feed / item['file'])
    root = ET.Element('configuration')
    sources = ET.SubElement(root, 'packageSources')
    ET.SubElement(sources, 'clear')
    ET.SubElement(sources, 'add', key='prepared', value=str(feed))
    ET.SubElement(sources, 'add', key='nuget.org', value='https://api.nuget.org/v3/index.json')
    mappings = ET.SubElement(root, 'packageSourceMapping')
    prepared = ET.SubElement(mappings, 'packageSource', key='prepared')
    for item in items:
        ET.SubElement(prepared, 'package', pattern=item['id'])
    public = ET.SubElement(mappings, 'packageSource', key='nuget.org')
    ET.SubElement(public, 'package', pattern='*')
    with path.open('xb') as stream:
        ET.ElementTree(root).write(stream, encoding='utf-8', xml_declaration=True)


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--config', required=True, type=Path)
    a = p.parse_args()
    write_config(a.config, json.loads((ROOT / 'dependency-source.json').read_text())['preparedPackages'])
    print('PASS: sealed input hashes and exact package source mappings')

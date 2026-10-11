#!/usr/bin/env python3
"""Build only this plugin using public host DLLs, then package and preflight locally."""
import argparse
import os
import re
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

p = argparse.ArgumentParser()
p.add_argument('--phinix-root', type=Path, required=True)
p.add_argument('--game-references', type=Path, required=True)
p.add_argument('--host-assemblies', type=Path)
p.add_argument('--packager', type=Path)
p.add_argument('--framework-references', type=Path)
p.add_argument('--version')
p.add_argument('--output', type=Path, required=True)
p.add_argument('--bundle-output', type=Path)
a = p.parse_args()
host = a.phinix_root.resolve()
game = a.game_references.resolve()
root = Path(__file__).resolve().parent
properties = ET.parse(root/'example/Example.csproj').getroot().find('PropertyGroup')
version = a.version or properties.findtext('Version')
assembly = properties.findtext('AssemblyName')
package_id = properties.findtext('PluginPackageId')
name = properties.findtext('PluginDisplayName')
if not all([version, assembly, package_id, name]):
    raise SystemExit('Declare Version, AssemblyName, PluginPackageId and PluginDisplayName in Example.csproj.')
if not re.fullmatch(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)', version):
    raise SystemExit('Version must be three nonnegative release numbers.')

def public_dll(name, fallback):
    if a.host_assemblies:
        folder = a.host_assemblies.resolve()
        matches = [file for file in folder.glob('*.dll')
                   if file.name == name+'.dll' or file.name.endswith('-'+name+'.dll')]
        if len(matches) != 1:
            raise SystemExit('Expected one public '+name+' DLL in '+str(folder))
        return matches[0]
    if not fallback.is_file():
        raise SystemExit('Prepare public host DLLs once, or use --host-assemblies. Missing: '+str(fallback))
    return fallback

utils = public_dll('Utils', host/'Dependencies/Phinix.Common/Common/Utils/bin/Release/net472/Utils.dll')
abstractions = public_dll('ClientExtensionAbstractions', host/'Client/ClientExtensionAbstractions/bin/Release/ClientExtensionAbstractions.dll')
subprocess.run(['dotnet', 'build', str(root/'example/Example.csproj'), '-c', 'Release', '-m:1',
                '-p:PhinixRoot='+str(host), '-p:GameReferences='+str(game),
                '-p:Version='+version, '-p:AssemblyVersion='+version+'.0',
                '-p:PhinixUtilsAssemblyPath='+str(utils), '-p:PhinixAbstractionsAssemblyPath='+str(abstractions)], check=True)
tool = host/'Extensions/PluginStore/Tools/ManagedPackageTool'
packager = a.packager.resolve() if a.packager else tool/'bin/Release/net10.0/ManagedPackageTool.dll'
if not packager.is_file():
    if a.packager:
        raise SystemExit('Packager does not exist: '+str(packager))
    subprocess.run(['dotnet', 'build', str(tool/'ManagedPackageTool.csproj'), '-c', 'Release',
                    '-m:1', '-p:BuildInParallel=false', '-p:NuGetAudit=false'], check=True)
references = []
framework = a.framework_references.resolve() if a.framework_references else game
if not (framework/'mscorlib.dll').is_file() and not a.framework_references:
    cache = Path(os.environ.get('NUGET_PACKAGES', str(Path.home()/'.nuget/packages')))
    candidates = list((cache/'microsoft.netframework.referenceassemblies.net472').glob('*/build/.NETFramework/v4.7.2/mscorlib.dll'))
    if len(candidates) == 1:
        framework = candidates[0].parent
if not (framework/'mscorlib.dll').is_file():
    raise SystemExit('Provide --framework-references with net472 mscorlib.dll, or use full RimWorld Managed references.')
references += ['--host-assembly', str(framework/'mscorlib.dll'), '--host-assembly', str(framework/'System.Core.dll')]
for dll in ['Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll',
            'UnityEngine.TextRenderingModule.dll', 'UnityEngine.IMGUIModule.dll']:
    references += ['--host-assembly', str(game/dll)]
references += ['--host-assembly', str(utils), '--host-assembly', str(abstractions)]
command = ['dotnet', str(packager), '--assembly', str(root/'example/bin/Release/net472'/ (assembly+'.dll')),
           '--package-id', package_id, '--name', name, '--version', version,
           '--config', str(root/'example/package-config.json'), '--output', str(a.output.resolve())]
for locale in ['en-US', 'zh-CN']:
    command += ['--language-file', str(root/'example/Resources/Localization'/ (locale+'.json'))]
command += references
if a.bundle_output:
    command += ['--bundle-output', str(a.bundle_output.resolve())]
subprocess.run(command, check=True)
subprocess.run(['dotnet', str(packager), '--validate', str(a.output.resolve())]+references, check=True)

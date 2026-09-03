#!/usr/bin/env node
// Builds the DragResizeCanvas add-in and packs it into a .mpack file.
// Launched from .vscode/launch.json (Run and Debug -> "Build & Pack .mpack").

'use strict';

const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const root = process.cwd();

function run (cmd, args, opts = {}) {
	console.log(`\n$ ${cmd} ${args.join(' ')}`);
	const res = spawnSync (cmd, args, {
		stdio: 'inherit',
		...opts,
	});
	if (res.status !== 0)
		process.exit (res.status ?? 1);
}

// 1) Compile in Release mode.
run ('dotnet', ['build', '-c', 'Release'], { cwd: root });

// 2) Resolve the add-in version (csproj <Version>X.Y.Z.0>) so the mpack
//    name matches, e.g. DragResizeCanvas.0.2.0.mpack.
const csproj = fs.readFileSync (path.join (root, 'DragResizeCanvas', 'DragResizeCanvas.csproj'), 'utf8');
const version = /<Version>\s*(\d+\.\d+\.\d+)/.exec (csproj)?.[1];
if (!version) {
	console.error ('Could not determine the add-in version from DragResizeCanvas.csproj.');
	process.exit (1);
}

// 3) Pack the assembly. mautil targets .NET 6, so allow it to roll forward
//    to the installed (newer) runtime.
run ('mautil', ['pack', path.join (root, 'DragResizeCanvas', 'bin', 'Release', 'net8.0', 'DragResizeCanvas.dll')], {
	cwd: root,
	env: { ...process.env, DOTNET_ROLL_FORWARD: 'LatestMajor' },
});

console.log (`\nDone: ${path.join (root, `DragResizeCanvas.${version}.mpack`)}`);

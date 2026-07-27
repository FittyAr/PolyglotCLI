# Pre-push hook

Este directorio tiene un hook `pre-push` que corre `dotnet build` +
`dotnet test` antes de cada `git push`. Si el build falla o los tests
no pasan, el push se bloquea y el error se muestra en pantalla.

## Activarlo (una vez por clone)

```powershell
git config core.hooksPath .githooks
```

Eso le dice a git que busque los hooks en este directorio en lugar
de `.git/hooks/`. Sin este paso, el hook no se ejecuta.

## Bypassearlo en una emergencia

```powershell
git push --no-verify
```

Útil si sabés que el build está roto pero necesitás pushear de
todos modos. No abusar.

## Archivos

- `pre-push.bat` — wrapper de Windows. Git para Windows puede
  ejecutar archivos `.bat` como hooks directamente, pero
  PowerShell rechaza `-File` sin extensión `.ps1`, por eso el
  wrapper existe.
- `pre-push.ps1` — la lógica real: corre `dotnet build` +
  `dotnet test` y devuelve exit code 0/1 según el resultado.

## Compatibilidad

El hook es Windows-only (PowerShell + .bat). En Unix el
`pre-push.ps1` necesita `pwsh` y el `.bat` no se ejecuta; el
hook simplemente no corre. Si en el futuro hay devs en Mac/Linux,
se puede agregar un `pre-push` (sin extensión) con shebang
`#!/bin/sh` que llame a `pwsh pre-push.ps1`.

## Cron de backup

Además del hook, hay un cron `polyglotcli-check` (en el dataDir
del agent) que corre cada 30 min y verifica el estado del build
de forma pasiva. Es la red de seguridad por si alguien pushea
desde un path que bypassa el hook (otro clone, GitHub Desktop,
etc).

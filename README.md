# Block Blast (Unity) – APK via GitHub Actions

Everything (sprites, UI, sounds) is generated in code – no imported assets.

## One-time setup
1. Unity Hub -> install **Unity 2022.3 LTS** -> New project -> **2D** template -> name it `BlockBlast`.
2. Copy `Assets/`, `.github/` and `.gitignore` from this zip into that project's root (merge folders).
3. Open the project. Open `Assets/Editor/BuildScript.cs` and set `CompanyName` + `BundleId`.
4. Menu **BlockBlast -> Apply Android Settings**, then press **Play** (the game boots itself in any scene).
5. Push the project to a GitHub repo, branch `main`.

## Unity licence for CI (Personal licence, free)
Follow https://game.ci/docs/github/activation, then add these repo secrets
(Settings -> Secrets and variables -> Actions):
- `UNITY_LICENSE` – full text of your `Unity_lic.ulf`
  (Windows: `C:\ProgramData\Unity\Unity_lic.ulf`)
- `UNITY_EMAIL` – your Unity account email
- `UNITY_PASSWORD` – your Unity account password

## Build
Push to `main` (or Actions -> Build Android APK -> Run workflow).
When it finishes, download the **BlockBlast-apk** artifact, unzip, copy the .apk to your phone, install.
First build is slow (IL2CPP, ~15-25 min); later ones reuse the Library cache.
